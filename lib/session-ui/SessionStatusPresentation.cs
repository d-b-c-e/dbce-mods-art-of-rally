// MIT. Source-vendored recording presentation; no engine, file, device or recorder dependency.
using System;
using System.Globalization;

namespace Dbce.Wheel.Ui
{
    public enum SessionDisplayState { Off, Recording, Saving, Saved, Incomplete, StartFailed, Playback, PlaybackEnded, PlaybackFailed }
    public enum SessionDisplayTone { Recording, Busy, Success, Problem, Playback }

    public sealed class SessionStatusLine
    {
        public readonly string Text;
        public readonly SessionDisplayTone Tone;
        public SessionStatusLine(string text, SessionDisplayTone tone) { Text = text; Tone = tone; }
    }

    /// <summary>Adapters report facts. Saved means the complete package is sealed, not that Stop was pressed.
    /// The host owns its stop key, output policy, and how long to retain a terminal state.</summary>
    public static class SessionStatusPresentation
    {
        public static SessionStatusLine Describe(SessionDisplayState state, double elapsedSeconds,
            bool outputsMuted = false, string stopKey = "F11", string takeoverKey = "F12")
        {
            string clock = Clock(elapsedSeconds);
            switch (state)
            {
                case SessionDisplayState.Off: return null;
                case SessionDisplayState.Recording:
                    return new SessionStatusLine("REC  " + clock + "  |  " + stopKey + " to stop and save" +
                        (outputsMuted ? "  |  Outputs muted" : ""), SessionDisplayTone.Recording);
                case SessionDisplayState.Saving:
                    return new SessionStatusLine("Saving recording  " + clock + "  |  Keep the game open", SessionDisplayTone.Busy);
                case SessionDisplayState.Saved:
                    return new SessionStatusLine("Recording saved  |  You can keep playing", SessionDisplayTone.Success);
                case SessionDisplayState.Incomplete:
                    return new SessionStatusLine("Recording incomplete  |  Files retained; check the recording log", SessionDisplayTone.Problem);
                case SessionDisplayState.StartFailed:
                    return new SessionStatusLine("Recording did not start  |  Check the recording log", SessionDisplayTone.Problem);
                case SessionDisplayState.Playback:
                    return new SessionStatusLine("PLAYBACK  |  Outputs muted  |  " + takeoverKey + " to take over", SessionDisplayTone.Playback);
                case SessionDisplayState.PlaybackEnded:
                    return new SessionStatusLine("Playback ended  |  Outputs remain muted", SessionDisplayTone.Playback);
                case SessionDisplayState.PlaybackFailed:
                    return new SessionStatusLine("Playback failed  |  Outputs remain muted; check the recording log", SessionDisplayTone.Problem);
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static string Clock(double seconds)
        {
            int whole = double.IsNaN(seconds) || seconds <= 0 ? 0 : (int)Math.Min(seconds, 359999);
            return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static SessionStatusLayout Layout(float width, float height, bool threeDisplays, float top = 12)
        {
            if (!Finite(width) || !Finite(height) || width < 64 || height < 64 || !Finite(top))
                throw new ArgumentOutOfRangeException("viewport");
            float scale = Math.Max(.8f, Math.Min(2f, height / 1080f));
            float displayWidth = threeDisplays ? width / 3f : width;
            float boxWidth = Math.Max(1, Math.Min(760 * scale, displayWidth - 24 * scale));
            return new SessionStatusLayout((width - boxWidth) / 2f, Math.Max(0, top) * scale,
                boxWidth, 48 * scale, scale);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class SessionStatusLayout
    {
        public readonly float X, Y, Width, MinHeight, Scale;
        public SessionStatusLayout(float x, float y, float width, float height, float scale)
        { X = x; Y = y; Width = width; MinHeight = height; Scale = scale; }
    }
}
