namespace ArtOfSimRally.Mod
{
    internal static class SettingsMigration
    {
        public static bool NeedsHandbrakeSplit(Settings cfg) =>
            string.IsNullOrEmpty(cfg.HandbrakeButtonBinding) && WheelInput.Binding.Parse(cfg.HandbrakeBinding)?.IsButton == true;
        public static void SplitHandbrake(Settings cfg)
        {
            if (!NeedsHandbrakeSplit(cfg)) return;
            cfg.HandbrakeButtonBinding = cfg.HandbrakeBinding;
            cfg.HandbrakeBinding = "";
        }

        // Support detail logging is for one reproduction. Left saved on, it kept
        // writing FFB traces for weeks after a user's support request (2026-10-05).
        public static bool EndSessionOnlySettings(Settings cfg)
        {
            if (!cfg.DiagnosticLogging) return false;
            cfg.DiagnosticLogging = false;
            return true;
        }
    }
}
