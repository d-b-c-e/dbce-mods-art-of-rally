using System.Collections.Generic;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class WheelInput
    {
        // One snapshot/edge per watchdog frame. Reads are non-consuming so two
        // game consumers see the same edge. Startup/reconnect/focus/edit requires
        // release first; failed reads never manufacture a press on recovery.
        // The game loops the horn from GetButtonDown(30) until GetButtonUp(30), so a horn press cut off by
        // focus, settings, a read failure or a reset still owes the game that release (OweUp).
        private sealed class ButtonState { public bool Armed, Held, Down, Up, OweUp; }
        private static readonly Dictionary<Channel, ButtonState> GameButtons = new Dictionary<Channel, ButtonState>();
        private static int _buttonFrame = -1;
        private static bool _staleLogged;
        internal static void ResetGameButtons()
        {
            bool oweHorn = GameButtons.TryGetValue(Channel.Horn, out var horn) && (horn.Held || horn.OweUp);
            GameButtons.Clear(); _buttonFrame = -1;
            if (oweHorn) GameButtons[Channel.Horn] = new ButtonState { OweUp = true };
        }
        private static void TickGameButtons()
        {
            _buttonFrame = Time.frameCount;
            foreach (var channel in Channels)
            {
                if (!IsGameButton(channel)) continue;
                if (!GameButtons.TryGetValue(channel, out var state)) GameButtons[channel] = state = new ButtonState();
                bool eligible = Enabled && Application.isFocused && !Main.SettingsVisible && !_assigning.HasValue;
                bool available, held;
                if (SharedChannel(channel, out bool sharedAvailable, out float sharedValue))
                { available = eligible && sharedAvailable; held = available && sharedValue > .5f; }
                else if (Shim.ShimInput.Consumer.Started && IsArtOnlyGameButton(channel))
                {
                    // Beside a started shared profile: this frame's shared copy, never a control the profile maps.
                    available = eligible && _bindings.TryGetValue(channel, out var binding) && SharedDuplicate(channel) == null && Resolve(binding)?.Ok == true;
                    held = available && ArtOnlyValue(channel) > .5f;
                }
                else
                {
                    available = eligible && _bindings.TryGetValue(channel, out var binding) && Resolve(binding)?.Ok == true;
                    held = available && Value(channel) > .5f;
                }
                state.Down = false;
                state.Up = false;
                if (!available)
                {
                    if (state.Held && channel == Channel.Horn) state.OweUp = true;
                    state.Armed = false; state.Held = false;
                    if (state.OweUp && eligible) { state.Up = true; state.OweUp = false; }
                    continue;
                }
                if (!held) state.Armed = true;
                if (state.Armed) { state.Down = held && !state.Held; state.Up = !held && state.Held; state.Held = held; }
                if (state.OweUp && !state.Held) { state.Up = true; state.OweUp = false; }
            }
        }
        internal static bool GameButton(Channel channel, bool down)
        {
            if (!Enabled || !Application.isFocused || Main.SettingsVisible || _assigning.HasValue) return false;
            if (_buttonFrame != Time.frameCount)
            {
                if (!_staleLogged && _open && _buttonFrame >= 0 && (IsBound(channel) || SharedOwns(channel)))
                { _staleLogged = true; ModLog.Warning("Profile action read preceded this frame's input snapshot; button withheld. Check script execution order."); }
                return false;
            }
            return GameButtons.TryGetValue(channel, out var state) && (down ? state.Down : state.Held);
        }
        internal static bool GameButtonReleased(Channel channel)
        {
            if (!Enabled || !Application.isFocused || Main.SettingsVisible || _assigning.HasValue || _buttonFrame != Time.frameCount) return false;
            return GameButtons.TryGetValue(channel, out var state) && state.Up;
        }
    }
}
