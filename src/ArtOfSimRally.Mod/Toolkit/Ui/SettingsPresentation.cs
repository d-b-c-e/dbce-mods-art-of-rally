// MIT. Source-vendored UI rules; no engine, device or persistence dependency.
using System;
using System.Globalization;

namespace Dbce.Wheel.Ui
{
    /// <summary>Binding presentation only. Hosts supply facts from their existing draft/save lifecycle;
    /// this does not save, discover devices, change access or clear a persistent Save failure.</summary>
    public static class BindingPresentation
    {
        public const string SaveModSettings = "Save mod settings";
        public const string SaveBindings = "Save bindings";
        public const string BindFrom = "Bind from";
        public const string Unsaved = "Binding edits unsaved";
        public const string SavedForNextStart = "Bindings saved for next start";

        // Save failures survive capture/general messages via BindingSaveStatus. A new draft takes
        // precedence over the previous saved revision; ordinary settings status stays separate.
        public static string Footer(bool hasDraft, bool savedPendingRestart, string saveFailure)
        {
            if (!Blank(saveFailure))
            {
                string reason = saveFailure.Trim();
                const string label = "Bindings not saved";
                // Existing writers may already label their failure. Keep the retained reason,
                // but give every consumer one presentation prefix without changing host state.
                if (reason.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = reason.Substring(label.Length).TrimStart();
                    if (suffix.Length == 0) return label;
                    if (suffix[0] == ':' || suffix[0] == '—' || suffix[0] == '-')
                    {
                        reason = suffix.Substring(1).TrimStart();
                        if (reason.Length == 0) return label;
                    }
                }
                return label + " — " + reason;
            }
            if (hasDraft) return Unsaved;
            return savedPendingRestart ? SavedForNextStart : "";
        }

        public static string SetupGuidance(bool canFindDevices)
        {
            return (canFindDevices
                ? "Choose a device under Bind from. Use Find devices if it is missing. "
                : "Choose an available device under Bind from. To add a device, close the game and run Set Up Controls.bat. Wheelkit is optional. ") +
                "Select Bind, release the control, then move it. Review the draft, select Save bindings, and restart to use the saved controls.";
        }

        private static bool Blank(string value)
        {
            if (value == null) return true;
            foreach (char c in value) if (!char.IsWhiteSpace(c)) return false;
            return true;
        }
    }

    /// <summary>Shared player-facing control names. Stored hardware indices remain zero based.</summary>
    public static class ControlNames
    {
        private static readonly string[] Axes = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
        public static string Axis(int index) => index >= 0 && index < Axes.Length ? Axes[index] : "Unknown axis";
        public static string Button(int index) => index >= 0 && index < 128 ? "Button " + (index + 1) : "Unknown button";
        public static string Hat(int index, int direction) => index >= 0 && index < 4 && direction >= 0 && direction < 36000
            ? "Hat " + (index + 1) + " · " + (direction / 100).ToString(CultureInfo.InvariantCulture) + "°" : "Unknown hat";
    }

    /// <summary>Numeric edits in WPF and in-game widgets use the same grid and finite-value rules.
    /// Call only for an explicit user edit; displaying a saved off-grid value must not rewrite it.</summary>
    public static class NumberEdit
    {
        public static bool TryValue(double requested, double min, double max, double step, out double value)
        {
            value = 0;
            if (!Finite(requested) || !Finite(min) || !Finite(max) || !Finite(step) || min > max || step <= 0) return false;
            double bounded = Math.Max(min, Math.Min(max, requested));
            double ticks = (bounded - min) / step;
            if (!Finite(ticks)) return false;
            value = Math.Max(min, Math.Min(max, min + Math.Round(ticks, MidpointRounding.AwayFromZero) * step));
            return Finite(value);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
