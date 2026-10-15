using System.Diagnostics;
using System.Globalization;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Opt-in per-window costs of the mod's known main-thread hot paths, for performance attribution (sana 121832/121875):
    /// physics steps, force-feedback device writes, triple-screen diagnostics samples and status writes.
    /// </summary>
    /// <remarks>
    /// Off unless <c>perf-hotpath.on</c> exists in the mod folder (a private test copy), so an ordinary install is
    /// untouched. When on, each timed call costs two Stopwatch timestamps. The totals are reported beside the frame-rate
    /// line for the same 10 s window (<see cref="FrameRate"/>). Main thread only. It changes no behavior, force or
    /// output.
    /// </remarks>
    internal static class HotPathCost
    {
        internal enum Kind { FfbWrite = 0, Diagnostics = 1, StatusWrite = 2 }

        private static readonly long[] Calls = new long[3], Ticks = new long[3], MaxTicks = new long[3];
        private static long _physicsSteps;

        internal static bool Enabled;

        internal static void PhysicsStep() { if (Enabled) _physicsSteps++; }

        internal static long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0;

        internal static void End(Kind kind, long start)
        {
            if (!Enabled || start == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - start;
            int i = (int)kind;
            Calls[i]++;
            Ticks[i] += elapsed;
            if (elapsed > MaxTicks[i]) MaxTicks[i] = elapsed;
        }

        /// <summary>The finished window's totals as one line, then a reset; null when off.</summary>
        internal static string TakeWindow()
        {
            if (!Enabled) return null;
            string line = "physics steps " + _physicsSteps + "; " + Part("ffb writes", Kind.FfbWrite) + "; " +
                          Part("diagnostics samples", Kind.Diagnostics) + "; " + Part("status writes", Kind.StatusWrite);
            _physicsSteps = 0;
            for (int i = 0; i < Calls.Length; i++) { Calls[i] = 0; Ticks[i] = 0; MaxTicks[i] = 0; }
            return line;
        }

        private static string Part(string label, Kind kind)
        {
            int i = (int)kind;
            return label + " " + Calls[i] + ", " + Ms(Ticks[i]) + " ms total, max " + Ms(MaxTicks[i]) + " ms";
        }

        private static string Ms(long ticks) => (ticks * 1000.0 / Stopwatch.Frequency).ToString("F2", CultureInfo.InvariantCulture);
    }
}
