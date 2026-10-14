using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

// Read-only evidence for how Unity's display indices relate to Windows' displays (sana 80507/80723). Unity 2019.4 gives
// no positions and documents only that index 0 is the primary. Three candidate identity sources are recorded side by
// side, and nothing is decided from them: independent displays still use the saved indices.
//   1. nativeDisplay: Unity's private per-display handle, kept only if GetMonitorInfo accepts it as an HMONITOR.
//   2. RelativeMouseAt: Unity's own point-to-display query, asked about each Windows display's center in two explicit
//      coordinate conventions. Only numbers are passed: no cursor, input or focus change.
//   3. X order: the hypothesis "primary first, then left to right by desktop X" (it fits the rc.20 and rc.21 Unity lists
//      and experiment 008's left=1/right=2). A hypothesis, never a mapping.
// This file is pure; UnityDisplayIdentity reads Unity and Windows.
internal static class DisplayIdentityEvidence
{
    internal const string Scope =
        "diagnostic only: no display is activated, selected or saved from this evidence; independent displays use the saved indices";
    internal const string DesktopConvention = "desktop: Windows virtual-desktop pixels, origin at the primary's top-left, y down";
    internal const string BottomUpConvention = "primaryBottomUp: pixels from the primary's bottom-left, y up (Unity screen convention)";

    internal sealed class Rect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Primary { get; set; }
        public override string ToString() => $"{X},{Y} {Width}x{Height}{(Primary ? " primary" : "")}";
    }

    internal sealed class UnityDisplay
    {
        public int Index { get; set; }
        public int SystemWidth { get; set; }
        public int SystemHeight { get; set; }
        /// <summary>Unity's private nativeDisplay value, hex; null if the field is unavailable.</summary>
        public string NativeDisplay { get; set; }
        /// <summary>The display GetMonitorInfo reports for nativeDisplay; null unless it is a valid HMONITOR.</summary>
        public Rect Hmonitor { get; set; }
        public string HmonitorDevice { get; set; }
        /// <summary>The Windows display the X-order hypothesis assigns to this index; null if it has none.</summary>
        public Rect XOrderPrediction { get; set; }
        /// <summary>Whether that prediction has this Unity display's size (necessary, not sufficient).</summary>
        public bool XOrderSizeAgrees { get; set; }
    }

    internal sealed class Probe
    {
        public Rect Target { get; set; }
        public string Convention { get; set; }
        public int InputX { get; set; }
        public int InputY { get; set; }
        public float ResultX { get; set; }
        public float ResultY { get; set; }
        /// <summary>RelativeMouseAt's z: the Unity display index it reports for the input point.</summary>
        public int ResultIndex { get; set; }
        public string Error { get; set; }
    }

    internal sealed class Snapshot
    {
        public string Scope { get; set; } = DisplayIdentityEvidence.Scope;
        public UnityDisplay[] Displays { get; set; } = Array.Empty<UnityDisplay>();
        public Rect[] WindowsDisplays { get; set; } = Array.Empty<Rect>();
        public Probe[] Probes { get; set; } = Array.Empty<Probe>();
        public string[] Conventions { get; set; } = { DesktopConvention, BottomUpConvention };
        public string Error { get; set; }
        /// <summary>One log line, compared between samples so it is written only when the evidence changes.</summary>
        public string Summary { get; set; } = "";
    }

    /// <summary>The X-order hypothesis: the single primary first, then the others by desktop X, then Y. Empty without
    /// exactly one primary.</summary>
    internal static IReadOnlyList<MonitorRect> PredictXOrder(IReadOnlyList<MonitorRect> monitors)
    {
        var primaries = monitors.Where(m => m.Primary).ToArray();
        if (primaries.Length != 1) return Array.Empty<MonitorRect>();
        return new[] { primaries[0] }.Concat(monitors.Where(m => !m.Primary).OrderBy(m => m.X).ThenBy(m => m.Y)).ToArray();
    }

    /// <summary>A display's center in the desktop convention.</summary>
    internal static (int X, int Y) DesktopCenter(MonitorRect m) => (m.X + m.Width / 2, m.Y + m.Height / 2);

    /// <summary>A display's center measured from the primary's bottom-left corner, y up.</summary>
    internal static (int X, int Y) PrimaryBottomUpCenter(MonitorRect m, MonitorRect primary)
    {
        var (x, y) = DesktopCenter(m);
        return (x - primary.X, primary.Y + primary.Height - y);
    }

    internal static Rect ToRect(MonitorRect m) => new() { X = m.X, Y = m.Y, Width = m.Width, Height = m.Height, Primary = m.Primary };

    /// <summary>Pure: the summary line for a finished snapshot.</summary>
    internal static string Summarize(Snapshot snapshot)
    {
        if (snapshot.Error != null) return "identity read failed: " + snapshot.Error;
        var native = snapshot.Displays.Count(d => d.Hmonitor != null);
        var parts = new List<string>
        {
            $"nativeDisplay is an HMONITOR for {native}/{snapshot.Displays.Length}" +
                (native == 0 ? "" : " [" + string.Join("; ", snapshot.Displays.Where(d => d.Hmonitor != null).Select(d => $"{d.Index}={d.Hmonitor}")) + "]"),
        };
        foreach (var group in snapshot.Probes.GroupBy(p => p.Convention.Split(':')[0]))
            parts.Add("RelativeMouseAt " + group.Key + " [" +
                string.Join("; ", group.Select(p => $"{p.Target.X},{p.Target.Y}->{(p.Error != null ? "error" : p.ResultIndex.ToString())}")) + "]");
        parts.Add("X-order hypothesis [" + string.Join("; ", snapshot.Displays.Select(d =>
            $"{d.Index}={(d.XOrderPrediction == null ? "none" : d.XOrderPrediction.X + "," + d.XOrderPrediction.Y)}{(d.XOrderSizeAgrees ? "" : " size differs")}")) + "]");
        return string.Join(" | ", parts);
    }
}
