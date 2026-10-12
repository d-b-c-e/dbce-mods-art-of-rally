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
            Consumer.Compose(Time.frameCount, IsolatedFrame == Time.frameCount,
                car.SteeringOutOfAlignmentEffect, ref values);
            throttle = values.Throttle; brake = values.Brake; steer = values.Steer;
            handbrake = values.Handbrake; clutch = values.Clutch; startEngine = values.StartEngine;
            return true;
        }
    }
}
