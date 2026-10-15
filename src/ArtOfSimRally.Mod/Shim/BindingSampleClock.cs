using System;
using System.Runtime.InteropServices;

namespace ArtOfSimRally.Mod.Shim
{
    // Same system-QPC origin and integer microsecond conversion as dbce_shim.cpp bridge_clock.
    // Clock observation only: no device read, force API, input injection or DLL shim call.
    internal static class BindingSampleClock
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceFrequency(out long value);
        internal static long Microseconds()
        {
            if (!QueryPerformanceFrequency(out var frequency) || frequency <= 0 || !QueryPerformanceCounter(out var ticks) || ticks < 0) return -1;
            try { return checked(ticks / frequency * 1000000 + ticks % frequency * 1000000 / frequency); }
            catch (OverflowException) { return -1; }
        }
    }
}
