using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dbce.Wheel.Display;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

// One windowed apply per observed topology. No preference writes: test/muted launch
// classification must be qualified before enabling STD-038's optional persistence.
internal sealed class RacingWindowLanding
{
    private string _appliedTopology;
    private IntPtr _pendingWindow;
    private int _x, _y;
    private float _placeAfter;

    internal void Update()
    {
        var monitors = new List<MonitorRect>(WindowsMonitors.Read());
        var topology = string.Join(";", monitors);
        if (_pendingWindow != IntPtr.Zero)
        {
            if (topology == _appliedTopology && Time.realtimeSinceStartup < _placeAfter) return;
            var pending = _pendingWindow;
            _pendingWindow = IntPtr.Zero;
            if (topology == _appliedTopology && SecondaryDisplayWindows.LandingWindow() == pending)
            {
                // Unity owns client size; move only, without focus or z-order changes.
                if (!SetWindowPos(pending, IntPtr.Zero, _x, _y, 0, 0, 0x0015))
                    ModLog.Info("Window landing: Windows refused placement; no repeated apply this episode.");
            }
        }
        if (_appliedTopology == topology) return;
        var window = SecondaryDisplayWindows.LandingWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return;
        var row = RacingRowPolicy.Select(monitors);
        var decision = WindowLandingPolicy.Decide(monitors, row, false,
            rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, null, null, null);
        // A minimized, moving or unavailable window is not an apply episode.
        if (WindowLandingPolicy.DisplayOf(monitors, rect.Left, rect.Top,
                rect.Right - rect.Left, rect.Bottom - rect.Top) == null) return;
        _appliedTopology = topology;
        ModLog.Info("Window landing: " + decision.Reason + "; start monitor unchanged (persistence deferred).");
        if (!decision.Move) return;
        // Separate-display rendering keeps one center window, never a desktop span.
        // Surround already appears as one wide primary to the shared selector.
        Screen.SetResolution(decision.Width, decision.Height, FullScreenMode.Windowed);
        _pendingWindow = window;
        _x = decision.X; _y = decision.Y;
        _placeAfter = Time.realtimeSinceStartup + .5f;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);
}
