using HarmonyLib;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Applies the shared profile's gears to the player's car once per physics step: H-pattern gates or sequential presses,
    /// through the same <c>Drivetrain.Shift</c> call as the game's own sequential path.
    /// </summary>
    /// <remarks>
    /// Only the profile's own transmission mode applies, never while the gearbox is automatic, and at most one change per
    /// step. The current car's gear-ratio range is checked every time; a missing gear is ignored, never clamped (see
    /// <see cref="Shim.ShimTransmission"/>). Only while the player is driving: the end-of-stage cutscene drives the car
    /// itself, and forcing gears into it would fight it. Gear indices: reverse 0, neutral 1, first 2.
    /// </remarks>
    [HarmonyPatch(typeof(CarDynamics), "FixedUpdate")]
    internal static class ProfileGears
    {
        private static CarDynamics _cachedFor;
        private static Drivetrain _drivetrain;
        private static int _applied = int.MinValue;
        // Start-of-stage evidence (owner 2026-10-31: 1st held at the start still not engaged on rc.19): what the shared
        // lever and the gearbox look like, logged on change, at most 12 lines per stage.
        private static int _diagLeft;
        private static string _lastDiag;

        [HarmonyPostfix]
        private static void ApplyGear(CarDynamics __instance)
        {
            var consumer = Shim.ShimInput.Consumer;
            if (!consumer.Started) return;
            if (!Main.Enabled || Main.Settings == null || !GameState.IsDriving)
            { consumer.ClearShifts(); _applied = int.MinValue; _diagLeft = 12; _lastDiag = null; return; }
            if (!ReferenceEquals(_cachedFor, __instance))
            {
                _cachedFor = __instance; _drivetrain = __instance.GetComponent<Drivetrain>();
                consumer.ClearShifts(); _applied = int.MinValue;
            }
            if (_drivetrain == null) return;
            // A press made in a panel or while unfocused must not shift on return.
            if (Main.SettingsVisible || !UnityEngine.Application.isFocused) { consumer.ClearShifts(); return; }
            WheelReaders.EnsureSharedFrame();
            var mode = WheelReaders.SharedTransmission;
            bool sequential = mode == Shim.ShimTransmission.Mode.Sequential && !_drivetrain.automatic;
            int step = sequential ? consumer.TakeShift() : 0;
            if (!sequential) consumer.ClearShifts();
            var gate = mode == Shim.ShimTransmission.Mode.HPattern ? Shim.ShimTransmission.Gate(consumer, UnityEngine.Time.frameCount) : null;
            int max = _drivetrain.gearRatios != null ? _drivetrain.gearRatios.Length - 1 : -1;
            var target = Shim.ShimTransmission.Next(mode, _drivetrain.automatic, _drivetrain.gear, gate, ref _applied, step, Main.Settings.SkipNeutral, max,
                _drivetrain.changingGear);
            if (target.HasValue) _drivetrain.Shift(target.Value, true);
            if (_diagLeft > 0 && mode == Shim.ShimTransmission.Mode.HPattern)
            {
                bool refused = target.HasValue && !_drivetrain.changingGear && _drivetrain.gear != target.Value;
                float clutch = _drivetrain.clutch != null ? _drivetrain.clutch.GetClutchPosition() : -1f;
                var line = "H-pattern at stage start: lever " + (gate.HasValue ? gate.Value.ToString() : "unread") + ", gearbox " + _drivetrain.gear +
                    (_drivetrain.changingGear ? " (changing)" : "") + ", autoClutch " + _drivetrain.autoClutch + ", clutch " + clutch.ToString("0.00") +
                    (target.HasValue ? ", shift to " + target.Value + (refused ? " refused" : " started") : "") + " (gears: reverse 0, neutral 1, first 2).";
                if (line != _lastDiag) { ModLog.Info(line); _lastDiag = line; _diagLeft--; }
            }
        }
    }
}
