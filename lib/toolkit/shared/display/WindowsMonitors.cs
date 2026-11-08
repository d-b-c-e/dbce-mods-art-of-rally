// MIT. Source-vendored Windows monitor reader for RacingRow.cs: each display's desktop rectangle and the primary flag,
// read with EnumDisplayMonitors + GetMonitorInfoW. Read-only: it changes no display setting, profile or window.
// Coordinates are those of the calling process's DPI context, as with the GetSystemMetrics(SM_*VIRTUALSCREEN) reads
// the mods used before. Kept apart from the pure policy so fixtures and non-Windows hosts never need it.
// Runs on Unity's legacy Mono 2.0 profile (.NET 3.5) as well as netstandard2.0 and later (see RacingRow.cs).
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Dbce.Wheel.Display
{
    public static class WindowsMonitors
    {
        /// <summary>The displays Windows reports now, in its enumeration order. Throws if Windows refuses the query. A
        /// ReadOnlyCollection, which is also an IReadOnlyList on .NET 4.5 and later.</summary>
        public static ReadOnlyCollection<MonitorRect> Read()
        {
            var list = new List<MonitorRect>();
            string? failure = null;
            MonitorEnumProc proc = (IntPtr monitor, IntPtr hdc, IntPtr clip, IntPtr data) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfoW(monitor, ref info)) { failure = "GetMonitorInfo failed"; return false; }
                var r = info.Monitor;
                list.Add(new MonitorRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, (info.Flags & MonitorInfoPrimary) != 0));
                return true;
            };
            bool enumerated = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
            GC.KeepAlive(proc);
            if (!enumerated || failure != null) throw new InvalidOperationException(failure ?? "EnumDisplayMonitors failed");
            return list.AsReadOnly();
        }

        /// <summary>The racing row for the displays Windows reports now (see <see cref="RacingRowPolicy"/>).</summary>
        public static RacingRow SelectRacingRow() => RacingRowPolicy.Select(Read());

        private const uint MonitorInfoPrimary = 1; // MONITORINFOF_PRIMARY
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }
        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr clip, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    }
}
