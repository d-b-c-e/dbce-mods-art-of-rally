using System;
using Rewired;

namespace ArtOfSimRally.Mod.Shim
{
    // Asks Rewired whether Art's own keyboard map contributed to the car's steering action this frame
    // (sana 35373). Rewired documents IsCurrentInputSource as contributed input this frame, not as "a key
    // is held": two opposing keys that cancel are not claimed either way. Any failure is Unknown, which
    // keeps the wheel's priority.
    internal static class KeyboardSteering
    {
        internal static KeyboardSteer Query(string steerAxis)
        {
            try
            {
                if (string.IsNullOrEmpty(steerAxis) || !ReInput.isReady) return KeyboardSteer.Unknown;
                var player = ReInput.players.GetPlayer(0);
                if (player == null) return KeyboardSteer.Unknown;
                return player.IsCurrentInputSource(steerAxis, ControllerType.Keyboard) ? KeyboardSteer.Present : KeyboardSteer.Absent;
            }
            catch (Exception) { return KeyboardSteer.Unknown; }
        }
    }
}
