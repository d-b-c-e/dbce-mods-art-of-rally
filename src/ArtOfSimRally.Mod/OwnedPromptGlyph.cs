using Rewired;

namespace ArtOfSimRally.Mod
{
    // The shared profile's own binding for a prompt or controls row, on the exactly owned joystick only.
    internal static class OwnedPromptGlyph
    {
        internal static bool TryLabel(Player player, Controller controller, InputAction action, bool negative, out string label)
        {
            label = null;
            if (action == null || !(controller is Joystick joystick) || !GameButtonInput.Primary(player) ||
                !GameButtonInput.NativeAction(action.id, negative, out int shared) ||
                !SharedInputStartup.TryGlyphBinding(joystick, shared, out var binding)) return false;
            label = PromptGlyphLabel.Binding(binding);
            return label != null;
        }
    }
}
