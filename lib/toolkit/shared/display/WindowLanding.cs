// MIT. Source-vendored window landing for RacingRow.cs: where a game's window belongs on the racing row, whether to move
// it now, and which engine display index to save so the next launch opens there. Pure: no Windows, engine, registry,
// window or display-setting calls; the consumer moves the window and writes the engine's own preference.
// Runs on Unity's legacy Mono 2.0 profile (.NET 3.5) like RacingRow.cs: no LINQ, Func, IReadOnlyList, ValueTuple or
// readonly struct (tests/RacingRow/Compat35).
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Dbce.Wheel.Display
{
    /// <summary>One display in the engine's own list (Unity: <c>Display.displays[i].systemWidth/systemHeight</c>), in the
    /// engine's order. The engine reports no position, so displays are told apart by size only.</summary>
    public struct EngineDisplay
    {
        private readonly int _width, _height;
        public EngineDisplay(int width, int height) { _width = width; _height = height; }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public override string ToString() => Width + "x" + Height;
    }

    /// <summary>A landing decision: the target rectangle, whether to move now, and the start-monitor index to save.</summary>
    public sealed class WindowLanding
    {
        internal WindowLanding(bool move, int x, int y, int width, int height, int? engineIndex, int? persist, string reason, string persistReason)
        {
            Move = move; X = x; Y = y; Width = width; Height = height; EngineIndex = engineIndex; Persist = persist;
            Reason = reason; PersistReason = persistReason;
        }
        /// <summary>Move the window to <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, <see cref="Height"/> now.</summary>
        public bool Move { get; }
        /// <summary>The target: the row's span when spanning, else the home display. Zero with no racing row.</summary>
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>The home display's index in the engine's list, when exactly one engine display can be it.</summary>
        public int? EngineIndex { get; }
        /// <summary>The start-monitor index to save for the next launch, or null to leave the saved value alone.</summary>
        public int? Persist { get; }
        /// <summary>One sentence: why the window moves or stays.</summary>
        public string Reason { get; }
        /// <summary>One sentence: why the saved index changes or stays.</summary>
        public string PersistReason { get; }
    }

    /// <summary>
    /// "The game window lands on the racing row." Home is the primary display: the whole row under Surround, the center
    /// display of a separate-monitor row. A spanning consumer's target is the row's rectangle instead.
    /// <list type="bullet">
    /// <item>With no racing row, nothing moves and nothing is saved (one display, or a layout RacingRowPolicy refuses).</item>
    /// <item>The target is never any display but the home display (or the row's span). An auxiliary or dashboard display
    /// above or below the row is never chosen.</item>
    /// <item>A window that is on no single display (minimized, moving, between displays) is not moved.</item>
    /// <item>The saved index is changed only when the mapping is certain: the engine lists as many displays as Windows,
    /// exactly one engine display has the home size, and the saved index names a display of the size the game opened on
    /// (which proves the saved index and the engine list share one order). It is never written when already right.</item>
    /// </list>
    /// </summary>
    public static class WindowLandingPolicy
    {
        /// <summary>The home display of a row, or null with no row.</summary>
        public static MonitorRect? Home(RacingRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.Kind == RacingRowKind.Surround) return row.Displays[0];
            if (row.Kind == RacingRowKind.Separate) return row.Displays[1];
            return null;
        }

        /// <summary>The one display holding the rectangle's center, or null.</summary>
        public static MonitorRect? DisplayOf(IList<MonitorRect> monitors, int x, int y, int width, int height)
        {
            if (monitors == null) throw new ArgumentNullException(nameof(monitors));
            int cx = x + width / 2, cy = y + height / 2, count = 0;
            MonitorRect found = default(MonitorRect);
            foreach (var m in monitors)
                if (cx >= m.X && cx < m.Right && cy >= m.Y && cy < m.Bottom) { found = m; count++; }
            return count == 1 ? found : (MonitorRect?)null;
        }

        /// <param name="monitors">The displays Windows reports.</param>
        /// <param name="row">The consumer's racing row for those displays (RacingRowPolicy.Select).</param>
        /// <param name="spanning">True when the consumer spans a Separate row (its span setting and triple gate allow it).</param>
        /// <param name="windowX">The game window's rectangle now (GetWindowRect).</param>
        /// <param name="openedOn">The display the game window opened on this launch, before any move; null when unknown.</param>
        /// <param name="engine">The engine's display list in its own order (Unity: Display.displays), or null when unavailable.</param>
        /// <param name="savedIndex">The engine's saved start-monitor index (Unity: PlayerPrefs UnitySelectMonitor), or null
        /// when none is saved; the engine then starts on index 0.</param>
        public static WindowLanding Decide(IList<MonitorRect> monitors, RacingRow row, bool spanning,
            int windowX, int windowY, int windowWidth, int windowHeight, MonitorRect? openedOn, IList<EngineDisplay>? engine, int? savedIndex)
        {
            if (monitors == null) throw new ArgumentNullException(nameof(monitors));
            if (row == null) throw new ArgumentNullException(nameof(row));
            var home = Home(row);
            string window = windowWidth + "x" + windowHeight + "@" + windowX + "," + windowY;
            if (home == null)
                return new WindowLanding(false, 0, 0, 0, 0, null, null, "no racing row (" + row.Reason + "); the game keeps its window", "no racing row; the saved index is left alone");

            bool span = row.Kind == RacingRowKind.Separate && spanning, move;
            int x = span ? row.X : home.Value.X, y = span ? row.Y : home.Value.Y;
            int width = span ? row.Width : home.Value.Width, height = span ? row.Height : home.Value.Height;
            string target = width + "x" + height + "@" + x + "," + y, reason;
            var on = DisplayOf(monitors, windowX, windowY, windowWidth, windowHeight);
            if (on == null) { move = false; reason = "the game window " + window + " is on no single display (minimized or moving); left as is"; }
            else if (span)
            {
                move = windowX != x || windowY != y || windowWidth != width || windowHeight != height;
                reason = move ? "the game window " + window + " is moved to the racing row's span " + target : "the game window spans the racing row (" + target + ")";
            }
            else
            {
                move = !on.Value.Equals(home.Value);
                reason = move ? "the game window " + window + " is on " + on.Value + ", outside the racing row; moving it to " + home.Value
                    : "the game window is on the racing row (" + home.Value + ")";
            }

            int? index = null, persist = null;
            string persistReason;
            if (engine == null) persistReason = "the engine's display list is unavailable; the saved index is left alone";
            else
            {
                string list = Describe(engine);
                int matches = 0, match = -1;
                for (int i = 0; i < engine.Count; i++)
                    if (engine[i].Width == home.Value.Width && engine[i].Height == home.Value.Height) { match = i; matches++; }
                if (matches == 1 && engine.Count == monitors.Count) index = match;
                if (engine.Count != monitors.Count) persistReason = "the engine lists " + engine.Count + " displays (" + list + ") and Windows " + monitors.Count + "; not mapped";
                else if (matches != 1)
                {
                    persistReason = (matches == 0 ? "no engine display is " : matches + " engine displays are ") + home.Value.Width + "x" + home.Value.Height + " (" + list + "); ambiguous, not saved";
                }
                else if (openedOn == null) persistReason = "the display the game opened on is unknown (" + list + "); not saved";
                else
                {
                    int current = savedIndex ?? 0;
                    var start = openedOn.Value;
                    if (current < 0 || current >= engine.Count || engine[current].Width != start.Width || engine[current].Height != start.Height)
                        persistReason = "the saved index " + (savedIndex.HasValue ? savedIndex.Value.ToString() : "unset") + " does not name the " + start.Width + "x" + start.Height +
                            " display the game opened on (" + list + "); the engine's order is not proven, not saved";
                    else if (match == current) persistReason = "the saved index " + current + " is already the racing display (" + list + ")";
                    else { persist = match; persistReason = "engine display " + match + " is the racing display " + home.Value + " (" + list + ")"; }
                }
            }
            return new WindowLanding(move, x, y, width, height, index, persist, reason, persistReason);
        }

        private static string Describe(IList<EngineDisplay> engine)
        {
            var text = new StringBuilder();
            for (int i = 0; i < engine.Count; i++) text.Append(i == 0 ? "" : ", ").Append(i).Append(": ").Append(engine[i]);
            return text.ToString();
        }
    }
}
