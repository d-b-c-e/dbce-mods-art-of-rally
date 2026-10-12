namespace ArtOfSimRally.Mod
{
    internal static class FfbController { public static void Reset() { } }
    // FfbNative asks the diagnostic probe for a log path (a03fb51); no probe runs in these fixtures.
    internal static class ShimStartup { internal static string NativeLogPath => null; }
    internal static class ModLog
    {
        public static void Info(string message) { }
        public static void Warning(string message) { }
        public static void Error(string message) { }
    }
}

namespace ArtOfSimRally.Mod
{
    internal static partial class WheelInput
    {
        private static readonly string[] AxisNames = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
    }
}
