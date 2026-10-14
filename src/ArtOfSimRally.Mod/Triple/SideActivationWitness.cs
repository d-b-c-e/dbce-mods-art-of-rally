using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

// Proves the predicted side indices on activation (sana 81386/81671). First the game's own main window must fill the
// row's center display exactly (Screen size alone does not show which monitor it is on). Until it does nothing is
// activated, and that refusal is not final, because nothing irreversible has happened. Unity cannot deactivate a
// display, so each side is then activated alone: the one new "Unity Secondary Display" window this process owns must
// fill that side's monitor before the other side is activated. Only after both are proven may side cameras render. A
// mismatch, a second new window, a failed activation or no window within the time limit fails for the rest of the
// process, without guessing another monitor. Pure: the caller passes the clock, the windows and the activation call.
internal sealed class SideActivationWitness
{
    internal const double DefaultTimeoutSeconds = 5;

    internal enum Phase { NotStarted, ProvingLeft, ProvingRight, Proven, Failed }

    /// <summary>This process's Unity main window(s): how many were found and where the client area of the single one is
    /// on the desktop (null unless exactly one was found).</summary>
    internal readonly struct MainWindow
    {
        internal MainWindow(int count, int x, int y, int width, int height, string detail = "")
        {
            Count = count; X = x; Y = y; Width = width; Height = height; Detail = detail ?? "";
        }
        internal int Count { get; }
        internal int X { get; }
        internal int Y { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal string Detail { get; }
        internal bool Fills(MonitorRect monitor) => Count == 1 && X == monitor.X && Y == monitor.Y && Width == monitor.Width && Height == monitor.Height;
    }

    /// <summary>One of this process's Unity secondary windows. Monitor is the display it fills exactly, or null while it
    /// does not yet fill the monitor Windows assigns it to (a new window may appear before Unity places it).</summary>
    internal readonly struct SecondaryWindow
    {
        internal SecondaryWindow(long handle, MonitorRect? monitor, string detail = "")
        {
            Handle = handle; Monitor = monitor; Detail = detail ?? "";
        }
        internal long Handle { get; }
        internal MonitorRect? Monitor { get; }
        /// <summary>Evidence: the window's rectangle as read.</summary>
        internal string Detail { get; }
    }

    private readonly int _leftIndex, _rightIndex;
    private readonly MonitorRect _left, _center, _right;
    private readonly double _timeoutSeconds;
    private readonly HashSet<long> _known = new();
    private readonly List<string> _observations = new();
    private double _phaseStarted;

    internal SideActivationWitness(int leftIndex, int rightIndex, MonitorRect left, MonitorRect center, MonitorRect right, double timeoutSeconds = DefaultTimeoutSeconds)
    {
        if (leftIndex < 1 || rightIndex < 1 || leftIndex == rightIndex) throw new ArgumentException("Two distinct secondary indices are required.");
        _leftIndex = leftIndex; _rightIndex = rightIndex; _left = left; _center = center; _right = right; _timeoutSeconds = timeoutSeconds;
    }

    internal Phase Current { get; private set; } = Phase.NotStarted;
    internal int LeftIndex => _leftIndex;
    internal int RightIndex => _rightIndex;
    internal string Code { get; private set; } = "SIDE_DISPLAYS_NOT_ACTIVATED";
    internal string Message { get; private set; } = "Side displays have not been activated.";
    internal IReadOnlyList<string> Observations => _observations;

    /// <summary>Advances the proof. <paramref name="activate"/> is called once per side, never after a failure.</summary>
    internal Phase Step(double nowSeconds, MainWindow main, IReadOnlyList<SecondaryWindow> windows, Action<int> activate)
    {
        switch (Current)
        {
            case Phase.NotStarted:
                // The center view is the main window: it must be the row's center display before any side is activated.
                // Not final: nothing has been activated, so the game may still be placed or made full screen.
                if (!main.Fills(_center))
                {
                    Code = main.Count == 1 ? "MAIN_WINDOW_NOT_ROW_CENTER" : "MAIN_WINDOW_UNKNOWN";
                    Message = main.Count == 1
                        ? $"The game window's picture is at {main.X},{main.Y} {main.Width}x{main.Height}, not filling the center racing display {_center}. Run the game full screen on the Windows primary; no side display is activated."
                        : $"{main.Count} game main windows found ({main.Detail}); the center display cannot be confirmed, so no side display is activated.";
                    return Current;
                }
                // Any secondary window before the first activation was not opened by this proof: it cannot be attributed.
                if (windows.Count != 0)
                    return Fail("SECONDARY_DISPLAY_STATE_UNKNOWN",
                        $"{windows.Count} Unity secondary window(s) already exist before activation, so neither side can be attributed. Restart the game.");
                Begin(Phase.ProvingLeft, nowSeconds, _leftIndex, activate);
                return Current;
            case Phase.ProvingLeft:
                return Prove(nowSeconds, windows, _leftIndex, _left, "left", () => Begin(Phase.ProvingRight, nowSeconds, _rightIndex, activate));
            case Phase.ProvingRight:
                return Prove(nowSeconds, windows, _rightIndex, _right, "right", () =>
                {
                    Current = Phase.Proven;
                    Code = "SIDE_DISPLAYS_PROVEN";
                    Message = $"Left = Unity {_leftIndex} on {_left}, right = Unity {_rightIndex} on {_right}, proven by their windows.";
                });
            default:
                return Current;
        }
    }

    private void Begin(Phase phase, double now, int index, Action<int> activate)
    {
        Current = phase;
        _phaseStarted = now;
        Code = "SIDE_DISPLAY_PROVING";
        Message = $"Activated Unity display {index}; waiting for its window.";
        try { activate(index); }
        catch (Exception exception)
        {
            // Activation may have taken effect before failing, so this is final like any other failure.
            Fail("SECONDARY_DISPLAY_ACTIVATION_FAILED",
                $"Activating Unity display {index} failed: {exception.GetType().Name}: {OneLine(exception.Message)}");
            return;
        }
        _observations.Add($"activated Unity {index} at {now:F2} s");
    }

    private static string OneLine(string text) => (text ?? "").Replace("\r", " ").Replace("\n", " ");

    private Phase Prove(double now, IReadOnlyList<SecondaryWindow> windows, int index, MonitorRect expected, string side, Action next)
    {
        var fresh = windows.Where(window => !_known.Contains(window.Handle)).ToArray();
        if (fresh.Length > 1)
        {
            Record(fresh, index, now);
            return Fail("SECONDARY_DISPLAY_IDENTITY_AMBIGUOUS",
                $"{fresh.Length} new Unity secondary windows appeared after activating Unity {index}; the {side} display cannot be attributed.");
        }
        if (fresh.Length == 1 && fresh[0].Monitor.HasValue)
        {
            var monitor = fresh[0].Monitor.Value;
            _observations.Add($"Unity {index} window 0x{fresh[0].Handle:X} ({fresh[0].Detail}) on {monitor} at {now:F2} s");
            if (!monitor.Equals(expected))
                return Fail("SECONDARY_DISPLAY_IDENTITY_MISMATCH",
                    $"Unity display {index} opened on {monitor}, not the {side} racing display {expected}. Side views stay off; restart the game after checking the displays.");
            _known.Add(fresh[0].Handle);
            next();
            return Current;
        }
        // A placed window is accepted even if it is first seen after the limit: the proof is exact, the limit only
        // bounds waiting.
        if (now - _phaseStarted > _timeoutSeconds)
        {
            Record(fresh, index, now);
            return Fail("SECONDARY_DISPLAY_WINDOW_TIMEOUT",
                $"No placed window for Unity display {index} within {_timeoutSeconds:F0} s; the {side} display is unproven.");
        }
        return Current;
    }

    // Evidence for a failure: where each new window actually is (unplaced, hidden or several).
    private void Record(IEnumerable<SecondaryWindow> fresh, int index, double now)
    {
        foreach (var window in fresh)
            _observations.Add($"after Unity {index}: window 0x{window.Handle:X} ({window.Detail}) {(window.Monitor.HasValue ? "fills " + window.Monitor.Value : "fills no monitor")} at {now:F2} s");
    }

    private Phase Fail(string code, string message)
    {
        Current = Phase.Failed;
        Code = code;
        Message = message;
        _observations.Add(code);
        return Current;
    }
}
