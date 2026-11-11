// MIT. The family frame-rate readout (STD-023/024) for Unity Mono plugins, extracted from art of rally and DRIVE Rally:
// the toolkit FrameRateMonitor (Dbce.Wheel.Telemetry) plus its optional counter, drawn top right with a one-pixel
// shadow. The game keeps its own setting, its log line and where the center screen's right edge is.
#nullable disable
using Dbce.Wheel.Telemetry;
using UnityEngine;

namespace Dbce.Wheel.Ui.Unity
{
    public sealed class FrameRateReadout
    {
        private readonly FrameRateMonitor _monitor = new FrameRateMonitor();
        private GUIStyle _style;

        /// <summary>Average fps, 1% low and worst frame of the last completed ten-second window, for the panel and log.</summary>
        public string Summary { get { return _monitor.Summary; } }

        /// <summary>True on the frame a ten-second window completed.</summary>
        public bool WindowCompleted { get { return _monitor.WindowCompleted; } }

        /// <summary>Once per frame, with the unscaled time and frame time.</summary>
        public void Tick(double now, float frameSeconds) { _monitor.Tick(now, frameSeconds); }

        /// <summary>From OnGUI while the counter is switched on: draws it in the repaint event, its right edge at
        /// <paramref name="right"/> pixels (the center screen's right edge on a three-screen window).</summary>
        public void Draw(float right)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            string text = _monitor.OverlayText;
            if (string.IsNullOrEmpty(text)) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 60), alignment = TextAnchor.UpperRight };
            var rect = new Rect(right - 330f, 8f, 320f, 60f);
            Color old = GUI.color;
            GUI.color = Color.black; GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _style);
            GUI.color = Color.white; GUI.Label(rect, text, _style);
            GUI.color = old;
        }
    }
}
