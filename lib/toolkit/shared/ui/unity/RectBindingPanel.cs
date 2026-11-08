// MIT. Rectangle IMGUI adapter following iRacing Arcade's stripped IL2CPP renderer.
// No GUILayout, device, profile or file calls. Host owns its Unity callback and edit gates.
#nullable disable
#if DBCE_F6_IL2CPP
extern alias interop;
using interop::UnityEngine;
#else
using UnityEngine;
#endif
using System;

namespace Dbce.Wheel.Ui.Unity
{
    public sealed class RectBindingPanel : ISharedBindingPanelUi
    {
        private float x, y, width, scale;
        private int control, pressed = -1;
        private GUIStyle label, help, button;
        // Help page additions: heading, small repository link, text field/area, one-line toggle text and a clear hit area.
        private GUIStyle heading, link, field, area, toggleText, plain, tab;
        public float Bottom { get { return y; } }

        public void Begin(float left, float top, float bodyWidth, float uiScale)
        {
            x = left; y = top; width = bodyWidth; scale = uiScale;
            control = 0;
            if (Event.current.type == EventType.MouseUp || !GUI.enabled) pressed = -1;
            if (label == null)
            {
                // The current Truckful IL2CPP proxy has no GUIStyle copy ctor.
                // Own plain styles instead of aliasing or mutating GUI.skin.
                label = new GUIStyle();
                help = new GUIStyle();
                button = new GUIStyle();
                button.alignment = TextAnchor.MiddleCenter;
                label.wordWrap = help.wordWrap = button.wordWrap = true;
                label.normal.textColor = new Color(F6Palette.Text.R, F6Palette.Text.G, F6Palette.Text.B, 1);
                help.normal.textColor = new Color(F6Palette.Secondary.R, F6Palette.Secondary.G, F6Palette.Secondary.B, 1);
                // Defensively clear every style background, including selected/
                // pressed/focused states. Flat fills below own the presentation.
                foreach (var state in new[] { button.normal, button.hover, button.active, button.focused,
                    button.onNormal, button.onHover, button.onActive, button.onFocused }) state.background = null;
            }
            if (heading == null)
            {
                // Plain owned styles as above; every state background stays null and flat fills draw the surfaces.
                heading = new GUIStyle(); heading.wordWrap = true; heading.fontStyle = FontStyle.Bold;
                heading.normal.textColor = ToColor(F6Palette.Text);
                link = new GUIStyle(); link.alignment = TextAnchor.MiddleLeft; link.clipping = TextClipping.Clip;
                field = new GUIStyle(); field.clipping = TextClipping.Clip;
                area = new GUIStyle(); area.wordWrap = true; area.clipping = TextClipping.Clip;
                toggleText = new GUIStyle(); toggleText.alignment = TextAnchor.MiddleLeft; toggleText.wordWrap = true;
                toggleText.normal.textColor = ToColor(F6Palette.Text);
                plain = new GUIStyle();
                tab = new GUIStyle(); tab.alignment = TextAnchor.MiddleCenter; tab.clipping = TextClipping.Clip;
                foreach (var style in new[] { link, field, area, plain, tab })
                    foreach (var state in States(style)) state.background = null;
            }
            label.fontSize = (int)Math.Round(F6Metrics.BodySize * scale);
            help.fontSize = (int)Math.Round(F6Metrics.HelpSize * scale);
            button.fontSize = (int)Math.Round(F6Metrics.BodySize * scale);
            heading.fontSize = (int)Math.Round(F6Metrics.HeadingSize * scale);
            link.fontSize = (int)Math.Round(F6Metrics.HelpSize * scale);
            field.fontSize = area.fontSize = toggleText.fontSize = tab.fontSize = (int)Math.Round(F6Metrics.BodySize * scale);
        }

        private Rect Next(string text, int size, float minimum)
        {
            // Conservative wrapping estimate avoids a dependency on stripped CalcHeight.
            int columns = Math.Max(12, (int)(width / Math.Max(1, size * scale * .54f)));
            // Each explicit line wraps on its own (a report preview has many); text without one is estimated as before.
            int lines = 0;
            foreach (string part in text.Split('\n')) lines += Math.Max(1, (part.Length + columns - 1) / columns);
            float height = Math.Max(minimum * scale, lines * (size + 4) * scale);
            var rect = new Rect(x, y, width, height);
            y += height + 5 * scale;
            return rect;
        }
        public void Label(string text) { GUI.Label(Next(text, F6Metrics.BodySize, 24), text, label); }
        public void Help(string text) { GUI.Label(Next(text, F6Metrics.HelpSize, 20), text, help); }
        public bool Button(string text, bool enabled = true)
        {
            bool prior = GUI.enabled;
            Color priorColor = GUI.color;
            int id = control++;
            var rect = Next(text, F6Metrics.BodySize, 36);
            bool active = prior && enabled;
            bool hover = active && rect.Contains(Event.current.mousePosition);
            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0) pressed = id;
            var fill = hover ? pressed == id ? F6Palette.Selected : F6Palette.ButtonHover : F6Palette.Button;
            var foreground = active ? F6Palette.Text : F6Palette.Disabled;
            try
            {
                GUI.enabled = true;
                GUI.color = new Color(F6Palette.ButtonBorder.R, F6Palette.ButtonBorder.G, F6Palette.ButtonBorder.B, 1);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = new Color(fill.R, fill.G, fill.B, active ? 1 : .65f);
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Texture2D.whiteTexture);
                foreach (var state in new[] { button.normal, button.hover, button.active, button.focused,
                    button.onNormal, button.onHover, button.onActive, button.onFocused })
                    state.textColor = new Color(foreground.R, foreground.G, foreground.B, 1);
                GUI.color = Color.white; GUI.enabled = active;
                return GUI.Button(rect, text, button);
            }
            finally { GUI.enabled = prior; GUI.color = priorColor; }
        }

        public void Heading(string text) { GUI.Label(Next(text, F6Metrics.HeadingSize, 28), text, heading); }

        /// <summary>A small underlined text link (shared Help presentation, F6Metrics.LinkHeight/LinkUnderline): help-size
        /// accent text with no button surface, only as wide as its estimated text. The host owns the destination.</summary>
        public bool Link(string text)
        {
            var row = Next(text, F6Metrics.HelpSize, F6Metrics.LinkHeight);
            var rect = new Rect(row.x, row.y, Math.Min(row.width, text.Length * F6Metrics.HelpSize * scale * .54f + 2 * scale), row.height);
            bool prior = GUI.enabled;
            Color priorColor = GUI.color;
            bool hover = prior && rect.Contains(Event.current.mousePosition);
            var tone = !prior ? F6Palette.Disabled : hover ? F6Palette.Text : F6Palette.Accent;
            try
            {
                foreach (var state in States(link)) state.textColor = ToColor(tone);
                GUI.color = ToColor(tone);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 3 * scale, rect.width, Math.Max(1, F6Metrics.LinkUnderline * scale)), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return GUI.Button(rect, text, link);
            }
            finally { GUI.enabled = prior; GUI.color = priorColor; }
        }

        /// <summary>A labeled one-line field or several-line area, full body width under its label. Disabled, it shows the
        /// text without an editor. Returns the text as edited this frame.</summary>
        public string TextInput(string text, string value, bool multiline, bool enabled = true, int maxLength = 4000)
        {
            Label(text);
            value = value ?? "";
            float height = (multiline ? 120 : F6Metrics.RowHeight) * scale;
            var rect = new Rect(x, y, width, height);
            y += height + 5 * scale;
            bool prior = GUI.enabled;
            Color priorColor = GUI.color;
            bool active = prior && enabled;
            var style = multiline ? area : field;
            try
            {
                GUI.enabled = true;
                GUI.color = ToColor(F6Palette.ButtonBorder);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = ToColor(active ? F6Palette.Card : F6Palette.Window);
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Texture2D.whiteTexture);
                GUI.color = Color.white;
                foreach (var state in States(style)) state.textColor = ToColor(active ? F6Palette.Text : F6Palette.Disabled);
                var inner = new Rect(rect.x + 8 * scale, rect.y + 6 * scale, rect.width - 16 * scale, rect.height - 12 * scale);
                if (!active) { GUI.Label(inner, value, style); return value; }
                string edited = multiline ? GUI.TextArea(inner, value, maxLength, style) : GUI.TextField(inner, value, maxLength, style);
                return edited ?? value;
            }
            finally { GUI.enabled = prior; GUI.color = priorColor; }
        }

        /// <summary>An explicit on/off row: a flat square (accent when on) and its label; the whole row toggles.
        /// Returns the new value.</summary>
        public bool Toggle(string text, bool value, bool enabled = true)
        {
            var rect = Next(text, F6Metrics.BodySize, F6Metrics.RowHeight);
            bool prior = GUI.enabled;
            Color priorColor = GUI.color;
            bool active = prior && enabled;
            bool hover = active && rect.Contains(Event.current.mousePosition);
            float box = 18 * scale;
            var square = new Rect(rect.x, rect.y + (rect.height - box) / 2, box, box);
            try
            {
                GUI.enabled = true;
                GUI.color = ToColor(F6Palette.ButtonBorder, active ? 1 : .65f);
                GUI.DrawTexture(square, Texture2D.whiteTexture);
                var fill = value ? F6Palette.Accent : hover ? F6Palette.ButtonHover : F6Palette.Button;
                GUI.color = ToColor(fill, active ? 1 : .65f);
                GUI.DrawTexture(new Rect(square.x + 1, square.y + 1, square.width - 2, square.height - 2), Texture2D.whiteTexture);
                GUI.color = Color.white;
                toggleText.normal.textColor = ToColor(active ? F6Palette.Text : F6Palette.Disabled);
                GUI.Label(new Rect(rect.x + box + 10 * scale, rect.y, rect.width - box - 10 * scale, rect.height), text, toggleText);
                GUI.enabled = active;
                return GUI.Button(rect, "", plain) ? !value : value;
            }
            finally { GUI.enabled = prior; GUI.color = priorColor; }
        }

        /// <summary>The page tabs in the shared tab presentation (F6Widgets.Tab): body text, secondary when unselected,
        /// bold primary with the accent line when selected, the tab-focus tint on hover, then a divider. Tabs that do not
        /// fit wrap at tab boundaries. Returns the clicked index or -1; the host switches pages.</summary>
        public int Tabs(string[] labels, int selected, bool enabled = true)
        {
            float height = (F6Metrics.RowHeight + 6) * scale, left = x, top = y;
            int clicked = -1;
            bool prior = GUI.enabled;
            Color priorColor = GUI.color;
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    float w = Math.Min(width, labels[i].Length * F6Metrics.BodySize * scale * .54f + 2 * F6Metrics.TabPadX * scale);
                    if (left > x && left + w > x + width) { left = x; top += height; }
                    var rect = new Rect(left, top, w, height);
                    bool active = prior && enabled, on = i == selected;
                    bool hover = active && rect.Contains(Event.current.mousePosition);
                    GUI.enabled = true;
                    if (hover)
                    {
                        GUI.color = ToColor(F6Palette.TabFocus, .6f);
                        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height - F6Metrics.TabLine * scale), Texture2D.whiteTexture);
                    }
                    if (on)
                    {
                        GUI.color = ToColor(F6Palette.Accent);
                        GUI.DrawTexture(new Rect(rect.x + 6 * scale, rect.y + rect.height - F6Metrics.TabLine * scale, rect.width - 12 * scale, F6Metrics.TabLine * scale), Texture2D.whiteTexture);
                    }
                    GUI.color = Color.white;
                    tab.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                    foreach (var state in States(tab)) state.textColor = ToColor(!active ? F6Palette.Disabled : on || hover ? F6Palette.Text : F6Palette.Secondary);
                    GUI.enabled = active;
                    if (GUI.Button(rect, labels[i], tab)) clicked = i;
                    left += w;
                }
                GUI.enabled = true;
                GUI.color = ToColor(F6Palette.Divider);
                GUI.DrawTexture(new Rect(x, top + height, width, Math.Max(1, scale)), Texture2D.whiteTexture);
            }
            finally { GUI.enabled = prior; GUI.color = priorColor; }
            y = top + height + 8 * scale;
            return clicked;
        }

        private static GUIStyleState[] States(GUIStyle style)
        {
            return new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused };
        }

        private static Color ToColor(F6Color color, float alpha = 1) { return new Color(color.R, color.G, color.B, alpha); }
    }
}
