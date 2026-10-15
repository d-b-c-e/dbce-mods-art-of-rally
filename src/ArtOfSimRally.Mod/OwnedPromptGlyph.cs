using Rewired;

namespace ArtOfSimRally.Mod
{
    internal static class OwnedPromptGlyph
    {
        internal static bool TryLabel(Player player, Controller controller, InputAction action, bool negative, out string label)
        {
            label = null;
            if (action == null || !(controller is Joystick joystick) || !GameButtonInput.Primary(player) ||
                !GameButtonInput.NativeAction(action.id, negative, out var sharedId, out var artChannel)) return false;
            if (sharedId != null && SharedInputStartup.TryGlyphBinding(joystick, sharedId, out var binding))
            { label = PromptGlyphLabel.Binding(binding); return label != null; }
            return artChannel.HasValue && WheelInput.TryGlyphFallback(joystick, sharedId, artChannel.Value, out label);
        }
    }
}
