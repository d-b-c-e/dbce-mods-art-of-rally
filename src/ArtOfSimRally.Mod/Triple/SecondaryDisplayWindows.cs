using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>Reads this process's Unity secondary display windows and the monitor each one fills, for
/// SideActivationWitness. Read-only: no window is moved, raised or focused.</summary>
internal static class SecondaryDisplayWindows
{
    internal static IReadOnlyList<SideActivationWitness.SecondaryWindow> Read()
    {
        var own = (uint)Process.GetCurrentProcess().Id;
        var list = new List<SideActivationWitness.SecondaryWindow>();
        EnumWindowsProc callback = (window, _) =>
        {
            GetWindowThreadProcessId(window, out var process);
            if (process != own) return true;
            var title = new StringBuilder(64);
            GetWindowText(window, title, title.Capacity);
            if (!string.Equals(title.ToString(), SideWindowFocusRestorer.SecondaryWindowTitle, StringComparison.Ordinal)) return true;
            MonitorRect? filled = null;
            var detail = "window rectangle unavailable";
            if (GetWindowRect(window, out var rect))
            {
                detail = $"window {rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top}";
                var monitor = MonitorFromWindow(window, MonitorDefaultToNull);
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                // Placed only once the window covers exactly the monitor Windows assigns it to.
                if (monitor != IntPtr.Zero && GetMonitorInfoW(monitor, ref info) &&
                    info.Monitor.Left == rect.Left && info.Monitor.Top == rect.Top &&
                    info.Monitor.Right == rect.Right && info.Monitor.Bottom == rect.Bottom)
                    filled = new MonitorRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, (info.Flags & MonitorInfoPrimary) != 0);
            }
            list.Add(new SideActivationWitness.SecondaryWindow(window.ToInt64(), filled, detail));
            return true;
        };
        if (!EnumWindows(callback, IntPtr.Zero)) throw new InvalidOperationException("EnumWindows failed");
        GC.KeepAlive(callback);
        return list;
    }

    private const uint MonitorDefaultToNull = 0; // MONITOR_DEFAULTTONULL
    private const uint MonitorInfoPrimary = 1;   // MONITORINFOF_PRIMARY
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
}
