using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The family frame-rate readout (STD-023/024, the toolkit's FrameRateReadout): average fps, 1% low and worst frame
    /// over ten seconds in the settings panel's Cameras page and the log, and an optional counter at the top right of the
    /// centre screen. Separate from <see cref="FrameHealth"/>, which counts hitches for support files.
    /// </summary>
    internal static class FrameRate
    {
        private static readonly Dbce.Wheel.Ui.Unity.FrameRateReadout Readout = new Dbce.Wheel.Ui.Unity.FrameRateReadout();
        private static double _nextLog;

        internal static string Summary => Readout.Summary;

        /// <summary>Per frame with the unscaled frame time.</summary>
        internal static void Tick()
        {
            double now = Time.unscaledTime;
            Readout.Tick(now, Time.unscaledDeltaTime);
            if (Readout.WindowCompleted && now >= _nextLog)
            {
                _nextLog = now + 30;
                ModLog.Info("Frame rate (10 s): " + Readout.Summary + (Screen.width >= Screen.height * 2.9f ? " (triple-wide window)" : ""));
            }
        }

        // Top right of the centre screen when the window spans three screens.
        internal static void Draw()
        {
            var settings = Main.Settings;
            if (settings == null || !settings.ShowFrameRate) return;
            Readout.Draw(Screen.width >= Screen.height * 2.9f ? Screen.width * 2f / 3f : Screen.width);
        }
    }
}
