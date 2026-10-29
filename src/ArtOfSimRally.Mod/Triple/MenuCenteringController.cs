using System.Collections.Generic;
using Dbce.TripleScreen.Protocol;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>
/// On a three-panel span, keeps the game's screen-space UI (menus, backgrounds, HUD) on the middle panel, as separate
/// displays already do (KI-52). Every root canvas covering the whole span has its top-level elements' horizontal
/// anchors mapped into the center third. The game's main UI and HUD scalers match height, so element sizes stay right.
/// Original anchors are restored on release.
/// </summary>
internal sealed class MenuCenteringController
{
    private readonly Dictionary<RectTransform, (Vector2 Min, Vector2 Max)> _original = new();

    internal void Update(bool enabled, LayoutLoadResult layout)
    {
        if (!enabled || !MatchesThreePanelOutput(layout))
        {
            Release();
            return;
        }

        var gone = new List<RectTransform>();
        foreach (var rect in _original.Keys) if (rect == null) gone.Add(rect);
        foreach (var rect in gone) _original.Remove(rect);   // destroyed with their scene

        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            // A camera canvas already limited to a camera rect is not on the whole span.
            if (Mathf.Abs(canvas.pixelRect.width - Screen.width) > 1f) continue;
            foreach (Transform child in canvas.transform)
                if (child is RectTransform rect && !_original.ContainsKey(rect))
                {
                    _original.Add(rect, (rect.anchorMin, rect.anchorMax));
                    rect.anchorMin = new Vector2(CenterThird(rect.anchorMin.x), rect.anchorMin.y);
                    rect.anchorMax = new Vector2(CenterThird(rect.anchorMax.x), rect.anchorMax.y);
                }
        }
        // shortcut: an element the game re-anchors after we move it stays where the game put it; add a re-check if a capture shows one.
    }

    internal static float CenterThird(float anchor) => (1f + anchor) / 3f;

    internal void Release()
    {
        foreach (var item in _original)
            if (item.Key != null)
            {
                item.Key.anchorMin = item.Value.Min;
                item.Key.anchorMax = item.Value.Max;
            }
        _original.Clear();
    }

    private static bool MatchesThreePanelOutput(LayoutLoadResult result)
    {
        if (!result.IsSuccess || result.Document?.Output == null || result.Document.Panel == null) return false;
        var output = result.Document.Output;
        var panel = result.Document.Panel;
        return panel.Count == 3 &&
               (output.Mode == "nvidia-surround" || output.Mode == "borderless-span") &&
               output.CombinedWidthPx == Screen.width && output.CombinedHeightPx == Screen.height &&
               (long)panel.NativeWidthPx * 3 == Screen.width && panel.NativeHeightPx == Screen.height;
    }
}
