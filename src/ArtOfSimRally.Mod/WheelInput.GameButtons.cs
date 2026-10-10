using System.Collections.Generic;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class WheelInput
    {
        // One snapshot/edge per watchdog frame. Reads are non-consuming so two
        // game consumers see the same edge. Startup/reconnect/focus/edit requires
        // release first; failed reads never manufacture a press on recovery.
        private sealed class ButtonState { public bool Armed, Held, Down; }
        private static readonly Dictionary<Channel, ButtonState> GameButtons = new Dictionary<Channel, ButtonState>();
        private static int _buttonFrame = -1;
        private static bool _staleLogged;
        internal static void ResetGameButtons() { GameButtons.Clear(); _buttonFrame = -1; }
        private static void TickGameButtons()
        {
            _buttonFrame = Time.frameCount;
            foreach (var channel in Channels)
            {
                if (!IsGameButton(channel)) continue;
                if (!GameButtons.TryGetValue(channel, out var state)) GameButtons[channel] = state = new ButtonState();
                bool available = Enabled && Application.isFocused && !Main.SettingsVisible && !_assigning.HasValue &&
                    _bindings.TryGetValue(channel, out var binding) && Resolve(binding)?.Ok == true;
                bool held = available && Value(channel) > .5f;
                state.Down = false;
                if (!available) { state.Armed = false; state.Held = false; continue; }
                if (!held) state.Armed = true;
                if (state.Armed) { state.Down = held && !state.Held; state.Held = held; }
            }
        }
        internal static bool GameButton(Channel channel, bool down)
        {
            if (!Enabled || !Application.isFocused || Main.SettingsVisible || _assigning.HasValue) return false;
            if (_buttonFrame != Time.frameCount)
            {
                if (!_staleLogged && _buttonFrame >= 0 && IsBound(channel))
                { _staleLogged = true; ModLog.Warning("Profile action read preceded this frame's input snapshot; button withheld. Check script execution order."); }
                return false;
            }
            return GameButtons.TryGetValue(channel, out var state) && (down ? state.Down : state.Held);
        }
    }
}
