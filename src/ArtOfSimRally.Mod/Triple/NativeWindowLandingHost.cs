using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dbce.Wheel.Display;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

internal sealed class NativeWindowLandingHost : RacingWindowLanding.IHost
{
    public float Now => Time.realtimeSinceStartup;
    public IReadOnlyList<MonitorRect> Monitors => WindowsMonitors.Read();
    public void Resize(int width, int height) => Screen.SetResolution(width, height, FullScreenMode.Windowed);
    public void Log(string message) => ModLog.Info(message);
    public bool Observe(out RacingWindowLanding.Observation observation)
    {
        observation = default;
        var window = SecondaryDisplayWindows.LandingWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var outer) ||
            !GetClientRect(window, out var client)) return false;
        var origin = new Point { X = client.Left, Y = client.Top };
        if (!ClientToScreen(window, ref origin)) return false;
        // All native coordinates are sampled in the calling Unity thread's DPI context.
        // Translate the observed nonclient offset; never change global DPI awareness.
        observation = new RacingWindowLanding.Observation {
            Window = window.ToInt64(), X = origin.X, Y = origin.Y,
            Width = client.Right - client.Left, Height = client.Bottom - client.Top,
            OffsetX = origin.X - outer.Left, OffsetY = origin.Y - outer.Top,
            EngineWidth = Screen.width, EngineHeight = Screen.height,
            Windowed = Screen.fullScreenMode == FullScreenMode.Windowed
        };
        return true;
    }
    public bool Place(long window, int x, int y) => SetWindowPos(new IntPtr(window), IntPtr.Zero, x, y, 0, 0, 0x0015);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);
}
