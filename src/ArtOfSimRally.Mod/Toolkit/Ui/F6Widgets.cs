// MIT. Optional Unity IMGUI (GUILayout) widgets for the family F6 panel. Keep out of engine-independent assemblies.
// C# 7.3 and Unity APIs present in Unity 2019.4 and 6000.x Mono (art of rally, #DRIVE Rally). Stripped IL2CPP hosts
// without GUILayout use the same F6Style rules with their own renderer.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dbce.Wheel.Ui.Unity
{
    /// <summary>
    /// The F6 panel's controls in Wheelkit's row shape: a label (with optional help under it) on the left and a fixed
    /// editor column on the right, stacking label-over-editor when the body is too narrow (<see cref="F6RowLayout"/>).
    /// Commands are measured and never clipped; segment choices wrap into lines instead of overflowing.
    /// <para>The host owns the window, scrolling, settings, saving and every action; widgets only draw and report what
    /// was clicked or edited. Values are reported as proposals: the host places them on its own grid
    /// (<see cref="NumberEdit"/>) and keeps saved off-grid values until an explicit edit.</para>
    /// <para>Keyboard: Tab / Shift+Tab move between this frame's controls, Enter / Space press the focused one, and
    /// Left / Right step a focused number. A requested focus is scrolled into view inside the body.</para>
    /// Call <see cref="Begin"/> at the start of the window function and <see cref="End"/> at its end.
    /// </summary>
    public sealed class F6Widgets
    {
        /// <summary>Multiplies every size; 1 when the host scales with GUI.matrix.</summary>
        public float Scale = 1f;
        /// <summary>The body width in unscaled units, set by the host each frame.</summary>
        public float BodyWidth = 820f;

        private readonly List<string> _focusable = new List<string>(), _lastFocusable = new List<string>();
        private string _focusRequest;
        private bool _inBody;
        private GUIStyle _label, _help, _heading, _title, _titleWrap, _subtitle, _text, _button, _buttonWrap, _segment, _segmentOn, _segmentWrap, _segmentOnWrap, _tab, _tabOn, _value,
            _warningTitle, _slider, _thumb, _link;
        private GUISkin _builtSkin;
        private float _builtScale = -1f;
        private static readonly Dictionary<int, Texture2D> Textures = new Dictionary<int, Texture2D>();

        private float S(float units) => units * Scale;
        private int Size(int points) => Mathf.Max(8, Mathf.RoundToInt(points * Scale));

        /// <summary>Requests keyboard focus for a control id at the next repaint (and scrolls it into view).</summary>
        public void Focus(string id) => _focusRequest = id;
        public string FocusedId => GUI.GetNameOfFocusedControl();

        public void Begin(float bodyWidth)
        {
            BodyWidth = bodyWidth;
            Build();
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab && _lastFocusable.Count > 0)
            {
                int current = _lastFocusable.IndexOf(GUI.GetNameOfFocusedControl());
                int next = current < 0 ? (e.shift ? _lastFocusable.Count - 1 : 0) : (current + (e.shift ? -1 : 1) + _lastFocusable.Count) % _lastFocusable.Count;
                _focusRequest = _lastFocusable[next];
                e.Use();
            }
            _focusable.Clear();
        }

        public void End()
        {
            var e = Event.current;
            if (e != null && e.type == EventType.Repaint)
            {
                _lastFocusable.Clear(); _lastFocusable.AddRange(_focusable);
                if (_focusRequest != null && _focusable.Contains(_focusRequest)) { GUI.FocusControl(_focusRequest); _focusRequest = null; }
            }
        }

        /// <summary>Wraps the scrolling body; inside it a requested focus scrolls into view. A height of 0 or less
        /// fills the remaining window height.</summary>
        public Vector2 BeginBody(Vector2 scroll, float height)
        {
            _inBody = true;
            return GUILayout.BeginScrollView(scroll, height > 0 ? GUILayout.Height(S(height)) : GUILayout.ExpandHeight(true));
        }
        public void EndBody() { GUILayout.EndScrollView(); _inBody = false; }

        // ---- text ----

        public void Title(string title, string subtitle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _title, GUILayout.ExpandWidth(false));
            if (!string.IsNullOrEmpty(subtitle)) { GUILayout.Space(S(12)); GUILayout.Label(subtitle, _subtitle, GUILayout.ExpandWidth(false)); }
            GUILayout.EndHorizontal();
        }
        /// <summary>
        /// The panel's title row with its close command at the right. The subtitle sits beside the title when everything
        /// fits; otherwise it moves to its own wrapped line under the title, and a title that is still too long wraps
        /// in the space left by the close command. Returns true when the close command was pressed.
        /// </summary>
        public bool TitleBar(string title, string subtitle, string closeId, string closeLabel)
        {
            var closeContent = new GUIContent(closeLabel);
            float close = Mathf.Max(F6Metrics.ButtonMinWidth, _button.CalcSize(closeContent).x / Scale) + F6Metrics.ColumnGap;
            float titleWidth = _title.CalcSize(new GUIContent(title)).x / Scale;
            float subtitleWidth = string.IsNullOrEmpty(subtitle) ? 0 : 12 + _subtitle.CalcSize(new GUIContent(subtitle)).x / Scale;
            float room = Mathf.Max(F6Metrics.LabelMinWidth, BodyWidth - close);
            bool oneLine = titleWidth + subtitleWidth <= room;
            GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
            GUILayout.BeginVertical(GUILayout.Width(S(room)));
            if (oneLine) Title(title, subtitle);
            else
            {
                GUILayout.Label(title, _titleWrap, GUILayout.Width(S(room)));
                if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, _help, GUILayout.Width(S(room)));
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            bool clicked = Command(closeId, closeLabel);
            GUILayout.EndHorizontal();
            return clicked;
        }

        public void Heading(string text) => GUILayout.Label(text, _heading, GUILayout.Width(S(BodyWidth)));
        /// <summary>A bold row-label line at the body width (a sub-section caption, such as one binding).</summary>
        public void Caption(string text) => GUILayout.Label(text, _label, GUILayout.Width(S(BodyWidth)));
        public void Text(string text) => GUILayout.Label(text, _text, GUILayout.Width(S(BodyWidth)));
        public void Help(string text) => GUILayout.Label(text, _help, GUILayout.Width(S(BodyWidth)));

        /// <summary>Body text inside the host's horizontal group (no fixed width).</summary>
        public void Inline(string text) => GUILayout.Label(text, _text);

        /// <summary>Adds a host-drawn control (e.g. a text field named with GUI.SetNextControlName) to the Tab order.</summary>
        public void Register(string id) => _focusable.Add(id);

        /// <summary>After drawing a host control: scrolls it into view when it is the requested focus.</summary>
        public void Reveal(string id)
        {
            if (_inBody && _focusRequest == id && Event.current.type == EventType.Repaint) GUI.ScrollTo(GUILayoutUtility.GetLastRect());
        }

        public void Divider()
        {
            Rect r = GUILayoutUtility.GetRect(S(BodyWidth), S(1), GUILayout.Width(S(BodyWidth)), GUILayout.Height(S(1)));
            Fill(r, F6Palette.Divider);
        }

        /// <summary>A flexible gap inside the host's horizontal group (pushes what follows to the right).</summary>
        public void Spacer() => GUILayout.FlexibleSpace();

        // ---- buttons ----

        /// <summary>A command sized to its text (at least the minimum width), never clipped: when the text is wider
        /// than <paramref name="available"/> (default: the body), it takes that width and wraps to its measured height.</summary>
        public bool Command(string id, string label, bool enabled = true, float available = 0)
        {
            float limit = S(available > 0 ? available : BodyWidth);
            var content = new GUIContent(label);
            float width = Mathf.Max(S(F6Metrics.ButtonMinWidth), _button.CalcSize(content).x);
            float height = S(F6Metrics.RowHeight);
            GUIStyle style = _button;
            if (width > limit)
            {
                width = limit;
                style = _buttonWrap;
                height = Mathf.Max(height, style.CalcHeight(content, width));
            }
            return Press(id, content, style, enabled, GUILayout.Width(width), GUILayout.Height(height));
        }

        /// <summary>A small underlined text link; the host owns the destination and opening it.</summary>
        public bool Link(string id, string label)
        {
            var content = new GUIContent(label);
            float width = Mathf.Max(S(1), Mathf.Min(S(BodyWidth), _link.CalcSize(content).x));
            float height = Mathf.Max(S(F6Metrics.LinkHeight), _link.CalcHeight(content, width));
            bool clicked = Press(id, content, _link, true, GUILayout.Width(width), GUILayout.Height(height), false);
            if (Event.current.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                Fill(new Rect(r.x, r.yMax - S(3), r.width, S(F6Metrics.LinkUnderline)),
                    GUI.GetNameOfFocusedControl() == id ? F6Palette.Text : F6Palette.Accent);
            }
            return clicked;
        }

        /// <summary>One option of a segmented choice (Off / On, Simple / Advanced, ...).</summary>
        public bool Segment(string id, string label, bool selected, bool enabled = true, float available = 0)
        {
            var content = new GUIContent(label);
            float limit = S(available > 0 ? available : BodyWidth);
            float width = Mathf.Max(S(F6Metrics.SegmentMinWidth), _segment.CalcSize(content).x), height = S(F6Metrics.RowHeight);
            GUIStyle style = selected ? _segmentOn : _segment;
            if (width > limit)
            {
                // Never wider than the line: take the line and wrap at the measured height.
                width = limit;
                style = selected ? _segmentOnWrap : _segmentWrap;
                height = Mathf.Max(height, style.CalcHeight(content, width));
            }
            return Press(id, content, style, enabled, GUILayout.Width(width), GUILayout.Height(height));
        }

        /// <summary>A page tab: text with the accent line under the selected one.</summary>
        public bool Tab(string id, string label, bool selected, bool enabled = true)
        {
            var content = new GUIContent(label);
            var style = selected ? _tabOn : _tab;
            float width = style.CalcSize(content).x;
            bool clicked = Press(id, content, style, enabled, GUILayout.Width(width), GUILayout.Height(S(F6Metrics.RowHeight + 6)), false);
            if (Event.current.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                if (selected) Fill(new Rect(r.x + S(6), r.yMax - S(F6Metrics.TabLine), r.width - S(12), S(F6Metrics.TabLine)), F6Palette.Accent);
                if (GUI.GetNameOfFocusedControl() == id) Fill(new Rect(r.x, r.y, r.width, r.height - S(F6Metrics.TabLine)), F6Palette.TabFocus.WithAlpha(.6f));
            }
            return clicked;
        }

        /// <summary>
        /// The page tabs and the view switch. On one line when both fit (tabs left, view switch right); otherwise the tabs
        /// wrap into lines at tab boundaries and the view switch takes its own line below. Focus order is always the tabs,
        /// then the views. Returns the clicked tab index or -1; <paramref name="viewClicked"/> is the clicked view or -1.
        /// </summary>
        public int Header(string[] tabIds, string[] tabs, int selectedTab, bool tabsEnabled,
            string[] viewIds, string[] views, int selectedView, bool viewsEnabled, out int viewClicked)
        {
            viewClicked = -1;
            int clicked = -1;
            var tabWidths = new float[tabs.Length];
            float tabsTotal = 0;
            for (int i = 0; i < tabs.Length; i++) { tabWidths[i] = (i == selectedTab ? _tabOn : _tab).CalcSize(new GUIContent(tabs[i])).x / Scale; tabsTotal += tabWidths[i]; }
            var viewWidths = new float[views.Length];
            float viewsTotal = 0;
            for (int i = 0; i < views.Length; i++) { viewWidths[i] = Mathf.Max(S(F6Metrics.SegmentMinWidth), _segment.CalcSize(new GUIContent(views[i])).x) / Scale; viewsTotal += viewWidths[i] + (i > 0 ? F6Metrics.RowGap : 0); }
            if (tabsTotal + F6Metrics.ColumnGap + viewsTotal <= BodyWidth)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
                for (int i = 0; i < tabs.Length; i++) if (Tab(tabIds[i], tabs[i], i == selectedTab, tabsEnabled)) clicked = i;
                GUILayout.FlexibleSpace();
                for (int i = 0; i < views.Length; i++) if (Segment(viewIds[i], views[i], i == selectedView, viewsEnabled)) viewClicked = i;
                GUILayout.EndHorizontal();
            }
            else
            {
                int tabLines, viewLines;
                int[] tabLine = F6Flow.Lines(tabWidths, BodyWidth, 0, out tabLines);
                for (int line = 0, i = 0; line < tabLines; line++)
                {
                    GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
                    for (; i < tabs.Length && tabLine[i] == line; i++) if (Tab(tabIds[i], tabs[i], i == selectedTab, tabsEnabled)) clicked = i;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                int[] viewLine = F6Flow.Lines(viewWidths, BodyWidth, F6Metrics.RowGap, out viewLines);
                for (int line = 0, i = 0; line < viewLines; line++)
                {
                    GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
                    for (; i < views.Length && viewLine[i] == line; i++) if (Segment(viewIds[i], views[i], i == selectedView, viewsEnabled, BodyWidth)) viewClicked = i;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            Divider();
            return clicked;
        }

        /// <summary>Segments that wrap into lines within <paramref name="available"/> units. Returns the clicked index or -1.</summary>
        public int Segments(string idPrefix, string[] labels, int selected, bool enabled, float available, bool alignRight)
        {
            if (labels == null || labels.Length == 0) return -1;
            var widths = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++) widths[i] = Mathf.Max(S(F6Metrics.SegmentMinWidth), _segment.CalcSize(new GUIContent(labels[i])).x) / Scale;
            int lineCount;
            int[] lines = F6Flow.Lines(widths, available, F6Metrics.RowGap, out lineCount);
            int clicked = -1;
            int item = 0;
            for (int line = 0; line < lineCount; line++)
            {
                GUILayout.BeginHorizontal();
                if (alignRight) GUILayout.FlexibleSpace();
                for (; item < labels.Length && lines[item] == line; item++)
                    if (Segment(idPrefix + item, labels[item], item == selected, enabled, available)) clicked = item;
                GUILayout.EndHorizontal();
            }
            return clicked;
        }

        // ---- rows ----

        /// <summary>A setting with Off / On. Returns the proposed value.</summary>
        public bool ToggleRow(string id, string label, string help, bool value, bool enabled)
        {
            float needs = 2 * F6Metrics.SegmentMinWidth + F6Metrics.RowGap;
            var layout = BeginRow(label, help, needs);
            if (!layout.Stacked) GUILayout.FlexibleSpace();
            if (Segment(id + "-off", "Off", !value, enabled)) value = false;
            if (Segment(id + "-on", "On", value, enabled)) value = true;
            EndRow(layout);
            return value;
        }

        /// <summary>A number with slider, minus, value and plus, and an optional "Default" command when the host has a
        /// default to restore. Returns the proposed value (the host snaps or clamps it).</summary>
        public double NumberRow(string id, string label, string help, double value, double min, double max, double step,
            string format, string unit, bool enabled, double? defaultValue = null)
        {
            var layout = BeginRow(label, help, F6Metrics.ControlColumn + (defaultValue.HasValue ? 96f : 0f));
            double proposed = value;
            bool prior = GUI.enabled;
            // Arrow keys first, before the slider can consume them with its own increment: exactly one host step.
            var e = Event.current;
            string focus = GUI.GetNameOfFocusedControl();
            bool arrow = enabled && prior && e.type == EventType.KeyDown && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow) &&
                (focus == id + "-down" || focus == id + "-up" || focus == id + "-slider");
            if (arrow) { proposed = value + (e.keyCode == KeyCode.LeftArrow ? -step : step); GUI.changed = true; e.Use(); }
            GUI.enabled = prior && enabled;
            Rect area = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.MinWidth(S(60)), GUILayout.Height(S(F6Metrics.RowHeight)), GUILayout.ExpandWidth(true));
            float fraction = max > min ? Mathf.Clamp01((float)((value - min) / (max - min))) : 0f;
            Rect track = new Rect(area.x + S(8), area.y + area.height / 2f - S(2), area.width - S(16), S(4));
            if (Event.current.type == EventType.Repaint)
            {
                Fill(track, F6Palette.Track);
                Fill(new Rect(track.x, track.y, track.width * fraction, track.height), GUI.enabled ? F6Palette.Accent : F6Palette.Disabled);
            }
            bool changedBefore = GUI.changed; GUI.changed = false;
            GUI.SetNextControlName(id + "-slider");
            if (GUI.enabled) _focusable.Add(id + "-slider");
            float slid = GUI.HorizontalSlider(new Rect(track.x, area.y + area.height / 2f - S(9), track.width, S(18)), (float)value, (float)min, (float)max, _slider, _thumb);
            if (GUI.changed && GUI.enabled && !arrow) proposed = slid;
            GUI.changed = changedBefore || GUI.changed;
            FocusOutline(id + "-slider", area);
            if (_inBody && _focusRequest == id + "-slider" && Event.current.type == EventType.Repaint) GUI.ScrollTo(area);
            GUI.enabled = prior;
            GUILayout.Space(S(F6Metrics.RowGap));
            if (Press(id + "-down", new GUIContent("−"), _button, enabled && value > min, GUILayout.Width(S(F6Metrics.StepButton)), GUILayout.Height(S(F6Metrics.RowHeight)))) proposed = value - step;
            GUILayout.Label(value.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + unit, _value,
                GUILayout.Width(S(F6Metrics.ValueWidth)), GUILayout.Height(S(F6Metrics.RowHeight)));
            if (Press(id + "-up", new GUIContent("+"), _button, enabled && value < max, GUILayout.Width(S(F6Metrics.StepButton)), GUILayout.Height(S(F6Metrics.RowHeight)))) proposed = value + step;
            if (defaultValue.HasValue)
            {
                GUILayout.Space(S(F6Metrics.RowGap));
                if (Command(id + "-default", "Default", enabled && value != defaultValue.Value, 96f)) proposed = defaultValue.Value;
            }
            EndRow(layout);
            return proposed;
        }

        /// <summary>A setting with one of several options. Returns the clicked index or -1.</summary>
        public int ChoiceRow(string id, string label, string help, string[] options, int selected, bool enabled)
        {
            float needs = 0;
            for (int i = 0; options != null && i < options.Length; i++)
                needs += Mathf.Max(S(F6Metrics.SegmentMinWidth), _segment.CalcSize(new GUIContent(options[i])).x) / Scale + (i > 0 ? F6Metrics.RowGap : 0);
            var layout = BeginRow(label, help, Mathf.Min(needs, BodyWidth));
            GUILayout.BeginVertical();
            int clicked = Segments(id + "-", options, selected, enabled, layout.EditorWidth, !layout.Stacked);
            GUILayout.EndVertical();
            EndRow(layout);
            return clicked;
        }

        /// <summary>A row with a host-drawn editor: call, draw inside the returned layout's editor area, then EndRow.</summary>
        public F6RowLayout BeginRow(string label, string help, float editorNeeds)
        {
            var layout = F6RowLayout.For(BodyWidth, editorNeeds);
            GUILayout.Space(S(F6Metrics.RowPaddingY));
            if (layout.Stacked)
            {
                LabelBlock(label, help, BodyWidth);
                GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
                GUILayout.BeginVertical(GUILayout.Width(S(layout.LabelWidth)));
                LabelBlock(label, help, layout.LabelWidth);
                GUILayout.EndVertical();
                GUILayout.Space(S(F6Metrics.ColumnGap));
                GUILayout.BeginHorizontal(GUILayout.Width(S(layout.EditorWidth)));
            }
            return layout;
        }

        public void EndRow(F6RowLayout layout)
        {
            if (!layout.Stacked) GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
            GUILayout.Space(S(F6Metrics.RowPaddingY));
            Divider();
        }

        /// <summary>A highlighted status box (title, detail and an optional action such as "Why?"). Returns true when
        /// the action was pressed.</summary>
        public bool StatusCard(string id, string title, string detail, bool warning, string action)
        {
            GUILayout.Space(S(F6Metrics.RowPaddingY));
            Rect start = GUILayoutUtility.GetRect(S(BodyWidth), 0, GUILayout.Width(S(BodyWidth)), GUILayout.Height(0));
            GUILayout.BeginHorizontal(GUILayout.Width(S(BodyWidth)));
            GUILayout.Space(S(14));
            GUILayout.BeginVertical();
            GUILayout.Space(S(10));
            GUILayout.Label(title, warning ? _warningTitle : _label);
            if (!string.IsNullOrEmpty(detail)) GUILayout.Label(detail, _help);
            GUILayout.Space(S(10));
            GUILayout.EndVertical();
            bool clicked = false;
            if (!string.IsNullOrEmpty(action))
            {
                GUILayout.BeginVertical(GUILayout.ExpandWidth(false));
                GUILayout.Space(S(10));
                clicked = Command(id, action, true, F6Metrics.ControlColumn);
                GUILayout.EndVertical();
            }
            GUILayout.Space(S(14));
            GUILayout.EndHorizontal();
            if (Event.current.type == EventType.Repaint)
            {
                Rect end = GUILayoutUtility.GetLastRect();
                var box = new Rect(start.x, start.y, S(BodyWidth), end.yMax - start.y);
                Outline(box, F6Palette.ButtonBorder);
                Fill(new Rect(box.x, box.y, S(3), box.height), warning ? F6Palette.Warning : F6Palette.Accent);
            }
            GUILayout.Space(S(F6Metrics.RowPaddingY));
            return clicked;
        }

        // ---- internals ----

        private void LabelBlock(string label, string help, float width)
        {
            GUILayout.Label(label, _label, GUILayout.Width(S(width)));
            if (!string.IsNullOrEmpty(help)) GUILayout.Label(help, _help, GUILayout.Width(S(width)));
        }

        private bool Press(string id, GUIContent content, GUIStyle style, bool enabled, GUILayoutOption a, GUILayoutOption b, bool outline = true)
        {
            bool prior = GUI.enabled;
            GUI.enabled = prior && enabled;
            if (GUI.enabled) _focusable.Add(id);
            bool focused = GUI.GetNameOfFocusedControl() == id;
            GUI.SetNextControlName(id);
            bool clicked = GUILayout.Button(content, style, a, b);
            var e = Event.current;
            if (GUI.enabled && focused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
            { clicked = true; GUI.changed = true; e.Use(); }   // hosts that save on GUI.changed see keyboard presses too
            if (e.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                if (outline) Outline(r, focused ? F6Palette.Accent : F6Palette.ButtonBorder);
                if (_inBody && _focusRequest == id) GUI.ScrollTo(r);
            }
            GUI.enabled = prior;
            return prior && enabled && clicked;
        }

        private void FocusOutline(string id, Rect r)
        {
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() == id) Outline(r, F6Palette.Accent);
        }

        private void Outline(Rect r, F6Color color)
        {
            float t = Mathf.Max(1f, S(F6Metrics.FocusLine));
            Fill(new Rect(r.x, r.y, r.width, t), color); Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
            Fill(new Rect(r.x, r.y, t, r.height), color); Fill(new Rect(r.xMax - t, r.y, t, r.height), color);
        }

        /// <summary>Fills a rectangle with a palette color (repaint only).</summary>
        public static void Fill(Rect r, F6Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            GUI.color = ToColor(color);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static Color ToColor(F6Color c) => new Color(c.R, c.G, c.B, c.A);

        /// <summary>A 1×1 texture of a palette color, created once and kept across scenes.</summary>
        public static Texture2D Texture(F6Color c)
        {
            int key = (Mathf.RoundToInt(c.R * 255) << 24) | (Mathf.RoundToInt(c.G * 255) << 16) | (Mathf.RoundToInt(c.B * 255) << 8) | Mathf.RoundToInt(c.A * 255);
            Texture2D texture;
            if (Textures.TryGetValue(key, out texture) && texture != null) return texture;
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, ToColor(c));
            texture.Apply();
            Textures[key] = texture;
            return texture;
        }

        private void Build()
        {
            // Built once per skin and scale (no per-frame allocation); a skin or scale change rebuilds.
            if (_label != null && _builtSkin == GUI.skin && Mathf.Approximately(_builtScale, Scale)) return;
            _builtSkin = GUI.skin; _builtScale = Scale;
            _label = Style(GUI.skin.label, F6Metrics.BodySize, F6Palette.Text, true);
            _label.fontStyle = FontStyle.Bold;
            _text = Style(GUI.skin.label, F6Metrics.BodySize, F6Palette.Text, true);
            _help = Style(GUI.skin.label, F6Metrics.HelpSize, F6Palette.Secondary, true);
            _link = Style(GUI.skin.label, F6Metrics.HelpSize, F6Palette.Accent, true);
            _link.padding = _link.margin = _link.border = new RectOffset();
            _link.alignment = TextAnchor.MiddleLeft;
            Backgrounds(_link, F6Palette.Window.WithAlpha(0f), F6Palette.Window.WithAlpha(0f), F6Palette.Window.WithAlpha(0f));
            _link.hover.textColor = _link.active.textColor = _link.focused.textColor = ToColor(F6Palette.Text);
            _heading = Style(GUI.skin.label, F6Metrics.HeadingSize, F6Palette.Text, true);
            _heading.fontStyle = FontStyle.Bold;
            _heading.margin = new RectOffset(0, 0, Mathf.RoundToInt(S(14)), Mathf.RoundToInt(S(4)));
            _title = Style(GUI.skin.label, F6Metrics.TitleSize, F6Palette.Text, false);
            _title.fontStyle = FontStyle.Bold;
            _titleWrap = new GUIStyle(_title) { wordWrap = true };
            _subtitle = Style(GUI.skin.label, F6Metrics.HelpSize, F6Palette.Secondary, false);
            _subtitle.alignment = TextAnchor.LowerLeft;
            _warningTitle = Style(GUI.skin.label, F6Metrics.BodySize, F6Palette.Warning, true);
            _warningTitle.fontStyle = FontStyle.Bold;
            _value = Style(GUI.skin.label, F6Metrics.BodySize, F6Palette.Text, false);
            _value.alignment = TextAnchor.MiddleCenter;
            _value.fontStyle = FontStyle.Bold;
            _value.normal.background = Texture(F6Palette.Window);
            _button = Style(GUI.skin.button, F6Metrics.BodySize, F6Palette.Text, false);
            int padX = Mathf.RoundToInt(S(F6Metrics.ButtonPadX)), padY = Mathf.RoundToInt(S(F6Metrics.ButtonPadY));
            _button.padding = new RectOffset(padX, padX, padY, padY);
            _button.margin = new RectOffset(Mathf.RoundToInt(S(3)), Mathf.RoundToInt(S(3)), Mathf.RoundToInt(S(2)), Mathf.RoundToInt(S(2)));
            _button.alignment = TextAnchor.MiddleCenter;
            Backgrounds(_button, F6Palette.Button, F6Palette.ButtonHover, F6Palette.Selected);
            _buttonWrap = new GUIStyle(_button) { wordWrap = true };
            _segment = new GUIStyle(_button);
            _segmentOn = new GUIStyle(_button);
            Backgrounds(_segmentOn, F6Palette.Selected, F6Palette.Selected, F6Palette.Selected);
            _segmentWrap = new GUIStyle(_segment) { wordWrap = true };
            _segmentOnWrap = new GUIStyle(_segmentOn) { wordWrap = true };
            _tab = Style(GUI.skin.button, F6Metrics.BodySize, F6Palette.Secondary, false);
            int tabX = Mathf.RoundToInt(S(F6Metrics.TabPadX)), tabY = Mathf.RoundToInt(S(F6Metrics.TabPadY) * .6f);
            _tab.padding = new RectOffset(tabX, tabX, tabY, tabY);
            _tab.margin = new RectOffset(0, 0, 0, 0);
            _tab.alignment = TextAnchor.MiddleCenter;
            Backgrounds(_tab, F6Palette.Header.WithAlpha(0f), F6Palette.TabFocus, F6Palette.TabFocus);
            _tab.hover.textColor = ToColor(F6Palette.Text);
            _tabOn = new GUIStyle(_tab) { fontStyle = FontStyle.Bold };
            _tabOn.normal.textColor = _tabOn.hover.textColor = _tabOn.active.textColor = _tabOn.focused.textColor = ToColor(F6Palette.Text);
            _slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = S(18), padding = new RectOffset(), margin = new RectOffset() };
            _slider.normal.background = _slider.hover.background = _slider.active.background = _slider.focused.background = null;
            _thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = S(14), fixedHeight = S(18), padding = new RectOffset(), margin = new RectOffset() };
            _thumb.normal.background = _thumb.focused.background = Texture(F6Palette.Accent);
            _thumb.hover.background = _thumb.active.background = Texture(F6Palette.Text);
        }

        private GUIStyle Style(GUIStyle from, int size, F6Color color, bool wrap)
        {
            var style = new GUIStyle(from) { fontSize = Size(size), wordWrap = wrap, richText = false };
            Color c = ToColor(color);
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = c;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = c;
            return style;
        }

        private static void Backgrounds(GUIStyle style, F6Color normal, F6Color hover, F6Color active)
        {
            style.normal.background = style.focused.background = style.onNormal.background = style.onFocused.background = Texture(normal);
            style.hover.background = style.onHover.background = Texture(hover);
            style.active.background = style.onActive.background = Texture(active);
            style.border = new RectOffset(0, 0, 0, 0);
        }
    }
}
