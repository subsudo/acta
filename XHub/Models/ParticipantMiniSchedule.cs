using System;
using System.Collections.Generic;
using System.Linq;

namespace XHub.Models;

public enum ParticipantMiniScheduleState
{
    Hidden,
    Unavailable,
    Ready
}

public enum ParticipantMiniScheduleHalfDay
{
    Morning,
    Afternoon
}

public enum ParticipantMiniScheduleCellStatus
{
    None,
    External,
    Dispensed
}

public sealed class ParticipantMiniScheduleSummary
{
    public ParticipantMiniScheduleState State { get; set; } = ParticipantMiniScheduleState.Hidden;
    public string Message { get; set; } = string.Empty;
    public List<ParticipantMiniScheduleCell> Cells { get; set; } = CreateDefaultCells();

    public ParticipantMiniScheduleCell GetCell(string dayKey, ParticipantMiniScheduleHalfDay halfDay)
    {
        return Cells.First(cell =>
            string.Equals(cell.DayKey, dayKey, StringComparison.OrdinalIgnoreCase) &&
            cell.HalfDay == halfDay);
    }

    public static List<ParticipantMiniScheduleCell> CreateDefaultCells()
    {
        var cells = new List<ParticipantMiniScheduleCell>();
        var dayKeys = new[] { "Mo", "Di", "Mi", "Do", "Fr" };
        foreach (var dayKey in dayKeys)
        {
            cells.Add(new ParticipantMiniScheduleCell { DayKey = dayKey, HalfDay = ParticipantMiniScheduleHalfDay.Morning });
            cells.Add(new ParticipantMiniScheduleCell { DayKey = dayKey, HalfDay = ParticipantMiniScheduleHalfDay.Afternoon });
        }

        return cells;
    }
}

public sealed class ParticipantMiniScheduleCell
{
    public string DayKey { get; set; } = string.Empty;
    public ParticipantMiniScheduleHalfDay HalfDay { get; set; }
    public List<ParticipantMiniScheduleEntry> Entries { get; set; } = new();
    public ParticipantMiniScheduleCellStatus Status { get; set; }
    public bool HasSupplementalDaz { get; set; }
    public bool IsEmpty => Entries.Count == 0;
}

public sealed class ParticipantMiniScheduleEntry
{
    public string Group { get; set; } = string.Empty;
    public string Teacher { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public bool IsExternal { get; set; }
}
