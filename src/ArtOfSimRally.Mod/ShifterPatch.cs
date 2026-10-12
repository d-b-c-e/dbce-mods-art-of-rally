using HarmonyLib;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Applies the separate shifter's selected gear to the car each physics step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on <c>CarDynamics.FixedUpdate</c> alongside force feedback and
    /// telemetry, so gear selection is sampled at the physics rate rather than the
    /// frame rate.
    /// </para>
    /// <para>
    /// Only while the player is driving: the end-of-stage cutscene drives the car
    /// itself, and forcing gears into an AI-driven car would fight it - the same
    /// mistake that had the wheel pulling to full lock after the finish line.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(CarDynamics), "FixedUpdate")]
    internal static class ShifterPatch
    {
        private static CarDynamics _cachedFor;
        private static Drivetrain _drivetrain;

        private static int _sharedApplied = int.MinValue;

        [HarmonyPostfix]
        private static void ApplyGear(CarDynamics __instance)
        {
            // A started shared session owns transmission input; Art's own shifter
            // reader is never opened alongside it, so the two never both apply.
            if (Shim.ShimInput.Consumer.Started) { ApplySharedGear(__instance); return; }
            var cfg = Main.Settings;
            if (!Main.Enabled || cfg == null || !cfg.ShifterEnabled) return;
            if (!Shifter.IsOpen) return;

            if (!GameState.IsDriving) { Shifter.Reset(); return; }

            if (!ReferenceEquals(_cachedFor, __instance))
            {
                _cachedFor = __instance;
                _drivetrain = __instance.GetComponent<Drivetrain>();
                Shifter.Reset();
            }

            Shifter.Update(_drivetrain);
        }

        // Shared gates and sequential presses, through the same Drivetrain.Shift call as
        // Art's shifter and the game's own sequential path. The current car's gear-ratio
        // range is checked every time; a missing gear is ignored, never clamped.
        private static void ApplySharedGear(CarDynamics car)
        {
            var consumer = Shim.ShimInput.Consumer;
            if (!Main.Enabled || Main.Settings == null || !GameState.IsDriving)
            { consumer.ClearShifts(); _sharedApplied = int.MinValue; return; }
            if (!ReferenceEquals(_cachedFor, car))
            {
                _cachedFor = car; _drivetrain = car.GetComponent<Drivetrain>();
                consumer.ClearShifts(); _sharedApplied = int.MinValue;
            }
            if (_drivetrain == null) return;
            // A press made in a panel or while unfocused must not shift on return.
            if (Main.SettingsVisible || !UnityEngine.Application.isFocused) { consumer.ClearShifts(); return; }
            WheelInput.EnsureSharedFrame();
            int max = _drivetrain.gearRatios != null ? _drivetrain.gearRatios.Length - 1 : -1;
            int frame = UnityEngine.Time.frameCount;
            var gate = Shim.ShimTransmission.Gate(consumer, frame);
            if (gate.HasValue && gate.Value != _sharedApplied)
            {
                _sharedApplied = gate.Value;
                if (Shim.ShimTransmission.Exists(gate.Value, max)) _drivetrain.Shift(gate.Value, true);
            }
            var step = Shim.ShimTransmission.Step(_drivetrain.gear, consumer.TakeShift(), Main.Settings.SkipNeutral, max);
            if (step.HasValue) _drivetrain.Shift(step.Value, true);
        }
    }
}
