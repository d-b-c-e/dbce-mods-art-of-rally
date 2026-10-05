using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Live camera adjustment by hotkey, persisted back to the mod's settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The right mount differs per car - a Group B monster and a 60s Mini do not
    /// want the same offsets - and the only way to judge it is to look through it
    /// while moving. Editing a config file and restarting for every 2 cm makes
    /// that unusable, so the offsets are nudgeable in place. The keys adjust
    /// whichever mounted view is on screen, bonnet or bumper, each with its own
    /// stored offsets.
    /// </para>
    /// <para>
    /// Saving is debounced rather than immediate: writing the config on every frame
    /// a key is held would hammer the disk. The persistent watchdog saves once idle,
    /// at least a second after the last adjustment, even after leaving this view.
    /// </para>
    /// <para>
    /// Each press moves one step (CameraMoveStep/TiltStep/FovStep, toolkit
    /// STD-006). Holding repeats after <see cref="CameraRepeat.Delay"/> at
    /// <see cref="CameraRepeat.Interval"/>, on real time, so the held rate does not
    /// depend on frame rate and a long frame never produces a burst of steps.
    /// </para>
    /// <para>
    /// Input is read through <c>UnityEngine.Input</c> rather than Rewired, so these
    /// keys sit outside the game's binding system. They can still trigger game
    /// actions on the same key; the numpad defaults reduce that overlap.
    /// </para>
    /// </remarks>
    internal static class CameraTuner
    {
        private static float _saveDueAt;
        private static readonly DeferredSave Save = new DeferredSave();
        private static bool _waitForRelease;
        private static bool _resetButtonPressed;
        internal static void ReadResetButton() => _resetButtonPressed = WheelInput.ShortcutPressed(WheelInput.Channel.CameraReset);

        private static readonly CameraRepeat[] Repeats = CreateRepeats();
        private static CameraRepeat[] CreateRepeats()
        {
            var repeats = new CameraRepeat[10];
            for (int i = 0; i < repeats.Length; i++) repeats[i] = new CameraRepeat();
            return repeats;
        }
        // Any frame the tuner does not read keys ends every hold, so a key still
        // down afterwards starts a fresh press instead of resuming a stale repeat.
        private static void ReleaseRepeats() { foreach (var repeat in Repeats) repeat.Release(); }

        public static void SuppressUntilRelease() { _waitForRelease = true; ReleaseRepeats(); }

        internal static void MarkDirty()
        {
            Save.MarkDirty();
            _saveDueAt = Time.unscaledTime + 1f;
        }

        // Called independently of the mounted camera's LateUpdate. Keep retries
        // pending after failure and perform no persistence in the driving path.
        public static void Flush(bool shutdown = false)
        {
            if (!shutdown && Time.unscaledTime < _saveDueAt) return;
            if (Save.Flush(Time.unscaledTime, !shutdown && Main.Enabled && GameState.IsDriving,
                    shutdown, Main.SaveSettings))
                ModLog.Info("Camera settings saved.");
        }

        /// <summary>
        /// Polls adjustment keys for the given view. Called from the camera's
        /// LateUpdate patch, so it only runs while that view is actually active.
        /// </summary>
        public static void Update(BonnetCamera.View view)
        {
            bool resetButton = _resetButtonPressed; _resetButtonPressed = false;
            var cfg = Main.Settings;
            if (!Main.Enabled || cfg == null || !cfg.CameraTuningKeys || view == BonnetCamera.View.None)
            {
                ReleaseRepeats();
                return;
            }
            if (Main.SettingsVisible || !Application.isFocused || CameraKeys.Listening >= 0 || CameraKeys.ModifierHeld())
            {
                SuppressUntilRelease();
                return;
            }
            // A captured key or the key that closed the panel must be released
            // before it can adjust/reset a mount on the next LateUpdate.
            if (_waitForRelease)
            {
                if (Input.anyKey || CameraKeys.AnyButtonHeld) return;
                _waitForRelease = false;
            }

            float now  = Time.unscaledTime;
            float move = CameraRepeat.Bounded(cfg.CameraMoveStep, .005f, .25f, .02f);
            float tilt = CameraRepeat.Bounded(cfg.CameraTiltStep, .1f, 10f, 1f);
            float fov  = CameraRepeat.Bounded(cfg.CameraFovStep, .5f, 10f, 2f);

            bool changed = false;
            bool bumper = view == BonnetCamera.View.Bumper;

            if (bumper)
            {
                changed |= Nudge(ref cfg.BumperHeight, 0, 1, move, now);
                changed |= Nudge(ref cfg.BumperForward, 2, 3, move, now);
                changed |= Nudge(ref cfg.BumperSide, 5, 4, move, now);
                changed |= Nudge(ref cfg.BumperPitch, 6, 7, tilt, now);
                changed |= Nudge(ref cfg.BumperFOV, 8, 9, fov, now);
            }
            else
            {
                changed |= Nudge(ref cfg.BonnetHeight, 0, 1, move, now);
                changed |= Nudge(ref cfg.BonnetForward, 2, 3, move, now);
                changed |= Nudge(ref cfg.BonnetSide, 5, 4, move, now);
                changed |= Nudge(ref cfg.BonnetPitch, 6, 7, tilt, now);
                changed |= Nudge(ref cfg.BonnetFOV, 8, 9, fov, now);
            }

            if (Input.GetKeyDown(cfg.KeyReset) || resetButton)
            {
                // A fresh instance carries the field initialisers, which are the
                // single source of truth for defaults now that there is no config
                // framework holding them separately.
                cfg.ResetCameraMount(bumper);
                changed = true;
            }

            if (changed) MarkDirty();
        }

        private static bool Nudge(ref float value, int increase, int decrease, float step, float now)
        {
            // Tick both every frame so each key's press/hold state stays current.
            bool up = Repeats[increase].Tick(CameraKeys.Held(increase), now);
            bool down = Repeats[decrease].Tick(CameraKeys.Held(decrease), now);
            float delta = (up ? step : 0f) - (down ? step : 0f);
            if (delta == 0f) return false;

            value += delta;
            return true;
        }
    }

    /// <summary>One step on press, then bounded repeat while held (toolkit STD-006).</summary>
    internal sealed class CameraRepeat
    {
        public const float Delay = .35f, Interval = .1f;
        private bool _held;
        private float _next;

        public void Release() => _held = false;

        public bool Tick(bool held, float now)
        {
            if (!held) { _held = false; return false; }
            if (!_held) { _held = true; _next = now + Delay; return true; }
            if (now < _next) return false;
            // Keep the cadence, but after a stall resume one interval from now
            // rather than replaying the missed steps.
            _next += Interval;
            if (_next <= now) _next = now + Interval;
            return true;
        }

        public static float Bounded(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
