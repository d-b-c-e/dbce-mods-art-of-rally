using System;
using System.Runtime.InteropServices;
using Dbce.Wheel.Display;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

// One borderless game window across a row of separate monitors, as Assetto Corsa/CSP does (owner 2026-11-01): the span
// mode's three views then draw into it exactly as under Surround, with no side windows. Unity's fullscreen cannot cover
// more than one monitor, so the window is windowed at the row's size, its border removed and placed at the row's origin.
internal static class SpanWindow
{
    private static float _nextResize;
    private static string _placed;

    /// <summary>True while the span window is in use (the home-display landing must not move it).</summary>
    internal static bool Engaged { get; private set; }

    /// <summary>Whether the span needs this window: no single display (Surround) is that wide.</summary>
    internal static bool Needed(int width) => Display.main.systemWidth < width;

    internal static void Release() { Engaged = false; _placed = null; }

    /// <summary>Null when the window covers the row at <paramref name="width"/> x <paramref name="height"/>; else why not yet.</summary>
    internal static string Ensure(int width, int height)
    {
        var row = RacingRowPolicy.Select(WindowsMonitors.Read());
        if (row.Kind != RacingRowKind.Separate || row.Width != width || row.Height != height)
        {
            Release();
            return "No row of separate monitors " + width + "x" + height + " (" + row + ").";
        }
        Engaged = true;
        if (Screen.fullScreenMode != FullScreenMode.Windowed)
        {
            if (Time.realtimeSinceStartup >= _nextResize)
            {
                _nextResize = Time.realtimeSinceStartup + 2f;
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                ModLog.Info("Span window: switching to a " + width + "x" + height + " window across " + row + ".");
            }
            return "Switching to a window across the monitors.";
        }
        var window = SecondaryDisplayWindows.LandingWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return "Waiting for the game window.";
        var style = GetWindowLongPtr(window, GwlStyle).ToInt64();
        var wanted = (style & ~(WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox)) | WsPopup;
        if (style != wanted || rect.Left != row.X || rect.Top != row.Y || rect.Right - rect.Left != width || rect.Bottom - rect.Top != height)
        {
            if (style != wanted) SetWindowLongPtr(window, GwlStyle, new IntPtr(wanted));
            SetWindowPos(window, IntPtr.Zero, row.X, row.Y, width, height, SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
            var placed = row.ToString();
            if (_placed != placed) { _placed = placed; ModLog.Info("Span window: borderless " + width + "x" + height + " at " + row.X + "," + row.Y + "."); }
        }
        return Screen.width == width && Screen.height == height ? null
            : "Waiting for the game to take the " + width + "x" + height + " window (now " + Screen.width + "x" + Screen.height + ").";
    }

    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000, WsThickFrame = 0x00040000, WsSysMenu = 0x00080000, WsMinimizeBox = 0x00020000,
        WsMaximizeBox = 0x00010000, WsPopup = 0x80000000;
    private const uint SwpNoZOrder = 0x4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20, SwpShowWindow = 0x40;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
