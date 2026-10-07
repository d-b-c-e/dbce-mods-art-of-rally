using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The family frame-rate readout (STD-023/024, toolkit FrameRateMonitor, vendored in Toolkit/): average fps, 1% low
    /// and worst frame over ten seconds in the settings panel's Cameras page and the log, and an optional counter at the
    /// top right of the centre screen. Separate from <see cref="FrameHealth"/>, which counts hitches for support files.
    /// </summary>
    internal static class FrameRate
    {
        private static readonly Dbce.Wheel.Telemetry.FrameRateMonitor Monitor = new Dbce.Wheel.Telemetry.FrameRateMonitor();
        private static double _nextLog;
        private static GUIStyle _style;

        internal static string Summary => Monitor.Summary;

        /// <summary>Per frame with the unscaled frame time.</summary>
        internal static void Tick()
        {
            double now = Time.unscaledTime;
            Monitor.Tick(now, Time.unscaledDeltaTime);
            if (Monitor.WindowCompleted && now >= _nextLog)
            {
                _nextLog = now + 30;
                ModLog.Info("Frame rate (10 s): " + Monitor.Summary + (Screen.width >= Screen.height * 2.9f ? " (triple-wide window)" : ""));
            }
        }

        internal static void Draw()
        {
            var settings = Main.Settings;
            if (settings == null || !settings.ShowFrameRate || Event.current == null || Event.current.type != EventType.Repaint) return;
            string text = Monitor.OverlayText;
            if (string.IsNullOrEmpty(text)) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 60), alignment = TextAnchor.UpperRight };
            // Top right of the centre screen when the window spans three screens.
            float right = Screen.width >= Screen.height * 2.9f ? Screen.width * 2f / 3f : Screen.width;
            var rect = new Rect(right - 330f, 8f, 320f, 60f);
            Color old = GUI.color;
            GUI.color = Color.black; GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _style);
            GUI.color = Color.white; GUI.Label(rect, text, _style);
            GUI.color = old;
        }
    }
}
