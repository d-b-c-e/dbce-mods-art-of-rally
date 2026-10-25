using System;
using Dbce.Wheel.Ui;

namespace ArtOfSimRally.Mod
{
    // No device/output calls: view selection can only change presentation fields.
    internal static class SettingsViewPolicy
    {
        public static readonly string[] Pages = { "Controls", "FFB", "Cameras", "Telemetry", "Help" };
        public static bool Advanced(Settings cfg) => cfg.SettingsView == "Advanced";
        // Keep the old persisted values stable: 0 was Setup, 1 Controls ... 5 Help.
        // Setup now opens Controls, while the other saved pages retain their meaning.
        public static int Page(Settings cfg) => cfg.SettingsPage >= 1 && cfg.SettingsPage <= Pages.Length
            ? cfg.SettingsPage - 1 : 0;
        public static bool Select(Settings cfg, bool advanced, int page, bool editing)
        {
            if (editing || page < 0 || page >= Pages.Length) return false;
            cfg.SettingsView = advanced ? "Advanced" : "Simple";
            cfg.SettingsPage = page + 1;
            return true;
        }
        public static bool CustomFfb(Settings c) => c.Smoothing != .2f || c.Invert || c.FyReference != Settings.DefaultFyReference ||
            !c.LandingEffectsEnabled || c.LandingStrength != 5 || c.CrashEffectsEnabled || c.CrashStrength != 50 ||
            c.ShiftEffectsEnabled || c.ShiftStrength != 5;
        public static bool CustomControls(Settings c) => !c.DirectSteering || !c.ZeroAxisDeadzone ||
            !c.BindAnyDevice || !c.GlyphTextFallback || c.DisableSteerAssist;
        public static bool CustomCamera(Settings c) => c.BonnetHeight != .95f || c.BonnetForward != 1f || c.BonnetSide != 0 ||
            c.BonnetPitch != 3 || c.BonnetFOV != 75 || c.BonnetLean != .1f || c.BumperHeight != .45f ||
            c.BumperForward != 1.9f || c.BumperSide != 0 || c.BumperPitch != 2 || c.BumperFOV != 80 ||
            c.CameraMoveStep != .02f || c.CameraTiltStep != 1 || c.CameraFovStep != 2;
        // The footer's save state. Ordinary settings save automatically; shared bindings save only with Save bindings, so
        // an unsaved draft or a binding saved for the next start is said first (Lola 129455; Woden's footer wording). The
        // settings status itself (pending, saved or an error) always stays visible; a plain "Saved" is named "Settings
        // saved" only beside a binding state, so it never reads as if the bindings were saved.
        public static string FooterStatus(string settingsStatus, bool bindingsUnsaved, bool bindingsSavedForNextStart, string bindingSaveFailure = null)
        {
            string bindings = BindingPresentation.Footer(bindingsUnsaved, bindingsSavedForNextStart, bindingSaveFailure);
            if (bindings.Length == 0) return settingsStatus;
            string settings = settingsStatus == "Saved" ? "Settings saved" : settingsStatus;
            return bindings + " · " + settings;
        }
        // The standalone setup route, shown with the shared profile in every state, not only when it is missing (Lola 129479
        // item 3; the common wording of sana 129606/129640, as iRacing 151a131). The in-game editor binds only devices this
        // profile already opened; another device needs the matching standalone Controls Setup, run with the game closed. It
        // never says this mod package bundles the setup, and the missing-profile card keeps the outside-the-game-folder step.
        public const string SetupHeading = "Controls setup";
        public static readonly string[] SetupGuidance = {
            "To add a device, close the game, update the shared profile in Wheelkit, then restart.",
            "In-game binding uses devices already opened by this profile." };
    }

    internal sealed class ConnectionEdit
    {
        public bool Editing { get; private set; }
        public string Host = "", Port = "", Error = "";
        public void Begin(Settings cfg) { Host = cfg.TelemetryHost; Port = cfg.TelemetryPort.ToString(); Error = ""; Editing = true; }
        public void Cancel() { Editing = false; Error = ""; }
        public bool Apply(Settings cfg)
        {
            int port;
            string host = (Host ?? "").Trim();
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown || !int.TryParse(Port, out port) || port < 1 || port > 65535)
            { Error = "Enter a host name or IP address and a port from 1 to 65535."; return false; }
            cfg.TelemetryHost = host; cfg.TelemetryPort = port; Cancel(); return true;
        }
    }
}
