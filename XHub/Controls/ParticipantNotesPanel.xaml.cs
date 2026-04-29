using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XHub.Models;
using XHub.Services;

namespace XHub.Controls;

public partial class ParticipantNotesPanel : UserControl
{
    private const string EmptyCheckbox = "☐";
    private const string CheckedCheckbox = "☑";
    private const string CheckboxPrefix = "☐ ";
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(800);

    private readonly ParticipantNotesService _notesService = new(App.NotesDirectoryPath);
    private readonly DispatcherTimer _autosaveTimer;
    private string? _participantKey;
    private string _currentHighlightColor = "#FFF2B8";
    private bool _isLoading;
    private bool _isUpdatingToolbar;

    public ParticipantNotesPanel()
    {
        InitializeComponent();
        _autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        _autosaveTimer.Tick += AutosaveTimer_OnTick;
        SetCurrentHighlightColor(_currentHighlightColor);
        UpdateEnabledState();
    }

    public void SetParticipant(ParticipantIndexEntry? participant)
    {
        var nextParticipantKey = participant?.ParticipantKey;
        if (string.Equals(_participantKey, nextParticipantKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FlushPendingAutosave();
        _participantKey = nextParticipantKey;

        try
        {
            _isLoading = true;
            Editor.Document.Blocks.Clear();
            if (string.IsNullOrWhiteSpace(_participantKey))
            {
                Editor.Document.Blocks.Add(new Paragraph());
            }
            else
            {
                _notesService.LoadInto(_participantKey, Editor.Document);
            }
        }
        finally
        {
            _isLoading = false;
            UpdateEnabledState();
            UpdateToolbarState();
        }
    }

    public void FlushPendingAutosave()
    {
        _autosaveTimer.Stop();
        if (!string.IsNullOrWhiteSpace(_participantKey))
        {
            SaveCurrentNote();
        }
    }

    private void AutosaveTimer_OnTick(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();
        SaveCurrentNote();
    }

    private void SaveCurrentNote()
    {
        if (string.IsNullOrWhiteSpace(_participantKey))
        {
            return;
        }

        _notesService.SaveOrDelete(_participantKey, Editor.Document);
    }

    private void BoldButton_OnClick(object sender, RoutedEventArgs e) =>
        ExecuteEditorCommand(EditingCommands.ToggleBold);

    private void ItalicButton_OnClick(object sender, RoutedEventArgs e) =>
        ExecuteEditorCommand(EditingCommands.ToggleItalic);

    private void StrikeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var currentValue = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var hasStrike = currentValue is TextDecorationCollection decorations &&
                        decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough);
        Editor.Selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            hasStrike ? null : TextDecorations.Strikethrough);
        Editor.Focus();
        UpdateToolbarState();
    }

    private void BulletsButton_OnClick(object sender, RoutedEventArgs e) =>
        ExecuteEditorCommand(EditingCommands.ToggleBullets);

    private void NumberingButton_OnClick(object sender, RoutedEventArgs e) =>
        ExecuteEditorCommand(EditingCommands.ToggleNumbering);

    private void CheckboxButton_OnClick(object sender, RoutedEventArgs e)
    {
        RemoveListFormattingIfNeeded();
        ToggleOrInsertCheckbox();
        Editor.Focus();
    }

    private void UndoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Editor.CanUndo)
        {
            Editor.Undo();
        }

        Editor.Focus();
    }

    private void RedoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Editor.CanRedo)
        {
            Editor.Redo();
        }

        Editor.Focus();
    }

    private void FontSizeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Editor is null || _isLoading)
        {
            return;
        }

        if (FontSizeComboBox.SelectedItem is not ComboBoxItem { Content: string sizeText } ||
            !double.TryParse(sizeText, out var fontSize))
        {
            return;
        }

        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
        Editor.Focus();
    }

    private void ApplyHighlightButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyHighlight(_currentHighlightColor);
    }

    private void HighlightDropdownButton_OnClick(object sender, RoutedEventArgs e)
    {
        HighlightPopup.IsOpen = true;
    }

    private void HighlightColorButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string color })
        {
            return;
        }

        SetCurrentHighlightColor(color);
        HighlightPopup.IsOpen = false;
        Editor.Focus();
    }

    private void ClearHighlightButton_OnClick(object sender, RoutedEventArgs e)
    {
        Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
        HighlightPopup.IsOpen = false;
        Editor.Focus();
    }

    private void ApplyHighlight(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        Editor.Focus();
    }

    private void SetCurrentHighlightColor(string color)
    {
        _currentHighlightColor = color;
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        HighlightSwatch.Background = brush;
    }

    private void Editor_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateToolbarState();
    }

    private void Editor_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || string.IsNullOrWhiteSpace(_participantKey))
        {
            return;
        }

        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private void Editor_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var currentParagraph = Editor.CaretPosition.Paragraph;
        if (currentParagraph is null || !TryGetCheckboxPrefix(currentParagraph, out _))
        {
            return;
        }

        e.Handled = true;
        var newParagraphPosition = Editor.CaretPosition.InsertParagraphBreak();
        Editor.CaretPosition = InsertCheckboxPrefix(newParagraphPosition);
    }

    private void Editor_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(Editor);
        var textPointer = Editor.GetPositionFromPoint(point, true);
        var paragraph = textPointer?.Paragraph;
        if (paragraph is null)
        {
            return;
        }

        var paragraphText = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        var trimmed = paragraphText.TrimStart();
        if (!trimmed.StartsWith(EmptyCheckbox, StringComparison.Ordinal) &&
            !trimmed.StartsWith(CheckedCheckbox, StringComparison.Ordinal))
        {
            return;
        }

        var paragraphBounds = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (point.X > paragraphBounds.X + 24)
        {
            return;
        }

        ToggleCheckbox(paragraph);
        e.Handled = true;
        Editor.Focus();
    }

    private void ExecuteEditorCommand(RoutedUICommand command)
    {
        command.Execute(null, Editor);
        Editor.Focus();
        UpdateToolbarState();
    }

    private void UpdateEnabledState()
    {
        var hasParticipant = !string.IsNullOrWhiteSpace(_participantKey);
        Editor.IsEnabled = hasParticipant;
        EmptyStateTextBlock.Visibility = hasParticipant ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateToolbarState()
    {
        if (_isUpdatingToolbar || Editor is null || BoldToggleButton is null)
        {
            return;
        }

        try
        {
            _isUpdatingToolbar = true;
            BoldToggleButton.IsChecked = IsSelectionBold();
            ItalicToggleButton.IsChecked = IsSelectionItalic();
            StrikeToggleButton.IsChecked = IsSelectionStrikethrough();
        }
        finally
        {
            _isUpdatingToolbar = false;
        }
    }

    private bool IsSelectionBold()
    {
        var value = Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        return value is FontWeight weight && weight == FontWeights.Bold;
    }

    private bool IsSelectionItalic()
    {
        var value = Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty);
        return value is FontStyle style && style == FontStyles.Italic;
    }

    private bool IsSelectionStrikethrough()
    {
        var value = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        return value is TextDecorationCollection decorations &&
               decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough);
    }

    private void ToggleOrInsertCheckbox()
    {
        if (!Editor.Selection.IsEmpty)
        {
            ToggleOrInsertCheckboxForSelection();
            return;
        }

        ToggleOrInsertCheckboxAtCaret();
    }

    private void ToggleOrInsertCheckboxForSelection()
    {
        var paragraphs = GetSelectedParagraphs();
        var allHaveCheckboxes = paragraphs.Count > 0 &&
                                paragraphs.All(paragraph => TryGetCheckboxPrefix(paragraph, out _));

        foreach (var paragraph in paragraphs)
        {
            if (allHaveCheckboxes)
            {
                RemoveCheckboxPrefix(paragraph);
                continue;
            }

            if (!TryGetCheckboxPrefix(paragraph, out _))
            {
                InsertCheckboxPrefix(paragraph.ContentStart);
            }
        }
    }

    private List<Paragraph> GetSelectedParagraphs()
    {
        var paragraphs = new List<Paragraph>();
        var pointer = Editor.Selection.Start;

        while (pointer is not null && pointer.CompareTo(Editor.Selection.End) <= 0)
        {
            var paragraph = pointer.Paragraph;
            if (paragraph is not null && !paragraphs.Contains(paragraph))
            {
                paragraphs.Add(paragraph);
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        if (paragraphs.Count == 0 && Editor.Selection.Start.Paragraph is not null)
        {
            paragraphs.Add(Editor.Selection.Start.Paragraph);
        }

        return paragraphs;
    }

    private void ToggleOrInsertCheckboxAtCaret()
    {
        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph is null)
        {
            Editor.CaretPosition = InsertCheckboxPrefix(Editor.CaretPosition);
            return;
        }

        if (TryGetCheckboxPrefix(paragraph, out _))
        {
            ToggleCheckbox(paragraph);
            return;
        }

        Editor.CaretPosition = InsertCheckboxPrefix(paragraph.ContentStart);
    }

    private static bool TryGetCheckboxPrefix(Paragraph paragraph, out bool isChecked)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimStart();
        if (text.StartsWith(CheckedCheckbox, StringComparison.Ordinal))
        {
            isChecked = true;
            return true;
        }

        if (text.StartsWith(EmptyCheckbox, StringComparison.Ordinal))
        {
            isChecked = false;
            return true;
        }

        isChecked = false;
        return false;
    }

    private static void ToggleCheckbox(Paragraph paragraph)
    {
        if (!TryGetCheckboxPrefix(paragraph, out var isChecked))
        {
            return;
        }

        var target = isChecked ? CheckedCheckbox : EmptyCheckbox;
        var replacement = isChecked ? EmptyCheckbox : CheckedCheckbox;
        var pointer = paragraph.ContentStart;

        while (pointer is not null && pointer.CompareTo(paragraph.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                var index = text.IndexOf(target, StringComparison.Ordinal);
                if (index >= 0)
                {
                    var start = pointer.GetPositionAtOffset(index);
                    var end = start?.GetPositionAtOffset(target.Length);
                    if (start is not null && end is not null)
                    {
                        new TextRange(start, end).Text = replacement;
                        ApplyCheckboxGlyphFormatting(start, replacement.Length);
                    }

                    return;
                }
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
    }

    private static void RemoveCheckboxPrefix(Paragraph paragraph)
    {
        var pointer = paragraph.ContentStart;

        while (pointer is not null && pointer.CompareTo(paragraph.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                var emptyIndex = text.IndexOf(EmptyCheckbox, StringComparison.Ordinal);
                var checkedIndex = text.IndexOf(CheckedCheckbox, StringComparison.Ordinal);
                var index = emptyIndex >= 0 ? emptyIndex : checkedIndex;

                if (index >= 0)
                {
                    var removeLength = 1;
                    while (text.Length > index + removeLength &&
                           (text[index + removeLength] == ' ' || text[index + removeLength] == '\u00A0'))
                    {
                        removeLength++;
                    }

                    var start = pointer.GetPositionAtOffset(index);
                    var end = start?.GetPositionAtOffset(removeLength);
                    if (start is not null && end is not null)
                    {
                        new TextRange(start, end).Text = string.Empty;
                        RemoveLeadingCheckboxWhitespace(paragraph);
                        ResetCheckboxParagraphLayout(paragraph);
                    }

                    return;
                }
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
    }

    private void RemoveListFormattingIfNeeded()
    {
        var listKinds = GetSelectedParagraphs()
            .Select(GetListKind)
            .Where(kind => kind is not ListKind.None)
            .Distinct()
            .ToList();

        if (listKinds.Contains(ListKind.Bullets))
        {
            EditingCommands.ToggleBullets.Execute(null, Editor);
        }

        if (listKinds.Contains(ListKind.Numbering))
        {
            EditingCommands.ToggleNumbering.Execute(null, Editor);
        }
    }

    private static ListKind GetListKind(Paragraph paragraph)
    {
        if (paragraph.Parent is not ListItem { Parent: List list })
        {
            return ListKind.None;
        }

        return list.MarkerStyle is TextMarkerStyle.Decimal or
            TextMarkerStyle.LowerLatin or
            TextMarkerStyle.UpperLatin or
            TextMarkerStyle.LowerRoman or
            TextMarkerStyle.UpperRoman
            ? ListKind.Numbering
            : ListKind.Bullets;
    }

    private static TextPointer InsertCheckboxPrefix(TextPointer pointer)
    {
        var start = pointer.GetInsertionPosition(LogicalDirection.Forward);
        ApplyCheckboxParagraphLayout(start.Paragraph);
        start.InsertTextInRun(CheckboxPrefix);
        var end = start.GetPositionAtOffset(EmptyCheckbox.Length);
        if (end is not null)
        {
            ApplyCheckboxGlyphFormatting(start, EmptyCheckbox.Length);
        }

        var afterPrefix = start.GetPositionAtOffset(CheckboxPrefix.Length, LogicalDirection.Forward);
        return afterPrefix?.GetInsertionPosition(LogicalDirection.Forward) ?? start;
    }

    private static void ApplyCheckboxGlyphFormatting(TextPointer start, int length)
    {
        var end = start.GetPositionAtOffset(length);
        if (end is null)
        {
            return;
        }

        var range = new TextRange(start, end);
        range.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily("Segoe UI Symbol"));
        range.ApplyPropertyValue(TextElement.FontSizeProperty, 14.0);
    }

    private static void ApplyCheckboxParagraphLayout(Paragraph? paragraph)
    {
        if (paragraph is null)
        {
            return;
        }

        paragraph.Margin = new Thickness(20, 0, 0, 0);
        paragraph.TextIndent = -20;
    }

    private static void ResetCheckboxParagraphLayout(Paragraph paragraph)
    {
        paragraph.Margin = new Thickness(0);
        paragraph.TextIndent = 0;
    }

    private static void RemoveLeadingCheckboxWhitespace(Paragraph paragraph)
    {
        var pointer = paragraph.ContentStart;

        while (pointer is not null && pointer.CompareTo(paragraph.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
            {
                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
                continue;
            }

            var text = pointer.GetTextInRun(LogicalDirection.Forward);
            var removeLength = 0;
            while (removeLength < text.Length &&
                   (text[removeLength] == ' ' || text[removeLength] == '\u00A0'))
            {
                removeLength++;
            }

            if (removeLength == 0)
            {
                return;
            }

            var end = pointer.GetPositionAtOffset(removeLength);
            if (end is not null)
            {
                new TextRange(pointer, end).Text = string.Empty;
            }

            return;
        }
    }

    private enum ListKind
    {
        None,
        Bullets,
        Numbering
    }
}
