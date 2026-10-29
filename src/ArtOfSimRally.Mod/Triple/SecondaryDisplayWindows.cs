using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>Reads this process's Unity windows for SideActivationWitness: the main window's picture (client area) and
/// each secondary display window with the monitor it fills. Read-only: no window is moved, raised or focused, and
/// titles are read with InternalGetWindowText, which sends no message to the window.</summary>
internal static class SecondaryDisplayWindows
{
    /// <summary>The class Unity gives its player windows.</summary>
    internal const string UnityWindowClass = "UnityWndClass";
    private static readonly uint OwnProcess = CurrentProcessId();

    internal static IReadOnlyList<SideActivationWitness.SecondaryWindow> Read()
    {
        var list = new List<SideActivationWitness.SecondaryWindow>();
        Enumerate(window =>
        {
            if (!string.Equals(Title(window), SideWindowFocusRestorer.SecondaryWindowTitle, StringComparison.Ordinal)) return;
            MonitorRect? filled = null;
            var visible = IsWindowVisible(window);
            var detail = $"visible={visible}, window rectangle unavailable";
            if (GetWindowRect(window, out var rect))
            {
                detail = $"visible={visible}, window {rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top}";
                var monitor = MonitorFromWindow(window, MonitorDefaultToNull);
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (monitor != IntPtr.Zero && GetMonitorInfoW(monitor, ref info))
                {
                    detail += $", nearest monitor {info.Monitor.Left},{info.Monitor.Top} {info.Monitor.Right - info.Monitor.Left}x{info.Monitor.Bottom - info.Monitor.Top}";
                    // Placed only once the window covers exactly the monitor Windows assigns it to.
                    if (info.Monitor.Left == rect.Left && info.Monitor.Top == rect.Top &&
                        info.Monitor.Right == rect.Right && info.Monitor.Bottom == rect.Bottom)
                        filled = new MonitorRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, (info.Flags & MonitorInfoPrimary) != 0);
                }
            }
            list.Add(new SideActivationWitness.SecondaryWindow(window.ToInt64(), filled, detail));
        });
        return list;
    }

    /// <summary>This process's visible Unity main window(s) (class UnityWndClass, not a secondary display window) and,
    /// if there is exactly one, where its client area is on the desktop.</summary>
    internal static SideActivationWitness.MainWindow ReadMain()
    {
        var found = new List<IntPtr>();
        var seen = new List<string>();
        Enumerate(window =>
        {
            if (!IsWindowVisible(window)) return;
            var title = Title(window);
            var cls = ClassName(window);
            seen.Add($"{cls} '{title}'");
            if (string.Equals(cls, UnityWindowClass, StringComparison.Ordinal) &&
                !string.Equals(title, SideWindowFocusRestorer.SecondaryWindowTitle, StringComparison.Ordinal))
                found.Add(window);
        });
        var detail = "visible windows: " + (seen.Count == 0 ? "none" : string.Join("; ", seen));
        if (found.Count != 1 || !GetClientRect(found[0], out var client)) return new SideActivationWitness.MainWindow(found.Count, 0, 0, 0, 0, detail);
        var origin = new NativePoint { X = client.Left, Y = client.Top };
        if (!ClientToScreen(found[0], ref origin)) return new SideActivationWitness.MainWindow(0, 0, 0, 0, 0, detail + "; client position unavailable");
        return new SideActivationWitness.MainWindow(1, origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top, detail);
    }

    internal static IntPtr LandingWindow()
    {
        var found = new List<IntPtr>();
        Enumerate(window =>
        {
            if (IsWindowVisible(window) && !IsIconic(window) &&
                ClassName(window) == UnityWindowClass && Title(window) != SideWindowFocusRestorer.SecondaryWindowTitle)
                found.Add(window);
        });
        return found.Count == 1 ? found[0] : IntPtr.Zero;
    }

    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);

    private static void Enumerate(Action<IntPtr> visit)
    {
        EnumWindowsProc callback = (window, _) =>
        {
            GetWindowThreadProcessId(window, out var process);
            if (process == OwnProcess) visit(window);
            return true;
        };
        if (!EnumWindows(callback, IntPtr.Zero)) throw new InvalidOperationException("EnumWindows failed");
        GC.KeepAlive(callback);
    }

    private static string Title(IntPtr window)
    {
        var text = new StringBuilder(128);
        InternalGetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private static string ClassName(IntPtr window)
    {
        var text = new StringBuilder(128);
        GetClassNameW(window, text, text.Capacity);
        return text.ToString();
    }

    private static uint CurrentProcessId()
    {
        using var process = Process.GetCurrentProcess();
        return (uint)process.Id;
    }

    private const uint MonitorDefaultToNull = 0; // MONITOR_DEFAULTTONULL
    private const uint MonitorInfoPrimary = 1;   // MONITORINFOF_PRIMARY
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int InternalGetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
}
