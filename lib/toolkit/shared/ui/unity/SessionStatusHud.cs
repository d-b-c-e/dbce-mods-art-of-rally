// MIT. Optional Unity IMGUI renderer. Keep out of engine-independent managed assemblies.
using UnityEngine;

namespace Dbce.Wheel.Ui.Unity
{
    /// <summary>Call from the host's always-visible OnGUI, not an F6-only callback.
    /// No GameObject, input, force, recording lifecycle or configuration ownership.</summary>
    public sealed class SessionStatusHud
    {
        private GUIStyle style;
        public void Draw(SessionStatusLine line, bool threeDisplays, float top = 12)
        {
            if (line == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldDepth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.depth = -10000;
                var layout = SessionStatusPresentation.Layout(Screen.width, Screen.height, threeDisplays, top);
                if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                style.fontSize = Mathf.RoundToInt(20 * layout.Scale);
                style.normal.textColor = Color.white;
                var content = new GUIContent(line.Text);
                float inset = 14 * layout.Scale;
                float height = Mathf.Max(layout.MinHeight, style.CalcHeight(content, layout.Width - 2 * inset) + 12 * layout.Scale);
                var rect = new Rect(layout.X, layout.Y, layout.Width, height);
                GUI.color = new Color(.04f, .05f, .07f, .94f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = line.Tone == SessionDisplayTone.Problem || line.Tone == SessionDisplayTone.Busy
                    ? new Color(1f, .75f, .25f) : line.Tone == SessionDisplayTone.Recording
                    ? new Color(1f, .3f, .35f) : new Color(.4f, .9f, .8f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 5 * layout.Scale, rect.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + inset, rect.y, rect.width - 2 * inset, rect.height), content, style);
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; }
        }
    }
}
