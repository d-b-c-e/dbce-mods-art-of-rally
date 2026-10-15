using System;

namespace ArtOfSimRally.Mod
{
    internal static class SettingsDisplayPolicy
    {
        internal static float Scale(int height, float hostScale, bool followHost)
        {
            hostScale = float.IsNaN(hostScale) ? 1 : Math.Max(.5f, Math.Min(5, hostScale));
            // An explicit non-default UMM scale wins. Auto only compensates for
            // the host's 1x default on a high-resolution display.
            return followHost || Math.Abs(hostScale - 1) > .01f ? hostScale : Math.Max(1, Math.Min(2, height / 1080f));
        }
        internal static float Width(int screenWidth, float scale, float hostWidth)
            // UMM's zero preference means its 960px default, not unlimited room.
            => Math.Max(200, Math.Min(screenWidth - 100, (hostWidth > 0 ? hostWidth : Math.Min(960, screenWidth)) - 70));
        internal static float PageHeight(int screenHeight, float scale, float hostHeight)
            => Math.Max(120, Math.Min(480 * scale, Math.Min(screenHeight * .45f,
                (hostHeight > 0 ? hostHeight : Math.Min(720, screenHeight)) - 180 - 120 * scale)));
        // The page body once a repaint has measured UMM's visible scroll viewport, where our panel starts in it, and our
        // fixed rows (title, header, any card, footer): the whole panel, footer included, then fits without UMM's outer
        // scrolling. A panel starting below half the viewport (other mods listed above ours) is sized to fit once UMM is
        // scrolled to it instead of shrinking to nothing. Before a measurement, or without one, the PageHeight estimate.
        // All values are screen pixels.
        internal static float BodyHeight(float viewport, float panelTop, float fixedRows, float scale, float estimate)
        {
            if (!(viewport > 0) || !(fixedRows > 0) || float.IsNaN(panelTop) || float.IsInfinity(viewport + panelTop + fixedRows)) return estimate;
            float above = panelTop > 0 && panelTop <= viewport / 2 ? panelTop : 0;
            return Math.Max(120, viewport - above - fixedRows - 12 * scale);
        }
        internal static int PageColumns(float width, float scale)
            => Math.Max(1, Math.Min(5, (int)(width / (85 * scale))));
        // The widest compact binding row needs 310 scaled pixels, plus its
        // spacer, style margins and label breathing room. Decide for the row,
        // not each child: individually capped widths can still overflow in sum.
        internal static bool StackRows(float bodyWidth, float scale)
            => bodyWidth < 380 * scale + 20;
    }
}
