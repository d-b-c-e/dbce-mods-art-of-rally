using System;
using Dbce.Wheel.Input;
using Dbce.Wheel.Ui;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class SettingsPanel
    {
        private static readonly SharedBindingDeviceSelection SharedDeviceSelection = new SharedBindingDeviceSelection();
        /// <summary>
        /// The shared profile's bindings, edited in F6 through the host (SharedBindingCaptureHost): one row per action
        /// with its active value, a saved value waiting for the next start and an unsaved draft value, then Bind (Cancel
        /// with the seconds left while listening), Clear and Invert. Save writes the draft; the active profile stays in use
        /// until the game restarts. A missing or read-only profile shows the rows read-only with the host's reason.
        /// The panel reads only the host's snapshot: no device discovery or polling here.
        /// </summary>
        private static void SharedBindings()
        {
            var host = Bindings;
            if (host == null) return;
            Heading("Shared profile");
            var access = host.Access;
            bool editable = access == SharedBindingAccess.Editable;
            if (!editable && !string.IsNullOrEmpty(host.AccessReason)) W.StatusCard("bindings-access", access == SharedBindingAccess.Missing ? "No shared profile" : "Shared profile is read-only",
                host.AccessReason, false, null);
            string capturing = host.CapturingAction;
            var currentDevices = host.Devices ?? Array.Empty<SharedBindingDeviceView>();
            SharedDeviceSelection.Reconcile(host, currentDevices);
            if (capturing != null)
            {
                Help(host.CaptureMessage);
                var devices = currentDevices;
                if (devices != null && devices.Count > 1)
                {
                    var names = new string[devices.Count]; int selected = -1;
                    for (int i = 0; i < devices.Count; i++) { names[i] = devices[i].Name; if (devices[i].IsDefault) selected = i; }
                    int pick = W.ChoiceRow("binding-device", "Device", null, names, selected, true);
                    if (pick >= 0 && pick != selected) host.BeginCapture(capturing, devices[pick].InstanceGuid);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(host.CaptureMessage)) Help(host.CaptureMessage);
                // An explicit device for the next Bind, chosen before capture starts (lola 123857/123930; shown with one open device too): an action whose saved device
                // is unavailable can still be bound on another open, qualified device. Only the host's current devices are
                // offered; nothing is substituted or discovered.
                var devices = currentDevices;
                if (editable && devices != null && devices.Count > 0)
                {
                    var names = new string[devices.Count + 1]; int selected = 0;
                    names[0] = "Saved device";
                    for (int i = 0; i < devices.Count; i++) names[i + 1] = devices[i].Name;
                    selected = SharedDeviceSelection.Mode == SharedBindingSelectionMode.Saved ? 0 : SharedDeviceSelection.SelectedIndex < 0 ? -1 : SharedDeviceSelection.SelectedIndex + 1;
                    int pick = W.ChoiceRow("binding-device-next", "Bind from", "Which open device the next Bind listens to.", names, selected, !LegacyEditing);
                    if (pick == 0) SharedDeviceSelection.SelectSaved();
                    else if (pick > 0 && pick <= devices.Count) SharedDeviceSelection.SelectDevice(devices[pick - 1].InstanceGuid, devices);
                }
                if (SharedDeviceSelection.UnavailableReason.Length > 0) Help(SharedDeviceSelection.UnavailableReason);
            }
            // Mutually exclusive with every other edit (an Art calibration, camera or Settings key capture, a connection edit):
            // shared actions start only when nothing else is being edited (kiki 123770).
            bool idle = !LegacyEditing && capturing == null;
            var rows = host.Rows;
            for (int i = 0; rows != null && i < rows.Count; i++) BindingRow(host, rows[i], access, capturing, idle, currentDevices.Count > 0);
            if (editable && (host.HasDraft || host.SavedPendingRestart))
            {
                GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
                if (W.Command("bindings-save", "Save bindings", host.HasDraft && idle) && !host.Save(out string message) && !(host is ISharedBindingSaveStatus)) Help(message);
                if (W.Command("bindings-discard", "Discard", host.HasDraft && idle)) host.Discard();
                W.Spacer();
                GUILayout.EndHorizontal();
                W.Help(host.HasDraft ? "Unsaved changes. Save bindings writes the profile; the game uses it from the next start."
                    : "Saved. Restart the game to use these bindings.");
            }
            if (host is ISharedBindingSaveStatus saveStatus && !string.IsNullOrEmpty(saveStatus.SaveFailure)) Help(saveStatus.SaveFailure);
        }

        private static string ValueText(SharedBindingValue value) =>
            !string.IsNullOrEmpty(value.Text) ? value.Text : value.Bound ? "Bound" : "Unbound";
        private static void BindingRow(ISharedBindingHost host, SharedBindingRowView row, SharedBindingAccess access, string capturing, bool idle, bool canListen)
        {
            // The host's own text: "Unbound" for a real clear or missing binding, "Unavailable" when the editor could not read
            // it (lola 124035); an unreadable control is never shown as absent.
            string value = ValueText(row.Active);
            if (row.ActiveInverted) value += " · inverted";
            if (row.NextStart.HasValue) value += "  →  next start: " + ValueText(row.NextStart.Value);
            if (row.Draft.HasValue) value += "  ·  unsaved: " + ValueText(row.Draft.Value);
            var layout = W.BeginRow(row.Label, value + (string.IsNullOrEmpty(row.Note) ? "" : "\n" + row.Note), 300);
            if (!layout.Stacked) W.Spacer();
            bool listening = capturing == row.Action;
            bool allowed = idle && SharedBindingUiPolicy.CanEditRow(access, capturing, row.Note);
            if (listening)
            {
                if (W.Command("bind-" + row.Action, "Cancel (" + Math.Ceiling(host.CaptureSecondsLeft).ToString("0") + " s)")) host.CancelCapture();
            }
            else if (W.Command("bind-" + row.Action, "Bind", allowed && canListen && SharedDeviceSelection.CanBind)) { if (SharedDeviceSelection.Mode == SharedBindingSelectionMode.Explicit) host.BeginCapture(row.Action, SharedDeviceSelection.InstanceGuid); else host.BeginCapture(row.Action); }
            bool bound = row.Draft.HasValue ? row.Draft.Value.Bound : row.NextStart.HasValue ? row.NextStart.Value.Bound : row.Active.Bound;
            if (W.Command("clear-" + row.Action, "Clear", allowed && bound)) host.Clear(row.Action);
            if (row.IsAxis)
            {
                bool inverted = row.DraftInverted ?? row.NextStartInverted ?? row.ActiveInverted;
                if (W.Segment("invert-" + row.Action, "Invert", inverted, allowed && bound)) host.SetInverted(row.Action, !inverted);
            }
            W.EndRow(layout);
        }

    }
}
