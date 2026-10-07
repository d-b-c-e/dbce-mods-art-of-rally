// Vendored from dbce-wheel-mod-toolkit 6121d47 (dotnet/Dbce.Wheel.Telemetry/FrameRateMonitor.cs), the family
// frame-rate readout (STD-023/024). The vendored toolkit Dbce.Wheel.Telemetry.dll predates it; drop this copy when
// the pin moves to a toolkit release that ships FrameRateMonitor. Do not edit here: change the toolkit and re-vendor.
#nullable disable   // toolkit source, written without nullable annotations
using System;

namespace Dbce.Wheel.Telemetry
{
    /// <summary>
    /// The family frame-rate readout (STD-023/024): average fps, 1% low and worst frame over ten-second
    /// windows, plus a smoothed current fps for an on-screen counter. Engine-agnostic: a mod calls
    /// <see cref="Tick"/> from its per-frame update with the unscaled frame time and draws the strings itself.
    /// </summary>
    /// <remarks>
    /// Owner, 2026-10-06: "the fps monitor helped, that's one to try and distribute to all the other projects
    /// via the toolkit". Reference: iRacing Arcade's FrameStats. Fixed buffer, one sort per window, no
    /// allocation per frame.
    /// </remarks>
    public sealed class FrameRateMonitor
    {
        public const float WindowSeconds = 10f;
        private readonly float[] _frames = new float[8192];
        private readonly float[] _sorted = new float[8192];
        private int _count;
        private double _windowStart = -1;
        private float _smoothed;

        /// <summary>Last completed window, e.g. "64 fps average, 1% low 31 fps, worst frame 39 ms"; "measuring" until then.</summary>
        public string Summary { get; private set; } = "measuring";
        /// <summary>Short text for an on-screen counter: "62 fps  ·  1% low 31".</summary>
        public string OverlayText => _smoothed <= 0 ? "" : $"{1f / _smoothed:F0} fps" + (Last.Frames > 0 ? $"  ·  1% low {Last.LowFps:F0}" : "");
        public FrameRateWindow.Result Last { get; private set; }
        /// <summary>True on the tick that completed a window (log <see cref="Summary"/> then, at the mod's own rate).</summary>
        public bool WindowCompleted { get; private set; }

        public void Tick(double now, float frameSeconds)
        {
            WindowCompleted = false;
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            if (_windowStart < 0) _windowStart = now;
            if (frameSeconds > 0 && !float.IsNaN(frameSeconds) && !float.IsInfinity(frameSeconds))
            {
                if (_count < _frames.Length) _frames[_count++] = frameSeconds;
                _smoothed = _smoothed <= 0 ? frameSeconds : _smoothed + (frameSeconds - _smoothed) * .1f;
            }
            if (now - _windowStart < WindowSeconds) return;
            var r = FrameRateWindow.Summarize(_frames, _count, _sorted);
            _count = 0; _windowStart = now;
            if (r.Frames == 0) return;
            Last = r; WindowCompleted = true;
            Summary = $"{r.AverageFps:F0} fps average, 1% low {r.LowFps:F0} fps, worst frame {r.WorstMs:F0} ms";
        }
    }

    /// <summary>Pure frame-time statistics behind <see cref="FrameRateMonitor"/>.</summary>
    public static class FrameRateWindow
    {
        public struct Result { public int Frames; public double AverageFps, LowFps, WorstMs; }

        /// <summary>Average fps, the mean fps of the slowest 1% of frames (at least one), and the worst frame in ms.</summary>
        /// <param name="frames">Frame times in seconds.</param>
        /// <param name="count">How many entries of <paramref name="frames"/> to use.</param>
        /// <param name="scratch">Optional buffer of at least <paramref name="count"/> floats, to avoid allocating.</param>
        public static Result Summarize(float[] frames, int count, float[] scratch = null)
        {
            if (frames == null || count <= 0) return default;
            count = Math.Min(count, frames.Length);
            var sorted = scratch != null && scratch.Length >= count ? scratch : new float[count];
            Array.Copy(frames, sorted, count);
            Array.Sort(sorted, 0, count);
            double total = 0;
            for (int i = 0; i < count; i++) total += sorted[i];
            int slow = Math.Max(1, count / 100);
            double slowTotal = 0;
            for (int i = count - slow; i < count; i++) slowTotal += sorted[i];
            return new Result { Frames = count, AverageFps = count / total, LowFps = slow / slowTotal, WorstMs = sorted[count - 1] * 1000.0 };
        }
    }
}
