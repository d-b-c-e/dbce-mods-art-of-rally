using UnityEngine;

namespace ArtOfSimRally.Mod.Shim
{
    internal static class ShimInput
    {
        // Deliberately dormant until an explicitly prepared shared-handle bridge
        // is attached. Native-source isolation has no production bootstrap yet.
        internal static readonly ShimAxisConsumer Consumer = new ShimAxisConsumer();
        // Future adapter stamps this only after identity-based Rewired isolation
        // has removed cached wheel values. Default refuses all native mixing.
        internal static int IsolatedFrame = -1;

        // The last shared composition, stamped with its Unity frame: the shared applied values, the game's
        // steering-alignment term and what GetInput returned. A drive probe pairs it with the car's fields.
        internal struct Composition
        {
            internal int Frame;
            internal bool SteerLive;
            internal float AppliedSteer, AppliedThrottle, AppliedBrake, AppliedHandbrake, Alignment;
            internal float Steer, Throttle, Brake, Handbrake;
            internal float AppliedClutch, Clutch;
            internal KeyboardSteer Keyboard;
            internal bool KeyboardSteered;
        }
        internal static Composition Last;

        internal static bool TryOverride(AxisCarController car,
            ref float throttle, ref float brake, ref float steer,
            ref float handbrake, ref float clutch, ref bool startEngine)
        {
            if (!Consumer.Started || !Main.Enabled) return false;
            // Match the existing direct-input handoff to the stock finishing driver.
            try
            {
                var manager = GameState.ExistingManager;
                if (manager == null || manager.status == EventStatusEnums.EventStatus.FINISHING_STAGE_ANIMATION) return true;
            }
            catch { return true; }
            // The car can read input before the watchdog runs; publish this frame's shared
            // snapshot first so Compose never sees a previous frame as current.
            WheelInput.EnsureSharedFrame();
            var values = new DrivingValues { Throttle = throttle, Brake = brake, Steer = steer,
                Handbrake = handbrake, Clutch = clutch, StartEngine = startEngine };
            int frame = Time.frameCount;
            bool isolated = IsolatedFrame == frame;
            // Ordinary input only: a latched session or a frame without isolation proof never asks.
            var keyboard = isolated && !Consumer.LatchedDelivery(frame) ? KeyboardSteering.Query(car.steerAxis) : KeyboardSteer.NotQueried;
            Consumer.Compose(frame, isolated, car.SteeringOutOfAlignmentEffect, ref values, keyboard);
            // Art-only: the handbrake button keeps Art's binding on this frame's shared copy.
            // A latched session's injected actions stay the only input.
            if (!Consumer.LatchedDelivery(frame))
                values.Handbrake = Mathf.Max(values.Handbrake, WheelInput.ArtOnlyValue(WheelInput.Channel.HandbrakeButton));
            throttle = values.Throttle; brake = values.Brake; steer = values.Steer;
            handbrake = values.Handbrake; clutch = values.Clutch; startEngine = values.StartEngine;
            Last = new Composition { Frame = frame, SteerLive = Consumer.Live(0, frame), Alignment = car.SteeringOutOfAlignmentEffect,
                Keyboard = keyboard, KeyboardSteered = Consumer.KeyboardSteered,
                AppliedSteer = Consumer.Applied(0, frame), AppliedThrottle = Consumer.Applied(1, frame), AppliedBrake = Consumer.Applied(2, frame), AppliedHandbrake = Consumer.Applied(4, frame),
                Steer = steer, Throttle = throttle, Brake = brake, Handbrake = handbrake, AppliedClutch = Consumer.Applied(3, frame), Clutch = clutch };
            return true;
        }
    }
}
