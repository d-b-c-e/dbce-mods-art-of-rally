using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Shim
{
    // What the settings panel says about the opt-in shared profile. Pure text from observed facts,
    // so every state, refusal and transmission mismatch is stated rather than silently ignored.
    internal static class SharedInputStatus
    {
        internal enum Phase { Off, Waiting, Isolating, Attached, Refused, CleanupFailed }
        internal sealed class Facts
        {
            internal Phase Phase;
            internal string Reason, WaitingFor, ProfileName, Transmission, ArtTransmissionMode, StopReason, RestoreStatus;
            internal string[] Devices;
            internal bool Stopped, Retrying, KeyboardRetained;
            internal bool? CarAutomatic { get; set; } // the current car while driving; null otherwise
        }
        // Select has no Art action, so its shared row is hidden rather than described here.
        internal const string ArtOnly = "Camera tuning keys and buttons keep Art bindings; so do the handbrake button, repair puncture, quick restart and look left, right and forward while Use assigned controls is On. An Art binding on a control the shared profile maps is not used.";
        internal const string Editors = "Shared controls are edited under Shared profile (Bind, Clear, Save bindings) and apply after a restart; the handbrake, camera, repair, restart and look-direction buttons stay editable here. Plugging in or changing devices also needs a restart.";
        // After a stop the car gets no driving input at all: the game's combined values cannot separate
        // keyboard from a re-enabled wheel. Menus keep the keyboard.
        internal const string StoppedEffect = ". Driving input, wheel and keyboard alike, stays neutral until the game restarts; menus still take the keyboard.";
        // A transport-only stop while the wheel is still verifiably isolated.
        internal const string StoppedKeyboardEffect = ". The wheel stays neutral until the game restarts; while it stays isolated, keyboard driving continues.";
        // Existing attended force/performance acceptance is retained; see
        // docs/2026-10-15-f6-ui-owner-delivery.md and docs/SHARED-BINDING-EDITOR.md.
        // Do not turn muted editor runs or new device cases into broader force assurances.
        internal static string[] Lines(Facts f, bool details = true)
        {
            var lines = new List<string>();
            switch (f.Phase)
            {
                case Phase.Off: lines.Add("Shared profile is off; Art bindings are used."); break;
                case Phase.Refused:
                    // SharedInputStartup preserves the qualification reason. Only this exact identity
                    // refusal gets the workaround; other failures and incomplete cleanup keep their text.
                    if (f.Reason != null && f.Reason.StartsWith("Rewired joystick identity unavailable:", System.StringComparison.Ordinal))
                    {
                        lines.Add("Shared profile not used for this session: a controller could not be identified, so the mod cannot rule out duplicate wheel input. Art bindings are used.");
                        lines.Add("Start the game without that controller connected, or, if you use Steam Input, disable it for art of rally, then restart the game. Steam Input is one possible cause.");
                        lines.Add("Details: " + f.Reason);
                    }
                    else lines.Add("Shared profile not used: " + f.Reason + " Art bindings are used. Restart to try again.");
                    break;
                case Phase.CleanupFailed:
                    lines.Add("Shared profile not used: " + f.Reason + " The game's own input for the isolated wheel is still off (" + f.RestoreStatus + "); " +
                        (f.Retrying ? "retrying in the menus." : "restart the game to restore it.") + " Art's shifter stays closed until then.");
                    break;
                case Phase.Waiting: lines.Add("Shared profile: waiting in the menus for " + (string.IsNullOrEmpty(f.WaitingFor) ? "the wheel readers" : f.WaitingFor) + "."); if (details) lines.Add(Editors); break;
                case Phase.Isolating: lines.Add("Shared profile: isolating the wheel from the game's own input."); if (details) lines.Add(Editors); break;
                case Phase.Attached:
                    if (f.Stopped)
                    {
                        lines.Add("Shared input stopped: " + f.StopReason + (f.KeyboardRetained ? StoppedKeyboardEffect : StoppedEffect));
                        if (details) lines.Add(Editors);
                        break;
                    }
                    if (!details)
                    {
                        lines.Add("Shared profile '" + (string.IsNullOrEmpty(f.ProfileName) ? "unnamed" : f.ProfileName) + "' is active. Saved bindings apply after a restart.");
                        break;
                    }
                    lines.Add("Shared profile '" + (string.IsNullOrEmpty(f.ProfileName) ? "unnamed" : f.ProfileName) + "' drives the wheel, pedals, menu buttons, look back, recover car, horn, shortcuts and gears. Keyboard stays native.");
                    if (f.Devices != null && f.Devices.Length > 0) lines.Add("Isolated from the game's own input: " + string.Join(", ", f.Devices) + ".");
                    lines.Add(ArtOnly);
                    lines.Add(Gears(f.Transmission, f.ArtTransmissionMode, f.CarAutomatic));
                    lines.Add(Editors);
                    break;
            }
            return lines.ToArray();
        }

        // Profile transmission against Art's gearbox switch and the current car: never claim gears work
        // while the gearbox is automatic.
        internal static string Gears(string transmission, string artMode, bool? carAutomatic)
        {
            var mode = ShimTransmission.ParseMode(transmission);
            if (mode == ShimTransmission.Mode.None)
                return string.IsNullOrEmpty(transmission) ? "The profile names no transmission: shared gear actions are not used."
                    : "Profile transmission is " + transmission + ": shared gear actions are not used.";
            if (carAutomatic == true) return "This car's gearbox is automatic now: shared " + transmission + " gear actions are ignored. Choose " + transmission + " under Transmission.";
            if (artMode == "Automatic") return "Profile is " + transmission + " but Transmission is Automatic: shared gear actions are ignored. Choose " + transmission + " under Transmission.";
            if (artMode == "Follow game") return "Shared " + transmission + " gear actions apply only while the game's own transmission option is manual. Choose " + transmission + " under Transmission to make it explicit.";
            return "Gears follow the profile (" + transmission + "); the gearbox is manual.";
        }
    }
}
