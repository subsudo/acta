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

namespace XHub.Controls;

public partial class ParticipantDetailPanel : UserControl
{
    private const int PhotoCacheLimit = 32;

    private static readonly Brush SchedulePrimaryBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
    private static readonly Brush ScheduleStatusExtBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E7156"));
    private static readonly Brush ScheduleStatusExtBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9E1C6"));
    private static readonly Brush ScheduleStatusDispBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#745050"));
    private static readonly Brush ScheduleStatusDispBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4C1C1"));


    private static readonly object PhotoCacheSync = new();
    private static readonly Dictionary<string, CachedPhoto> PhotoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> PhotoCacheOrder = new();

    private readonly Dictionary<string, ScheduleCellVisual> _scheduleVisuals = new(StringComparer.OrdinalIgnoreCase);
    private ParticipantIndexEntry? _participant;
    private double? _scheduleBaseWidth;

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
            visual.Primary.Foreground = (Brush)FindResource("Brush.SubtleText");
            visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }

        var primaryEntry = cell.Entries[0];
        if (cell.HasSupplementalDaz)
        {
            visual.Primary.Margin = new Thickness(0, -2, 0, 0);
            visual.Teacher.Margin = new Thickness(1, -2, 0, 0);
            visual.Room.Margin = new Thickness(0, -2, 2, 0);
            visual.Additional.Margin = new Thickness(0, -2, 0, 0);
            visual.Additional.Text = "+DAZ";
            visual.Additional.Visibility = Visibility.Visible;
        }

        visual.Primary.Text = primaryEntry.Group;
        visual.Primary.Foreground = SchedulePrimaryBrush;
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
            visual.Primary.Foreground = (Brush)FindResource("Brush.SubtleText");
        }
    }

    private void ResetScheduleCellVisual(ScheduleCellVisual visual)
    {
        visual.Primary.Text = string.Empty;
        visual.Primary.Foreground = SchedulePrimaryBrush;
        visual.Primary.Background = Brushes.Transparent;
        visual.Primary.FontSize = 10;
        visual.Primary.FontWeight = FontWeights.SemiBold;
        visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Primary.TextAlignment = TextAlignment.Center;
        visual.Primary.Margin = new Thickness(0);
        visual.Room.Text = string.Empty;
        visual.Room.Foreground = (Brush)FindResource("Brush.SecondaryText");
        visual.Room.FontSize = 8;
        visual.Room.Margin = new Thickness(0, 0, 2, 0);
        visual.Teacher.Text = string.Empty;
        visual.Teacher.Foreground = (Brush)FindResource("Brush.SecondaryText");
        visual.Teacher.FontSize = 8.4;
        visual.Teacher.Margin = new Thickness(1, 0, 0, 0);
        visual.Additional.Text = string.Empty;
        visual.Additional.Foreground = (Brush)FindResource("Brush.SubtleText");
        visual.Additional.Background = Brushes.Transparent;
        visual.Additional.FontSize = 6.4;
        visual.Additional.FontWeight = FontWeights.Normal;
        visual.Additional.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Additional.Margin = new Thickness(0);
        visual.Additional.Visibility = Visibility.Collapsed;
    }

    private static void SetStatusCellVisual(ScheduleCellVisual visual, string label, Brush foreground, Brush background)
    {
        visual.Primary.Text = $" {label} ";
        visual.Primary.Foreground = foreground;
        visual.Primary.Background = background;
        visual.Primary.FontSize = 8.2;
        visual.Primary.FontWeight = FontWeights.SemiBold;
        visual.Primary.HorizontalAlignment = HorizontalAlignment.Center;
        visual.Primary.TextAlignment = TextAlignment.Center;
        visual.Room.Text = string.Empty;
        visual.Teacher.Text = string.Empty;
        visual.Additional.Text = string.Empty;
        visual.Additional.Visibility = Visibility.Collapsed;
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

            PhotoCardBorder.Margin = new Thickness(0, 0, 12, 0);
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

        PhotoCardBorder.Margin = new Thickness(0, 0, 0, 10);
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

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });
    }

    private sealed record CachedPhoto(DateTime LastWriteTimeUtc, BitmapImage Bitmap);
    private sealed record ScheduleCellVisual(TextBlock Primary, TextBlock Room, TextBlock Teacher, TextBlock Additional);
}




























