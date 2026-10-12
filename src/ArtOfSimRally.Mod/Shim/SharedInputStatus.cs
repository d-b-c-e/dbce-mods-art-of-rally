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
            internal bool Stopped, Retrying;
            internal bool? CarAutomatic { get; set; } // the current car while driving; null otherwise
        }
        internal const string Unsupported = "Not available in Art: select, look back, reset and horn.";
        internal const string ArtOnly = "Camera tuning keys and buttons keep Art bindings, and so does the handbrake button while Use assigned controls is On.";
        internal const string Editors = "Art's editors for shared controls are hidden; the handbrake and camera buttons stay editable. Turn the shared profile off and restart to edit the rest. Plugging in or changing devices also needs a restart.";
        // After a stop the car gets no driving input at all: the game's combined values cannot separate
        // keyboard from a re-enabled wheel. Menus keep the keyboard.
        internal const string StoppedEffect = ". Driving input, wheel and keyboard alike, stays neutral until the game restarts; menus still take the keyboard.";
        internal const string Ffb = "Force feedback together with the shared profile is not qualified yet; treat the first drive as an attended test.";

        internal static string[] Lines(Facts f)
        {
            var lines = new List<string>();
            switch (f.Phase)
            {
                case Phase.Off: lines.Add("Shared Wheelkit profile is off; Art bindings are used."); break;
                case Phase.Refused: lines.Add("Shared profile not used: " + f.Reason + " Art bindings are used. Restart to try again."); break;
                case Phase.CleanupFailed:
                    lines.Add("Shared profile not used: " + f.Reason + " The game's own input for the isolated wheel is still off (" + f.RestoreStatus + "); " +
                        (f.Retrying ? "retrying in the menus." : "restart the game to restore it.") + " Art's shifter stays closed until then.");
                    break;
                case Phase.Waiting: lines.Add("Shared profile: waiting in the menus for " + (string.IsNullOrEmpty(f.WaitingFor) ? "the wheel readers" : f.WaitingFor) + "."); lines.Add(Editors); break;
                case Phase.Isolating: lines.Add("Shared profile: isolating the wheel from the game's own input."); lines.Add(Editors); break;
                case Phase.Attached:
                    if (f.Stopped)
                    {
                        lines.Add("Shared input stopped: " + f.StopReason + StoppedEffect);
                        lines.Add(Editors);
                        break;
                    }
                    lines.Add("Shared profile '" + (string.IsNullOrEmpty(f.ProfileName) ? "unnamed" : f.ProfileName) + "' drives the wheel, pedals, menu buttons, shortcuts and gears. Keyboard stays native.");
                    if (f.Devices != null && f.Devices.Length > 0) lines.Add("Isolated from the game's own input: " + string.Join(", ", f.Devices) + ".");
                    lines.Add(Unsupported); lines.Add(ArtOnly);
                    lines.Add(Gears(f.Transmission, f.ArtTransmissionMode, f.CarAutomatic));
                    lines.Add(Editors);
                    lines.Add(Ffb);
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
