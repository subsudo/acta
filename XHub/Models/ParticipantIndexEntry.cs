using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XHub.Models;

public class ParticipantIndexEntry : INotifyPropertyChanged
{
    private IReadOnlyList<ParticipantHintDisplay> _activeHints = Array.Empty<ParticipantHintDisplay>();
    private IReadOnlyList<ParticipantHintDisplay> _hintMarkers = Array.Empty<ParticipantHintDisplay>();

    public string ParticipantKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string OdooUrl { get; set; } = string.Empty;
    public string CounselorInitials { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string StatusTag { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public bool HasStatusTag => !string.IsNullOrWhiteSpace(StatusTag);
    public IReadOnlyList<string> SearchTokens { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SearchTokensFallback { get; set; } = Array.Empty<string>();
    public ParticipantMiniScheduleSummary? MiniSchedule { get; set; }

    public IReadOnlyList<ParticipantHintDisplay> ActiveHints
    {
        get => _activeHints;
        set
        {
            var normalized = value.ToList();
            _activeHints = normalized;
            _hintMarkers = normalized.Take(4).ToList();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HintMarkers));
            OnPropertyChanged(nameof(HasActiveHints));
        }
    }

    public IReadOnlyList<ParticipantHintDisplay> HintMarkers => _hintMarkers;
    public bool HasActiveHints => ActiveHints.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

