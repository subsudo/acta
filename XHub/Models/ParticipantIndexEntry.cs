using System;
using System.Collections.Generic;

namespace XHub.Models;

public class ParticipantIndexEntry
{
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
    public bool HasStatusTag => !string.IsNullOrWhiteSpace(StatusTag);
    public IReadOnlyList<string> SearchTokens { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SearchTokensFallback { get; set; } = Array.Empty<string>();
    public ParticipantMiniScheduleSummary? MiniSchedule { get; set; }
}


