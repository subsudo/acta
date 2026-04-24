using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XHub.Models;
using XHub.Services;

namespace XHub.Controls;

public partial class ParticipantDetailPanel : UserControl
{
    private const int PhotoCacheLimit = 32;
    private const string PrimaryTextBrushKey = "Brush.PrimaryText";
    private const string SecondaryTextBrushKey = "Brush.SecondaryText";
    private const string SubtleTextBrushKey = "Brush.SubtleText";

    private static readonly Brush ScheduleStatusExtBrush = CreateFrozenBrush("#3E7156");
    private static readonly Brush ScheduleStatusExtBackground = CreateFrozenBrush("#B9E1C6");
    private static readonly Brush ScheduleStatusDispBrush = CreateFrozenBrush("#745050");
    private static readonly Brush ScheduleStatusDispBackground = CreateFrozenBrush("#E4C1C1");


    private static readonly object PhotoCacheSync = new();
    private static readonly Dictionary<string, CachedPhoto> PhotoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> PhotoCacheOrder = new();

    private readonly Dictionary<string, ScheduleCellVisual> _scheduleVisuals = new(StringComparer.OrdinalIgnoreCase);
    private ParticipantIndexEntry? _participant;
    private double? _scheduleBaseWidth;
    private int CurrentUiScaleLevel => App.NormalizeUiScaleLevel(App.UserPrefs.UiScaleLevel);

    public ParticipantDetailPanel()
    {
        InitializeComponent();
        InitializeScheduleVisuals();
        SizeChanged += (_, _) => UpdateDetailLayout();
        Loaded += (_, _) => UpdateDetailLayout();
        LinkCardBorder.SizeChanged += (_, _) => UpdateScheduleWidth();
    }

    public ParticipantIndexEntry? CurrentParticipant => _participant;

    public void UpdateParticipant(ParticipantIndexEntry? participant, IReadOnlyList<DetailModuleConfig> modules)
    {
        _participant = participant;

        if (participant is null)
        {
            Clear();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ContentScrollViewer.Visibility = Visibility.Visible;
        DetailTitleTextBox.Text = participant.DisplayName;
        InitialsTextBox.Text = string.IsNullOrWhiteSpace(participant.Initials) ? string.Empty : participant.Initials;
        UpdateCounselorInfo(participant.CounselorInitials);

        if (App.Config.ShowParticipantPhoto)
        {
            UpdatePhoto(participant.ImagePath, participant.Initials);
        }
        else
        {
            ResetPhotoVisuals();
        }

        UpdateOdooLink(participant.OdooUrl);
        UpdateSchedule(participant.MiniSchedule);
        UpdateDetailLayout();
    }

    public void Clear()
    {
        _participant = null;
        EmptyState.Visibility = Visibility.Visible;
        ContentScrollViewer.Visibility = Visibility.Collapsed;
        DetailTitleTextBox.Text = string.Empty;
        InitialsTextBox.Text = string.Empty;
        CounselorTextBlock.Text = string.Empty;
        ResetPhotoVisuals();
        OdooButton.Visibility = Visibility.Collapsed;
        OdooFallbackTextBlock.Visibility = Visibility.Collapsed;
        ScheduleCardBorder.Visibility = Visibility.Collapsed;
        ScheduleGrid.Visibility = Visibility.Collapsed;
        ScheduleMessageTextBlock.Visibility = Visibility.Collapsed;
        UpdateDetailLayout();
    }

    private void OdooButton_OnClick(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl(_participant?.OdooUrl);
    }

    private void UpdatePhoto(string? imagePath, string? initials)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            ShowPhotoPlaceholder("Kein Foto hinterlegt", initials);
            return;
        }

        try
        {
            var bitmap = GetCachedPhoto(imagePath);
            PhotoImage.Source = bitmap;
            PhotoImage.Visibility = Visibility.Visible;
            PhotoPlaceholderSurface.Visibility = Visibility.Collapsed;
            PhotoHintTextBlock.Text = string.Empty;
            PhotoPlaceholderInitialsTextBlock.Text = string.Empty;
            PhotoPlaceholderInitialsTextBlock.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ShowPhotoPlaceholder("Foto konnte nicht geladen werden", initials);
        }
    }

    private void ResetPhotoVisuals()
    {
        PhotoImage.Source = null;
        PhotoImage.Visibility = Visibility.Collapsed;
        PhotoPlaceholderSurface.Visibility = Visibility.Visible;
        PhotoHintTextBlock.Text = string.Empty;
        PhotoPlaceholderInitialsTextBlock.Text = string.Empty;
        PhotoPlaceholderInitialsTextBlock.Visibility = Visibility.Collapsed;
    }

    private void ShowPhotoPlaceholder(string hint, string? initials)
    {
        PhotoImage.Source = null;
        PhotoImage.Visibility = Visibility.Collapsed;
        PhotoPlaceholderSurface.Visibility = Visibility.Visible;
        PhotoHintTextBlock.Text = hint;
        UpdatePhotoPlaceholderInitials(initials);
    }

    private void UpdatePhotoPlaceholderInitials(string? initials)
    {
        if (string.IsNullOrWhiteSpace(initials))
        {
            PhotoPlaceholderInitialsTextBlock.Text = string.Empty;
            PhotoPlaceholderInitialsTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        PhotoPlaceholderInitialsTextBlock.Text = initials.Trim();
        PhotoPlaceholderInitialsTextBlock.Visibility = Visibility.Visible;
    }

    private void UpdateCounselorInfo(string? counselorInitials)
    {
        var suffix = string.IsNullOrWhiteSpace(counselorInitials) ? string.Empty : $" {counselorInitials.Trim()}";
        CounselorTextBlock.Text = $"Beratungsperson:{suffix}";
    }

    private void UpdateOdooLink(string? odooUrl)
    {
        if (string.IsNullOrWhiteSpace(odooUrl))
        {
            OdooButton.Visibility = Visibility.Collapsed;
            OdooFallbackTextBlock.Visibility = Visibility.Visible;
            return;
        }

        OdooButton.Visibility = Visibility.Visible;
        OdooFallbackTextBlock.Visibility = Visibility.Collapsed;
    }

    private void UpdateSchedule(ParticipantMiniScheduleSummary? summary)
    {
        ClearScheduleVisuals();

        if (summary is null || summary.State == ParticipantMiniScheduleState.Hidden)
        {
            ScheduleCardBorder.Visibility = Visibility.Collapsed;
            return;
        }

        ScheduleCardBorder.Visibility = Visibility.Visible;
        ScheduleMessageTextBlock.Text = string.Empty;
        ScheduleMessageTextBlock.Visibility = Visibility.Collapsed;
        ScheduleGrid.Visibility = Visibility.Visible;
        ScheduleGrid.Opacity = summary.State == ParticipantMiniScheduleState.Ready ? 1.0 : 0.5;

        BindScheduleCell("Mo_VM", summary.GetCell("Mo", ParticipantMiniScheduleHalfDay.Morning));
        BindScheduleCell("Di_VM", summary.GetCell("Di", ParticipantMiniScheduleHalfDay.Morning));
        BindScheduleCell("Mi_VM", summary.GetCell("Mi", ParticipantMiniScheduleHalfDay.Morning));
        BindScheduleCell("Do_VM", summary.GetCell("Do", ParticipantMiniScheduleHalfDay.Morning));
        BindScheduleCell("Fr_VM", summary.GetCell("Fr", ParticipantMiniScheduleHalfDay.Morning));
        BindScheduleCell("Mo_NM", summary.GetCell("Mo", ParticipantMiniScheduleHalfDay.Afternoon));
        BindScheduleCell("Di_NM", summary.GetCell("Di", ParticipantMiniScheduleHalfDay.Afternoon));
        BindScheduleCell("Mi_NM", summary.GetCell("Mi", ParticipantMiniScheduleHalfDay.Afternoon));
        BindScheduleCell("Do_NM", summary.GetCell("Do", ParticipantMiniScheduleHalfDay.Afternoon));
        BindScheduleCell("Fr_NM", summary.GetCell("Fr", ParticipantMiniScheduleHalfDay.Afternoon));
    }
    private void BindScheduleCell(string key, ParticipantMiniScheduleCell cell)
    {
        if (!_scheduleVisuals.TryGetValue(key, out var visual))
        {
            return;
        }

        ResetScheduleCellVisual(visual);

        if (cell.Status == ParticipantMiniScheduleCellStatus.External)
        {
            SetStatusCellVisual(visual, "ext", ScheduleStatusExtBrush, ScheduleStatusExtBackground);
            return;
        }

        if (cell.Status == ParticipantMiniScheduleCellStatus.Dispensed)
        {
            SetStatusCellVisual(visual, "disp", ScheduleStatusDispBrush, ScheduleStatusDispBackground);
            return;
        }

        if (cell.Entries.Count == 0)
        {
            visual.Primary.Text = "-";
            ApplySubtleTextBrush(visual.Primary);
            visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }

        var primaryEntry = cell.Entries[0];
        if (cell.HasSupplementalDaz)
        {
            visual.Primary.Margin = new Thickness(0, -2, 0, 0);
            visual.Teacher.Margin = WithTopOffset(GetScheduleTeacherMargin(), -2);
            visual.Room.Margin = WithTopOffset(GetScheduleRoomMargin(), -2);
            visual.Additional.Margin = WithTopOffset(GetScheduleAdditionalMargin(), -2);
            visual.Additional.Text = "+DAZ";
            visual.Additional.Visibility = Visibility.Visible;
        }

        visual.Primary.Text = primaryEntry.Group;
        ApplyPrimaryTextBrush(visual.Primary);
        visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Room.Text = primaryEntry.Room;
        visual.Teacher.Text = BuildTeacherLine(primaryEntry);
    }

    private void ClearScheduleVisuals()
    {
        foreach (var visual in _scheduleVisuals.Values)
        {
            ResetScheduleCellVisual(visual);
            visual.Primary.Text = "-";
            ApplySubtleTextBrush(visual.Primary);
        }
    }

    private void ResetScheduleCellVisual(ScheduleCellVisual visual)
    {
        visual.Primary.Text = string.Empty;
        ApplyPrimaryTextBrush(visual.Primary);
        visual.Primary.Background = Brushes.Transparent;
        visual.Primary.FontSize = GetSchedulePrimaryFontSize();
        visual.Primary.FontWeight = FontWeights.SemiBold;
        visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Primary.TextAlignment = TextAlignment.Center;
        visual.Primary.Margin = new Thickness(0);
        visual.Room.Text = string.Empty;
        ApplySecondaryTextBrush(visual.Room);
        visual.Room.FontSize = GetScheduleRoomFontSize();
        visual.Room.Margin = GetScheduleRoomMargin();
        visual.Teacher.Text = string.Empty;
        ApplySecondaryTextBrush(visual.Teacher);
        visual.Teacher.FontSize = GetScheduleTeacherFontSize();
        visual.Teacher.Margin = GetScheduleTeacherMargin();
        visual.Additional.Text = string.Empty;
        ApplySubtleTextBrush(visual.Additional);
        visual.Additional.Background = Brushes.Transparent;
        visual.Additional.FontSize = GetScheduleAdditionalFontSize();
        visual.Additional.FontWeight = FontWeights.Normal;
        visual.Additional.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Additional.Margin = GetScheduleAdditionalMargin();
        visual.Additional.Visibility = Visibility.Collapsed;
    }

    private void SetStatusCellVisual(ScheduleCellVisual visual, string label, Brush foreground, Brush background)
    {
        visual.Primary.Text = $" {label} ";
        visual.Primary.Foreground = foreground;
        visual.Primary.Background = background;
        visual.Primary.FontSize = GetSchedulePrimaryFontSize();
        visual.Primary.FontWeight = FontWeights.SemiBold;
        visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Primary.TextAlignment = TextAlignment.Center;
        visual.Room.Text = string.Empty;
        visual.Teacher.Text = string.Empty;
        visual.Additional.Text = string.Empty;
        visual.Additional.Visibility = Visibility.Collapsed;
    }

    private static void ApplyPrimaryTextBrush(TextBlock textBlock)
    {
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, PrimaryTextBrushKey);
    }

    private static void ApplySecondaryTextBrush(TextBlock textBlock)
    {
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, SecondaryTextBrushKey);
    }

    private static void ApplySubtleTextBrush(TextBlock textBlock)
    {
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, SubtleTextBrushKey);
    }

    private static string BuildTeacherLine(ParticipantMiniScheduleEntry entry)
    {
        if (entry.Group.Contains("DAZ", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var teacher = string.IsNullOrWhiteSpace(entry.Teacher)
            ? string.Empty
            : entry.Teacher.Trim().ToUpperInvariant();

        return teacher;
    }

    private double GetSchedulePrimaryFontSize() => CurrentUiScaleLevel switch
    {
        1 => 8.8,
        2 => 9.2,
        3 => 9.6,
        4 => 10.0,
        _ => 10.2
    };

    private double GetScheduleTeacherFontSize() => CurrentUiScaleLevel switch
    {
        1 => 6.6,
        2 => 7.0,
        3 => 7.4,
        4 => 8.0,
        _ => 8.2
    };

    private double GetScheduleRoomFontSize() => CurrentUiScaleLevel switch
    {
        1 => 6.2,
        2 => 6.6,
        3 => 7.0,
        4 => 7.6,
        _ => 7.8
    };

    private double GetScheduleAdditionalFontSize() => CurrentUiScaleLevel switch
    {
        1 => 5.4,
        2 => 5.8,
        3 => 6.1,
        4 => 6.4,
        _ => 6.6
    };

    private Thickness GetScheduleTeacherMargin() => CurrentUiScaleLevel switch
    {
        1 => new Thickness(2, 0, 0, 0),
        2 => new Thickness(2, 0, 0, 0),
        3 => new Thickness(2, 0, 0, 0),
        4 => new Thickness(1, 0, 0, 0),
        _ => new Thickness(1, 0, 0, 0)
    };

    private Thickness GetScheduleRoomMargin() => CurrentUiScaleLevel switch
    {
        1 => new Thickness(0, 0, 3, 0),
        2 => new Thickness(0, 0, 3, 0),
        3 => new Thickness(0, 0, 3, 0),
        4 => new Thickness(0, 0, 2, 0),
        _ => new Thickness(0, 0, 2, 0)
    };

    private Thickness GetScheduleAdditionalMargin() => CurrentUiScaleLevel switch
    {
        1 => new Thickness(0, 0, 0, 0),
        2 => new Thickness(0, 0, 0, 0),
        3 => new Thickness(0, 0, 0, 0),
        4 => new Thickness(0),
        _ => new Thickness(0)
    };

    private static Thickness WithTopOffset(Thickness thickness, double topOffset)
    {
        return new Thickness(thickness.Left, thickness.Top + topOffset, thickness.Right, thickness.Bottom);
    }

    private void InitializeScheduleVisuals()
    {
        _scheduleVisuals["Mo_VM"] = new ScheduleCellVisual(ScheduleMoVmPrimaryTextBlock, ScheduleMoVmRoomTextBlock, ScheduleMoVmTeacherTextBlock, ScheduleMoVmAdditionalTextBlock);
        _scheduleVisuals["Di_VM"] = new ScheduleCellVisual(ScheduleDiVmPrimaryTextBlock, ScheduleDiVmRoomTextBlock, ScheduleDiVmTeacherTextBlock, ScheduleDiVmAdditionalTextBlock);
        _scheduleVisuals["Mi_VM"] = new ScheduleCellVisual(ScheduleMiVmPrimaryTextBlock, ScheduleMiVmRoomTextBlock, ScheduleMiVmTeacherTextBlock, ScheduleMiVmAdditionalTextBlock);
        _scheduleVisuals["Do_VM"] = new ScheduleCellVisual(ScheduleDoVmPrimaryTextBlock, ScheduleDoVmRoomTextBlock, ScheduleDoVmTeacherTextBlock, ScheduleDoVmAdditionalTextBlock);
        _scheduleVisuals["Fr_VM"] = new ScheduleCellVisual(ScheduleFrVmPrimaryTextBlock, ScheduleFrVmRoomTextBlock, ScheduleFrVmTeacherTextBlock, ScheduleFrVmAdditionalTextBlock);
        _scheduleVisuals["Mo_NM"] = new ScheduleCellVisual(ScheduleMoNmPrimaryTextBlock, ScheduleMoNmRoomTextBlock, ScheduleMoNmTeacherTextBlock, ScheduleMoNmAdditionalTextBlock);
        _scheduleVisuals["Di_NM"] = new ScheduleCellVisual(ScheduleDiNmPrimaryTextBlock, ScheduleDiNmRoomTextBlock, ScheduleDiNmTeacherTextBlock, ScheduleDiNmAdditionalTextBlock);
        _scheduleVisuals["Mi_NM"] = new ScheduleCellVisual(ScheduleMiNmPrimaryTextBlock, ScheduleMiNmRoomTextBlock, ScheduleMiNmTeacherTextBlock, ScheduleMiNmAdditionalTextBlock);
        _scheduleVisuals["Do_NM"] = new ScheduleCellVisual(ScheduleDoNmPrimaryTextBlock, ScheduleDoNmRoomTextBlock, ScheduleDoNmTeacherTextBlock, ScheduleDoNmAdditionalTextBlock);
        _scheduleVisuals["Fr_NM"] = new ScheduleCellVisual(ScheduleFrNmPrimaryTextBlock, ScheduleFrNmRoomTextBlock, ScheduleFrNmTeacherTextBlock, ScheduleFrNmAdditionalTextBlock);
        ClearScheduleVisuals();
    }

    private void UpdateDetailLayout()
    {
        var showPhoto = App.Config.ShowParticipantPhoto;
        var useSideBySide = showPhoto && ActualWidth >= 360;

        PhotoCardBorder.Visibility = showPhoto ? Visibility.Visible : Visibility.Collapsed;

        if (!showPhoto)
        {
            PhotoColumnDefinition.Width = new GridLength(0);
            LinkColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
            TopRowDefinition.Height = GridLength.Auto;
            BottomRowDefinition.Height = new GridLength(0);
            Grid.SetRow(LinkCardBorder, 0);
            Grid.SetColumn(LinkCardBorder, 0);
            Grid.SetColumnSpan(LinkCardBorder, 2);
            LinkCardBorder.Margin = new Thickness(0);
            UpdateScheduleWidth();
            return;
        }

        if (useSideBySide)
        {
            PhotoColumnDefinition.Width = GridLength.Auto;
            LinkColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
            TopRowDefinition.Height = GridLength.Auto;
            BottomRowDefinition.Height = new GridLength(0);

            Grid.SetRow(PhotoCardBorder, 0);
            Grid.SetColumn(PhotoCardBorder, 0);
            Grid.SetColumnSpan(PhotoCardBorder, 1);
            Grid.SetRow(LinkCardBorder, 0);
            Grid.SetColumn(LinkCardBorder, 1);
            Grid.SetColumnSpan(LinkCardBorder, 1);

            PhotoCardBorder.Margin = new Thickness(0, 0, 10, 0);
            LinkCardBorder.Margin = new Thickness(0);
            UpdateScheduleWidth();
            return;
        }

        PhotoColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        LinkColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        TopRowDefinition.Height = GridLength.Auto;
        BottomRowDefinition.Height = GridLength.Auto;

        Grid.SetRow(PhotoCardBorder, 0);
        Grid.SetColumn(PhotoCardBorder, 0);
        Grid.SetColumnSpan(PhotoCardBorder, 2);
        Grid.SetRow(LinkCardBorder, 1);
        Grid.SetColumn(LinkCardBorder, 0);
        Grid.SetColumnSpan(LinkCardBorder, 2);

        PhotoCardBorder.Margin = new Thickness(0, 0, 0, 8);
        LinkCardBorder.Margin = new Thickness(0);
        UpdateScheduleWidth();
    }

    private void UpdateScheduleWidth()
    {
        if (ScheduleGrid is null || LinkCardBorder is null)
        {
            return;
        }

        var linkWidth = LinkCardBorder.ActualWidth;
        if (linkWidth <= 1)
        {
            return;
        }

        _scheduleBaseWidth ??= linkWidth;

        var baseWidth = _scheduleBaseWidth.Value;
        var targetWidth = Math.Min(linkWidth, baseWidth * 1.25);
        ScheduleGrid.Width = targetWidth;
        ScheduleGrid.MaxWidth = targetWidth;
        ScheduleGrid.MinWidth = Math.Min(baseWidth, targetWidth);
    }

    private static BitmapImage GetCachedPhoto(string imagePath)
    {
        var fileInfo = new FileInfo(imagePath);
        var key = fileInfo.FullName;
        var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

        lock (PhotoCacheSync)
        {
            if (PhotoCache.TryGetValue(key, out var cached) && cached.LastWriteTimeUtc == lastWriteTimeUtc)
            {
                TouchPhotoCacheEntry(key);
                return cached.Bitmap;
            }
        }

        var bitmap = LoadBitmap(imagePath);

        lock (PhotoCacheSync)
        {
            PhotoCache[key] = new CachedPhoto(lastWriteTimeUtc, bitmap);
            TouchPhotoCacheEntry(key);
            TrimPhotoCache();
        }

        return bitmap;
    }

    private static BitmapImage LoadBitmap(string imagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void TouchPhotoCacheEntry(string key)
    {
        var existingNode = PhotoCacheOrder.Find(key);
        if (existingNode is not null)
        {
            PhotoCacheOrder.Remove(existingNode);
        }

        PhotoCacheOrder.AddLast(key);
    }

    private static void TrimPhotoCache()
    {
        while (PhotoCacheOrder.Count > PhotoCacheLimit)
        {
            var oldest = PhotoCacheOrder.First;
            if (oldest is null)
            {
                return;
            }

            PhotoCache.Remove(oldest.Value);
            PhotoCacheOrder.RemoveFirst();
        }
    }

    private static void OpenExternalUrl(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            AppLogger.Warn($"XHub.DetailPanel.OdooUrl ungültig oder nicht erlaubt: '{target}'");
            MessageBox.Show("Der Odoo-Link ist ungültig.", "Acta", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.DetailPanel.OdooUrl '{target}'", ex);
            MessageBox.Show("Der Odoo-Link konnte nicht geöffnet werden.", "Acta", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static Brush CreateFrozenBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }

    private sealed record CachedPhoto(DateTime LastWriteTimeUtc, BitmapImage Bitmap);
    private sealed record ScheduleCellVisual(TextBlock Primary, TextBlock Room, TextBlock Teacher, TextBlock Additional);
}




























