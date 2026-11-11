using ArtOfSimRally.Mod.Shim;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The shared profile's buttons: the game's own actions (menus, change camera, look back, recover car, horn) and the
    /// mod's shortcuts (Settings, Stop FFB, and the F6 panel's navigation), as press, hold and release edges per Unity frame.
    /// </summary>
    /// <remarks>
    /// Values come only from the shared frame accepted in this Unity frame; an action the profile does not own never
    /// presses. Reads are non-consuming, so two game consumers see the same edge. After a start, reconnect, focus change
    /// or settings visit a held button must be released before it presses again, and a failed read never manufactures a
    /// press on recovery. The game loops the horn from GetButtonDown(30) until GetButtonUp(30), so a horn press cut off by
    /// focus, settings, a read failure or a reset still owes the game that release (OweUp).
    /// </remarks>
    internal static class ProfileButtons
    {
        // Shared action indices (ShimFrame.Names) with a consumer in Art. Select (12) has none.
        internal const int Confirm = 9, Back = 10, Start = 11, NavUp = 13, NavDown = 14, NavLeft = 15, NavRight = 16,
            Camera = 17, LookBack = 18, Recover = 19, Horn = 20, ModMenu = 21, MuteFfb = 22;
        private static readonly int[] GameActions = { Confirm, Back, Start, NavUp, NavDown, NavLeft, NavRight, Camera, LookBack, Recover, Horn };
        internal static bool IsGameAction(int action) => System.Array.IndexOf(GameActions, action) >= 0;

        private sealed class State { public bool Armed, Held, Down, Up, OweUp; }
        private static readonly State[] States = NewStates();
        private static State[] NewStates()
        {
            var states = new State[ShimFrame.Count];
            foreach (int action in GameActions) states[action] = new State();
            return states;
        }
        // Shortcut edges: true means held or not yet seen released, so the first held frame is never a press.
        private static readonly bool[] ShortcutHeld = AllHeld(new bool[ShimFrame.Count]);
        private static bool[] AllHeld(bool[] held) { for (int i = 0; i < held.Length; i++) held[i] = true; return held; }
        private static int _frame = -1;
        private static bool _staleLogged;

        /// <summary>This frame's game buttons are projected.</summary>
        internal static bool Current => _frame == Time.frameCount;

        /// <summary>Re-arms every game button (focus change, pause); a held or interrupted horn still owes its release.</summary>
        internal static void Reset()
        {
            foreach (int action in GameActions)
            {
                var state = States[action];
                bool oweUp = action == Horn && (state.Held || state.OweUp);
                state.Armed = state.Held = state.Down = state.Up = false;
                state.OweUp = oweUp;
            }
            _frame = -1;
        }

        /// <summary>The readers closed: game buttons and shortcut edges both start over.</summary>
        internal static void Close() { Reset(); AllHeld(ShortcutHeld); }

        // True when the started session owns the action; its value is then this frame's shared value only.
        private static bool Owned(int action, out bool available, out float value)
        {
            available = false; value = 0f;
            if (!ShimInput.Consumer.Owns(action)) return false;
            WheelReaders.EnsureSharedFrame();
            available = ShimInput.Consumer.Live(action, Time.frameCount);
            value = ShimInput.Consumer.Value(action, Time.frameCount);
            return true;
        }

        /// <summary>One press edge of a mod shortcut or an F6 navigation button; read every frame so an edge is never stale.</summary>
        internal static bool Pressed(int action)
        {
            if (!Owned(action, out bool available, out float value) || !available) { ShortcutHeld[action] = true; return false; }
            bool held = value > .5f;
            bool edge = held && !ShortcutHeld[action];
            ShortcutHeld[action] = held;
            return edge;
        }

        internal static void Tick()
        {
            _frame = Time.frameCount;
            bool eligible = WheelReaders.Enabled && Application.isFocused && !Main.SettingsVisible;
            foreach (int action in GameActions)
            {
                var state = States[action];
                bool available = false, held = false;
                if (Owned(action, out bool live, out float value)) { available = eligible && live; held = available && value > .5f; }
                state.Down = false;
                state.Up = false;
                if (!available)
                {
                    if (state.Held && action == Horn) state.OweUp = true;
                    state.Armed = false; state.Held = false;
                    if (state.OweUp && eligible) { state.Up = true; state.OweUp = false; }
                    continue;
                }
                if (!held) state.Armed = true;
                if (state.Armed) { state.Down = held && !state.Held; state.Up = !held && state.Held; state.Held = held; }
                if (state.OweUp && !state.Held) { state.Up = true; state.OweUp = false; }
            }
        }

        /// <summary>GetButtonDown (down) or GetButton for one of the game's actions, this frame.</summary>
        internal static bool Button(int action, bool down)
        {
            if (!WheelReaders.Enabled || !Application.isFocused || Main.SettingsVisible || !IsGameAction(action)) return false;
            if (_frame != Time.frameCount)
            {
                if (!_staleLogged && WheelReaders.IsOpen && _frame >= 0 && ShimInput.Consumer.Owns(action))
                { _staleLogged = true; ModLog.Warning("Profile action read preceded this frame's input snapshot; button withheld. Check script execution order."); }
                return false;
            }
            var state = States[action];
            return down ? state.Down : state.Held;
        }

        /// <summary>GetButtonUp: the release edge, including a horn release owed after an interrupted press.</summary>
        internal static bool Released(int action)
        {
            if (!WheelReaders.Enabled || !Application.isFocused || Main.SettingsVisible || _frame != Time.frameCount || !IsGameAction(action)) return false;
            return States[action].Up;
        }
    }
}
