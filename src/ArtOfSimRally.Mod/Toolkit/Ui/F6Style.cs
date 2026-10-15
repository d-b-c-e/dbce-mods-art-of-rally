// MIT. Source-vendored F6 panel presentation rules: palette, metrics and row geometry. No engine, device, input or
// persistence dependency; C# 7.3 so Unity 2019 Mono hosts (art of rally) and newer hosts compile the same file.
using System;

namespace Dbce.Wheel.Ui
{
    /// <summary>An RGBA color token, 0..1 per channel; engines convert it to their own color type.</summary>
    public struct F6Color
    {
        public readonly float R, G, B, A;
        public F6Color(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        /// <summary>From 0xRRGGBB, opaque.</summary>
        public static F6Color Hex(int rgb) => new F6Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        public F6Color WithAlpha(float a) => new F6Color(R, G, B, a);
    }

    /// <summary>
    /// The F6 palette, taken from Wheelkit's dark theme so the in-game panel reads like the app
    /// (dbce-wheelkit 03a43a9: Views/ThemeResources.xaml, FormStyles.xaml, SettingRows.xaml).
    /// </summary>
    public static class F6Palette
    {
        public static readonly F6Color Window = F6Color.Hex(0x121315);        // ListBg
        public static readonly F6Color Header = F6Color.Hex(0x181A1D);        // WindowHeaderBackground
        public static readonly F6Color Card = F6Color.Hex(0x1A1C1F);          // CardBg
        public static readonly F6Color Divider = F6Color.Hex(0x26282B);       // Divider
        public static readonly F6Color Text = F6Color.Hex(0xE7EAF0);          // primary text
        public static readonly F6Color Secondary = F6Color.Hex(0xA8AEB7);     // help text, unselected tabs
        public static readonly F6Color Accent = F6Color.Hex(0x55B9E8);        // selected tab line, keyboard focus, slider fill
        public static readonly F6Color Button = F6Color.Hex(0x292D34);        // FormButton background
        public static readonly F6Color ButtonBorder = F6Color.Hex(0x464D58);
        public static readonly F6Color ButtonHover = F6Color.Hex(0x394655);
        public static readonly F6Color Selected = F6Color.Hex(0x244760);      // selected segment (FormRow IsSelected)
        public static readonly F6Color TabFocus = F6Color.Hex(0x283642);      // SettingsTab keyboard focus
        public static readonly F6Color Track = F6Color.Hex(0x3A4048);         // slider track
        public static readonly F6Color Warning = F6Color.Hex(0xE0A33E);       // disabled notes, unsaved drafts
        public static readonly F6Color Disabled = F6Color.Hex(0x6B717A);      // disabled text
    }

    /// <summary>Logical-unit metrics (1080p design units; the host scales the whole panel).</summary>
    public static class F6Metrics
    {
        // Kiki's shared F6 review (hub docs/reviews/2026-10-14-shared-f6-ux-kiki.md): rows at least 40 high, commands at
        // least 36 with measured width + 24, numeric editor slider >= 120 / step buttons 36 / value 72..96, editor capped
        // near 360, label above editor when the usable width is below about 720.
        public const float RowHeight = 36f, RowGap = 6f, RowPaddingY = 6f, ColumnGap = 16f;
        /// <summary>The editor column (Wheelkit's GameSettingRow uses 300; the review caps editors near 360).</summary>
        public const float ControlColumn = 340f;
        public const float ButtonPadX = 12f, ButtonPadY = 6f, ButtonMinWidth = 64f, StepButton = 36f, ValueWidth = 80f;
        public const float SegmentMinWidth = 56f, TabPadX = 18f, TabPadY = 10f, TabLine = 2f, FocusLine = 1f;
        public const float LabelMinWidth = 200f, EditorMinWidth = 220f, StackBelow = 720f;
        public const int BodySize = 16, HelpSize = 14, HeadingSize = 20, TitleSize = 22;
    }

    /// <summary>Where a row's label and editor go for a given body width (Wheelkit's two columns, or stacked).</summary>
    public struct F6RowLayout
    {
        /// <summary>True: label on its own line, editor below at the full body width.</summary>
        public readonly bool Stacked;
        public readonly float LabelWidth, EditorWidth;
        public F6RowLayout(bool stacked, float labelWidth, float editorWidth) { Stacked = stacked; LabelWidth = labelWidth; EditorWidth = editorWidth; }

        /// <summary>
        /// Two columns when the body is at least <see cref="F6Metrics.StackBelow"/> wide and the label's minimum and the
        /// editor's needed width fit beside each other; otherwise stacked.
        /// <paramref name="editorNeeds"/> is the editor's own measured minimum (a segment row can need more than the
        /// fixed column); the column is never narrower than <see cref="F6Metrics.ControlColumn"/> when that fits.
        /// </summary>
        public static F6RowLayout For(float bodyWidth, float editorNeeds)
        {
            if (float.IsNaN(bodyWidth) || bodyWidth <= 0) bodyWidth = 1;
            float editor = Math.Max(F6Metrics.ControlColumn, Math.Max(F6Metrics.EditorMinWidth, editorNeeds));
            float label = bodyWidth - editor - F6Metrics.ColumnGap;
            if (bodyWidth >= F6Metrics.StackBelow && label >= F6Metrics.LabelMinWidth) return new F6RowLayout(false, label, editor);
            return new F6RowLayout(true, bodyWidth, bodyWidth);
        }
    }

    /// <summary>Lays out measured items (segments, commands) left to right, wrapping into more lines when they do not
    /// fit, so nothing is clipped or drawn past the body. Returns the line index for each item.</summary>
    public static class F6Flow
    {
        public static int[] Lines(float[] widths, float available, float gap, out int lineCount)
        {
            var lines = new int[widths == null ? 0 : widths.Length];
            lineCount = lines.Length == 0 ? 0 : 1;
            float x = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                float w = Math.Min(widths[i], available);
                if (x > 0 && x + gap + w > available) { lineCount++; x = 0; }
                else if (x > 0) x += gap;
                lines[i] = lineCount - 1;
                x += w;
            }
            return lines;
        }
    }
}
