using System;

namespace ArtOfSimRally.Mod
{
    // A normal build (ArtShimProbe=false, see the project file) carries no shim
    // probe. This facade keeps ordinary startup and input exactly as they are
    // when no probe is requested; a probe request set for this build is
    // reported once and otherwise ignored.
    internal static class ShimStartup
    {
        internal static bool Requested => false;
        internal static string NativeLogPath => null;
        internal static bool Prepare(string modDirectory)
        {
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBCE_ART_SHIM_REQUEST")))
                ModLog.Warning("Shared input probe requested, but this build carries no shim probe; starting normally.");
            return true;
        }
        internal static bool Arm() => true;
        internal static void Tick() { }
        internal static void Shutdown() { }
    }
}
