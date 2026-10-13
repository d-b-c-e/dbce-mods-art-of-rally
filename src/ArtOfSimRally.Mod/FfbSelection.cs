using System;

namespace ArtOfSimRally.Mod
{
    // Resolve only persisted selections or Core's frozen shared-profile identity. The native toolkit validates presence and
    // FFB capability under strict GUID selection; never fall back by name/index.
    internal static class FfbSelection
    {
        // The shared mode and configured steering identity are frozen at startup, from the same
        // profile bytes later passed to Core. No live setting edit or qualification callback retargets FFB.
        private static bool sharedRequested;
        private static string sharedName, sharedGuid, sharedReason;
        internal static void PrepareShared(bool requested, string name = null, string guid = null, string reason = null)
        {
            sharedRequested = requested; sharedName = name; sharedGuid = guid;
            sharedReason = reason ?? "The shared steering target is unavailable. Check the shared profile and restart.";
        }
        public static bool FollowsSteering(Settings cfg) => cfg.FfbDeviceMode == "steering" ||
            (cfg.FfbDeviceMode != "explicit" && string.IsNullOrEmpty(cfg.PreferredDeviceGuid) &&
             string.IsNullOrEmpty(cfg.PreferredDevice) && cfg.PreferredDeviceIndex < 0);
        public static bool TryTarget(Settings cfg, out string name, out int index, out string guid, out string reason)
        {
            name = cfg.PreferredDevice; index = cfg.PreferredDeviceIndex; guid = cfg.PreferredDeviceGuid;
            reason = "";
            if (FollowsSteering(cfg))
            {
                if (sharedRequested)
                {
                    name = sharedName; index = -1; guid = sharedGuid;
                    Guid sharedIdentity;
                    if (!Guid.TryParse(guid, out sharedIdentity) || sharedIdentity == Guid.Empty)
                    { name = ""; guid = ""; reason = sharedReason; return false; }
                    name = String.IsNullOrWhiteSpace(name) ? "Shared steering wheel (" + sharedIdentity.ToString("D") + ")" : name;
                    guid = sharedIdentity.ToString("D");
                    return true;
                }
                var steering = WheelInput.Binding.Parse(cfg.SteerBinding);
                if (steering == null || steering.IsButton)
                { reason = "Bind Steering in Controls, or select an FFB device explicitly."; return false; }
                name = steering.Device; index = steering.DeviceIndex; guid = steering.InstanceGuid?.ToString("D") ?? "";
            }
            Guid identity;
            if (!Guid.TryParse(guid, out identity) || identity == Guid.Empty)
            { reason = "Saved selection has no verified device identity. Select the wheel again."; return false; }
            return true;
        }
    }
}
