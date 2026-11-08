// MIT. Source-vendored racing-row selector: which displays a triple-screen game should span, from the observed monitor
// rectangles and the primary display alone. Pure geometry: no Windows, engine, device or display-setting calls, and it
// never changes the topology. Enumeration lives in WindowsMonitors.cs; consumers pin this file's toolkit commit.
// Runs on Unity's legacy Mono 2.0 profile (.NET 3.5) as well as netstandard2.0 and later. It uses no LINQ, so it has no
// Func, which a .NET 4.x compile would bind to mscorlib, where Mono 2.0 lacks it. It also avoids IReadOnlyList,
// Array.Empty and the generic string.Join (tests/RacingRow/Compat35).
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dbce.Wheel.Display
{
    // Immutable through readonly fields. Neither a `readonly struct` nor auto-properties: a .NET 4.7.2+ compile marks
    // both with mscorlib's IsReadOnlyAttribute, which Mono 2.0 lacks.
    /// <summary>One display's desktop rectangle (virtual-desktop pixels) and whether Windows calls it primary.</summary>
    public struct MonitorRect : IEquatable<MonitorRect>
    {
        private readonly int _x, _y, _width, _height;
        private readonly bool _primary;
        public MonitorRect(int x, int y, int width, int height, bool primary)
        {
            _x = x; _y = y; _width = width; _height = height; _primary = primary;
        }
        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public bool Primary { get { return _primary; } }
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public override string ToString() => Width + "x" + Height + "@" + X + "," + Y + (Primary ? " primary" : "");
        public bool Equals(MonitorRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height && Primary == other.Primary;
        public override bool Equals(object? obj) => obj is MonitorRect other && Equals(other);
        public override int GetHashCode() => unchecked((((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height) * 2 + (Primary ? 1 : 0));
    }

    public enum RacingRowKind
    {
        /// <summary>No racing row: the game keeps its own single-screen behavior; nothing is spanned.</summary>
        None,
        /// <summary>The primary display is at least <see cref="RacingRowPolicy.SurroundAspect"/>:1 wide, so it is used as one
        /// wide surface: the mods' existing wide-primary path. A shape test only, not proof of NVIDIA Surround or AMD
        /// Eyefinity grouping.</summary>
        Surround,
        /// <summary>The primary and its equal-size left and right neighbors: three separate displays to span.</summary>
        Separate,
    }

    /// <summary>The selected row: its rectangle, the displays in it (left to right) and why, for logs and fixtures.</summary>
    public sealed class RacingRow
    {
        internal RacingRow(RacingRowKind kind, int x, int y, int width, int height, MonitorRect[] displays, string reason)
        {
            Kind = kind; X = x; Y = y; Width = width; Height = height; Displays = new ReadOnlyCollection<MonitorRect>(displays); Reason = reason;
        }
        public RacingRowKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>Left to right. A ReadOnlyCollection, which is also an IReadOnlyList on .NET 4.5 and later.</summary>
        public ReadOnlyCollection<MonitorRect> Displays { get; }
        /// <summary>One plain sentence: what was selected, or why there is no row.</summary>
        public string Reason { get; }
        public override string ToString() => Kind == RacingRowKind.None ? "no racing row: " + Reason : Kind + " " + Width + "x" + Height + "@" + X + "," + Y;
    }

    /// <summary>
    /// The automatic racing-row policy (owner, 2026-10-13: three 2560x1440 displays at -2560/0/2560 plus a 1920x1080
    /// diagnostic display above). The primary display is the center of the row:
    /// <list type="bullet">
    /// <item>A primary at least <see cref="SurroundAspect"/>:1 wide takes the existing wide-primary path (Surround in the
    /// mods' wording; a shape test, not proof of driver grouping): the row is the primary alone, and other displays
    /// are ignored, never appended to it.</item>
    /// <item>Otherwise the row is the primary plus exactly one display touching each side with the same size, top and
    /// bottom. Displays wholly above or below the row are ignored.</item>
    /// <item>Anything else refuses auto-span (Kind None) and never guesses: a missing side, a gap, a vertical offset
    /// or a different size on a side, two candidates for a side, a display overlapping the row, a fourth display in
    /// the row's band, or not exactly one primary.</item>
    /// </list>
    /// It never picks another triple or spans more than three displays.
    /// </summary>
    public static class RacingRowPolicy
    {
        /// <summary>A primary this wide or wider takes the wide-primary path (the mods' existing 2.9:1 rule; 7680x1440 is 5.3:1).</summary>
        public const double SurroundAspect = 2.9;

        public static RacingRow Select(IEnumerable<MonitorRect> monitors)
        {
            if (monitors == null) throw new ArgumentNullException(nameof(monitors));
            var all = new List<MonitorRect>(monitors);
            var primaries = new List<MonitorRect>();
            bool empty = false;
            foreach (var m in all)
            {
                if (m.Primary) primaries.Add(m);
                if (m.Width <= 0 || m.Height <= 0) empty = true;
            }
            if (empty) return None("a display reports an empty rectangle");
            if (primaries.Count != 1) return None(primaries.Count == 0 ? "no primary display" : "more than one primary display");
            var p = primaries[0];
            if (p.Width >= p.Height * SurroundAspect)
                return new RacingRow(RacingRowKind.Surround, p.X, p.Y, p.Width, p.Height, new[] { p }, "the primary display is at least 2.9:1 wide, used as one surface (" + p + ")");

            // Displays that share any of the primary's rows of pixels: only these can be left or right of it. A display
            // that merely touches the row's top or bottom edge (the diagnostic display above) is outside the band.
            var band = new List<MonitorRect>();
            foreach (var m in all) if (!m.Primary && m.Y < p.Bottom && m.Bottom > p.Y) band.Add(m);
            if (band.Count == 0) return None("a single display row: no display beside the primary");
            var left = new List<MonitorRect>();
            var right = new List<MonitorRect>();
            foreach (var m in band)
            {
                if (m.Right == p.X && Same(m, p)) left.Add(m);
                if (m.X == p.Right && Same(m, p)) right.Add(m);
            }
            if (left.Count > 1 || right.Count > 1) return None("two displays claim the same side of the primary");
            if (left.Count == 0 || right.Count == 0)
            {
                string side = left.Count == 0 && right.Count == 0 ? "either side" : left.Count == 0 ? "the left" : "the right";
                return None("no equal-size display directly on " + side + " of the primary (missing, gap, overlap, offset or different size)");
            }
            var row = new[] { left[0], p, right[0] };
            var extra = new List<MonitorRect>();
            foreach (var m in band) if (!m.Equals(left[0]) && !m.Equals(right[0])) extra.Add(m);
            if (extra.Count > 0)
                return None("another display shares the racing row's band (" + Join(extra) + "); the row is ambiguous, so nothing is spanned");
            int x = left[0].X, width = right[0].Right - left[0].X;
            return new RacingRow(RacingRowKind.Separate, x, p.Y, width, p.Height, row,
                "three equal displays with the primary in the center (" + Join(row) + ")");
        }

        private static bool Same(MonitorRect a, MonitorRect b) => a.Width == b.Width && a.Height == b.Height && a.Y == b.Y;
        private static RacingRow None(string reason) => new RacingRow(RacingRowKind.None, 0, 0, 0, 0, new MonitorRect[0], reason);
        // string.Join(string, string[]) exists on .NET 3.5; the generic overload does not.
        private static string Join(IList<MonitorRect> displays)
        {
            var text = new string[displays.Count];
            for (int i = 0; i < text.Length; i++) text[i] = displays[i].ToString();
            return string.Join(", ", text);
        }
    }
}
