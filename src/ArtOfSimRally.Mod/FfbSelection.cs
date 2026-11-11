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
            (cfg.FfbDeviceMode != "explicit" && string.IsNullOrEmpty(cfg.PreferredDeviceGuid));
        public static bool TryTarget(Settings cfg, out string name, out int index, out string guid, out string reason)
        {
            name = cfg.PreferredDevice; index = cfg.PreferredDeviceIndex; guid = cfg.PreferredDeviceGuid;
            reason = "";
            if (FollowsSteering(cfg))
            {
                // The shared profile's steering wheel; with no profile there is nothing to follow.
                name = ""; index = -1; guid = "";
                if (!sharedRequested) { reason = "No shared profile names a steering wheel. Select the FFB device explicitly."; return false; }
                Guid sharedIdentity;
                if (!Guid.TryParse(sharedGuid, out sharedIdentity) || sharedIdentity == Guid.Empty) { reason = sharedReason; return false; }
                name = String.IsNullOrWhiteSpace(sharedName) ? "Shared steering wheel (" + sharedIdentity.ToString("D") + ")" : sharedName;
                guid = sharedIdentity.ToString("D");
                return true;
            }
            Guid identity;
            if (!Guid.TryParse(guid, out identity) || identity == Guid.Empty)
            { reason = "Saved selection has no verified device identity. Select the wheel again."; return false; }
            return true;
        }
    }
}
