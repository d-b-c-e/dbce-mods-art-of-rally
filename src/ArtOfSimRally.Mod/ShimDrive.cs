using System;
using ArtOfSimRally.Mod.Shim;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Drive probe glue (diagnostic only, never in a normal launch): observes the drive fences in the
    // running game and, once attached, samples the player car after each CarController.Update, where the
    // game has called GetInput (our shared composition) and stored the inputs it drives with.
    internal static class ShimDrive
    {
        private const string HarmonyId = "ArtOfSimRally.Mod.DriveProbe";
        private static Harmony harmony;
        private static DriveTrial trial;

        internal static DriveTrial.Fences ObserveFences(bool noForceArmed) => new DriveTrial.Fences
        {
            NoForceArmed = noForceArmed,
            SettingsWritesSuppressed = !Main.SaveSettings(), // the probe's settings guard answers without writing
            TelemetryIdle = TelemetryPump.ActiveEndpoint == null,
            MissingHooks = InputHooks.Missing(),
            MissingGuards = InputHooks.MissingCaptureGuards(),
            Compatible = GameButtonCompatibility.Allowed,
            Custom = GameModeManager.GameMode == GameModeManager.GAME_MODES.CUSTOM,
            StageReady = StageReady(),
            Focused = Application.isFocused,
        };

        // The car is held at the start (WAITING_TO_BEGIN) or, if the countdown already ended, stationary.
        private static bool StageReady()
        {
            var manager = GameState.ExistingManager;
            var car = manager?.playerManager?.carDynamics;
            if (car == null) return false;
            if (manager.status == EventStatusEnums.EventStatus.WAITING_TO_BEGIN) return true;
            var body = car.GetComponent<Rigidbody>();
            return manager.status == EventStatusEnums.EventStatus.UNDERWAY && body != null && body.velocity.magnitude < .5f;
        }
        // The event this drive runs in is still loaded.
        internal static bool InStage => GameState.ExistingManager?.playerManager?.carDynamics != null;

        internal static void Begin(DriveTrial drive)
        {
            trial = drive;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(CarController), "Update"), postfix: new HarmonyMethod(typeof(ShimDrive), nameof(AfterCarUpdate)));
        }
        internal static void End()
        {
            try { harmony?.UnpatchAll(HarmonyId); } catch (Exception ex) { ModLog.Error("Drive probe unpatch: " + ex.Message); }
            harmony = null;
        }

        private static void AfterCarUpdate(CarController __instance)
        {
            if (trial == null || trial.Finished) return;
            try
            {
                var manager = GameState.ExistingManager;
                var car = manager?.playerManager?.carDynamics;
                if (car == null || __instance.gameObject != car.gameObject) return;
                var drivetrain = __instance.GetComponent<Drivetrain>();
                var body = __instance.GetComponent<Rigidbody>();
                var composed = ShimInput.Last;
                int frame = Time.frameCount;
                var consumer = ShimInput.Consumer;
                var sample = new DriveTrial.Sample
                {
                    Frame = frame,
                    Underway = manager.status == EventStatusEnums.EventStatus.UNDERWAY,
                    Composed = composed.Frame == frame,
                    SteerLive = composed.SteerLive,
                    AppliedSteer = composed.AppliedSteer, AppliedThrottle = composed.AppliedThrottle, AppliedBrake = composed.AppliedBrake,
                    AppliedHandbrake = composed.AppliedHandbrake, Alignment = composed.Alignment,
                    OutSteer = composed.Steer, OutThrottle = composed.Throttle, OutBrake = composed.Brake, OutHandbrake = composed.Handbrake,
                    EngineSteer = __instance.steerInput, EngineThrottle = __instance.throttleInput,
                    EngineBrake = __instance.brakeInput, EngineHandbrake = __instance.handbrakeInput,
                    Gear = drivetrain != null ? drivetrain.gear : -1,
                    Automatic = drivetrain != null && drivetrain.automatic,
                    ChangingGear = drivetrain != null && drivetrain.changingGear,
                    Speed = body != null ? body.velocity.magnitude : 0f,
                };
                foreach (int action in DriveTrial.Delivered) sample.Injected[action] = consumer.Injected(action, frame);
                trial.Tick(sample);
            }
            catch (Exception ex) { ModLog.Error("Drive probe sample: " + ex.Message); }
        }
    }
}
