using HarmonyLib;
using Rewired;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Shows the consumed shared binding for an owned joystick, or the native
    /// element name when that joystick's artwork is missing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ControlsRemapper.RefreshDisplayedGlyph</c> looks glyphs up by the
    /// controller's <c>hardwareTypeGuid</c>. Any wheel Rewired does not recognise
    /// has an all-zero GUID, so it matches nothing, falls back to a generic set,
    /// and where that has no entry either assigns a null sprite - drawn as an
    /// empty black box. A binding really did register; it just cannot be read.
    /// </para>
    /// <para>
    /// No new artwork is needed. The same method already drives a text badge for
    /// keyboard bindings:
    /// </para>
    /// <code>
    /// uiData.keyboardGlyph.SetActive(true);
    /// uiData.keyboardText.text = aem.keyboardKeyCode.ToString();
    /// </code>
    /// <para>
    /// A nonnull sprite may also be generic gamepad artwork. An active shared
    /// binding therefore takes precedence for an exactly owned joystick. Its
    /// native Rewired map is not the binding consumed by the shared input sink.
    /// This patch changes only the badge; it never rewrites either mapping.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ControlsRemapper), "RefreshDisplayedGlyph")]
    internal static class GlyphFallback
    {
        [HarmonyPostfix]
        internal static void ShowNameWhenNoGlyph(ControlsRemapper.UIActionSet uiData, InputAction action,
            ActionElementMap aem, Controller ___activeController)
        {
            var cfg = Main.Settings;
            if (!Main.Enabled || cfg == null || !cfg.GlyphTextFallback) return;
            if (uiData == null || aem == null) return;

            try
            {
                // The original method resets activeController to null on its
                // keyboard path. Use that exact selected object, not a fresh
                // last-active lookup or the element map's controller index.
                if (!(___activeController is Joystick) || uiData.glyphImage == null) return;
                if (uiData.keyboardGlyph == null || uiData.keyboardText == null) return;
                bool owned = OwnedPromptGlyph.TryLabel(PadManager.GetPlayer(), ___activeController, action,
                    uiData.isNegativeSplitAxis, out var label);
                if (!owned && uiData.glyphImage.sprite != null) return;

                uiData.glyphImage.enabled = false;
                uiData.keyboardGlyph.SetActive(true);
                uiData.keyboardText.text = owned ? label : Shorten(aem.elementIdentifierName);
                uiData.keyboardText.enabled = true;
            }
            catch
            {
                // Cosmetic only - never break the controls screen over a label.
            }
        }

        /// <summary>
        /// Compresses Rewired's element names to fit a badge sized for "F12".
        /// </summary>
        /// <remarks>
        /// "Button 12" becomes "B12" and "X Axis" becomes "X"; anything else is
        /// truncated. A wheel can report over a hundred buttons, so the long form
        /// would overflow the badge on most of them.
        /// </remarks>
        internal static string Shorten(string name) => PromptGlyphLabel.Element(name);
    }
}
