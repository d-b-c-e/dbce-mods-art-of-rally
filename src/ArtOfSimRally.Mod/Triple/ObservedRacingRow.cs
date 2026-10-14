using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

// Read-only diagnostics: the racing row the toolkit's shared selector (lib/display) sees in Windows' displays, written
// beside Unity's own display list. Evidence only. Independent displays keep using the saved Unity indices
// (LeftDisplayIndex/RightDisplayIndex); nothing here maps a Unity display index to a Windows display, chooses an index
// or refuses one, because Unity's display order is not a reliable link to these rectangles.
internal static class ObservedRacingRow
{
    internal const string IndexCorrelation =
        "unresolved: the saved Unity indices (LeftDisplayIndex/RightDisplayIndex) stay authoritative; Unity's display " +
        "order is not mapped to these Windows displays";

    internal sealed class DisplayRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Primary { get; set; }
    }

    internal sealed class Snapshot
    {
        public string Kind { get; set; } = "None";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Reason { get; set; } = "";
        /// <summary>Left to right; empty when there is no row.</summary>
        public DisplayRect[] RowDisplays { get; set; } = Array.Empty<DisplayRect>();
        /// <summary>Displays outside the row (a dashboard, for example); empty when there is no row.</summary>
        public DisplayRect[] AuxiliaryDisplays { get; set; } = Array.Empty<DisplayRect>();
        public DisplayRect[] WindowsDisplays { get; set; } = Array.Empty<DisplayRect>();
        public string Error { get; set; }
        public string IndexCorrelation { get; set; } = ObservedRacingRow.IndexCorrelation;

        /// <summary>One line for the log, compared between samples so it is written only when the row changes.</summary>
        public string Summary => Error != null ? "display read failed: " + Error
            : Kind == "None" ? "no racing row (" + Reason + ")"
            : $"{Kind} {Width}x{Height} at {X},{Y}; {AuxiliaryDisplays.Length} other display(s)";
    }

    /// <summary>Pure: the snapshot for these displays.</summary>
    internal static Snapshot Describe(IReadOnlyList<MonitorRect> monitors)
    {
        var row = RacingRowPolicy.Select(monitors);
        var snapshot = new Snapshot
        {
            Kind = row.Kind.ToString(), X = row.X, Y = row.Y, Width = row.Width, Height = row.Height, Reason = row.Reason,
            RowDisplays = row.Displays.Select(ToDisplay).ToArray(),
            WindowsDisplays = monitors.Select(ToDisplay).ToArray(),
        };
        if (row.Kind != RacingRowKind.None)
            snapshot.AuxiliaryDisplays = monitors.Where(m => !row.Displays.Contains(m)).Select(ToDisplay).ToArray();
        return snapshot;
    }

    /// <summary>Reads Windows' displays now. Never throws: a failed read is reported in the snapshot.</summary>
    internal static Snapshot Observe()
    {
        try { return Describe(WindowsMonitors.Read()); }
        catch (Exception exception) { return new Snapshot { Reason = "display read failed", Error = exception.GetType().Name + ": " + exception.Message }; }
    }

    private static DisplayRect ToDisplay(MonitorRect m) => new() { X = m.X, Y = m.Y, Width = m.Width, Height = m.Height, Primary = m.Primary };
}
