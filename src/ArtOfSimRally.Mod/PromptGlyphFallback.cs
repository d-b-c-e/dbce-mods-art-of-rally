using System.Collections.Generic;
using HarmonyLib;
using Rewired;
using UnityEngine.UI;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// KI-46: menu prompts (e.g. "next weather") showed a keyboard badge and a PlayStation symbol on top of each
    /// other with a wheel, instead of the wheel's button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ControllerButtonDisplay.SetupGlpyh</c> (sic) handles a joystick by switching the text off and asking
    /// <c>ControllerGlyphs</c> for artwork by <c>hardwareTypeGuid</c>. A wheel Rewired does not recognise has no
    /// artwork, and the method then changes nothing else: the image keeps the sprite it last showed (a gamepad
    /// glyph) and the keyboard badge stays as the keyboard left it, so both draw at once.
    /// </para>
    /// <para>
    /// Same remedy as <see cref="GlyphFallback"/> on the controls screen: where no artwork exists, hide the stale
    /// image and write the bound element's short name ("B12") into the keyboard badge the prompt already lays
    /// out. Controllers with real artwork keep it. The element is chosen exactly as the game chose it.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ControllerButtonDisplay), "SetupGlpyh")]
    internal static class PromptGlyphFallback
    {
        // UpdateGlyph runs every frame in menus; reuse one list (main thread only).
        private static readonly List<ActionElementMap> Maps = new List<ActionElementMap>();

        [HarmonyPostfix]
        private static void ShowNameWhenNoGlyph(Player p, Controller controller, InputAction action,
            ControllerButtonDisplay.AxisPolarity ___axisPolarity, Image ___displayImage, Text ___displayText,
            Image ___keyboardImage, ControllerGlyphs ___controllerGlyphs)
        {
            var cfg = Main.Settings;
            if (!Main.Enabled || cfg == null || !cfg.GlyphTextFallback) return;
            try
            {
                var joystick = controller as Joystick;
                if (p == null || action == null || joystick == null || ___displayImage == null || ___displayText == null || ___keyboardImage == null) return;
                var aem = Pick(p, controller, action, ___axisPolarity);
                if (aem == null) return;
                if (___controllerGlyphs != null && ___controllerGlyphs.GetGlyph(joystick.hardwareTypeGuid, aem.elementIdentifierId, aem.axisRange) != null) return;

                ___displayImage.enabled = false;
                ___keyboardImage.enabled = true;
                ___displayText.text = GlyphFallback.Shorten(aem.elementIdentifierName);
                ___displayText.enabled = true;
            }
            catch
            {
                // Cosmetic only - never break a menu over a label.
            }
        }

        // The game's own element choice in SetupGlpyh, including its fallback to the last mapped element.
        private static ActionElementMap Pick(Player p, Controller controller, InputAction action, ControllerButtonDisplay.AxisPolarity polarity)
        {
            var maps = p.controllers.maps;
            ActionElementMap chosen = null;
            bool matched = false;
            Maps.Clear();
            switch (polarity)
            {
                case ControllerButtonDisplay.AxisPolarity.NONE:
                    chosen = maps.GetFirstElementMapWithAction(controller, action.id, true);
                    break;
                case ControllerButtonDisplay.AxisPolarity.NEGATIVE:
                case ControllerButtonDisplay.AxisPolarity.POSITIVE:
                    var pole = polarity == ControllerButtonDisplay.AxisPolarity.NEGATIVE ? Pole.Negative : Pole.Positive;
                    maps.GetElementMapsWithAction(controller, action.id, true, Maps);
                    foreach (var map in Maps)
                        if (map.axisContribution == pole) { chosen = map; matched = true; }
                    break;
            }
            if (!matched)
            {
                maps.GetElementMapsWithAction(controller, action.id, true, Maps);
                foreach (var map in Maps) chosen = map;
            }
            return chosen;
        }
    }
}
