// MIT. Extracted from iRacing Arcade SharedBindingPanel: presentation only.
// Uses the existing shared binding contract and selection policy; owns no reader or writer.
#nullable disable
using System;
#if DBCE_INPUT_NET35
using System.Collections.Generic;
#else
using System.Linq;
#endif
using Dbce.Wheel.Input;

namespace Dbce.Wheel.Ui
{
    public interface ISharedBindingPanelUi
    {
        void Label(string text);
        void Help(string text);
        bool Button(string text, bool enabled = true);
    }

    /// <summary>One selected action at a time keeps the same controls usable in a bounded rectangle.
    /// Devices are only the host's accepted snapshot, never an OS discovery result.</summary>
    public sealed class SharedBindingPanel
    {
        private readonly SharedBindingDeviceSelection selection = new SharedBindingDeviceSelection();
        private int devicePage;
        private string action;
        private bool details, unsupported;
        private object previousHost;

        public void Draw(ISharedBindingHost host, ISharedBindingPanelUi ui, string saveStatus)
        {
            if (host == null || ui == null) throw new ArgumentNullException();
            if (!ReferenceEquals(previousHost, host)) { action = null; devicePage = 0; details = unsupported = false; previousHost = host; }
            string capturing = host.CapturingAction;
            bool listening = capturing != null;
            ui.Label("Shared controls");
            ui.Help(host.AccessReason);
            if (host.SavedPendingRestart) ui.Help("Saved for next start. Active bindings are unchanged; restart to use saved controls.");
            ui.Help("Choose an open device for each action. For a new device, close the game and use the matching Controls Setup download. Wheelkit is optional.");
            ui.Help(host.CaptureMessage);
            if (listening)
            {
                ui.Label("Listening for " + capturing + " · " + Math.Ceiling(host.CaptureSecondsLeft) + " s");
                if (ui.Button("Cancel capture")) host.CancelCapture();
            }

            var rows = host.Rows;
            if (rows.Count == 0) { ui.Help("No admitted control rows are available."); Footer(host, ui, saveStatus, listening); return; }
#if DBCE_INPUT_NET35
            // Avoid compiler-selected mscorlib Func references on old Unity profiles.
            var admitted = new List<SharedBindingRowView>();
            var refused = new List<SharedBindingRowView>();
            bool found = false;
            foreach (var value in rows)
            {
                if (value.Applicability == SharedBindingApplicability.Supported) { admitted.Add(value); if (value.Action == action) found = true; }
                else refused.Add(value);
            }
            var delivered = admitted.ToArray();
            var unavailable = refused.ToArray();
            if (!found) action = delivered.Length == 0 ? null : delivered[0].Action;
#else
            var delivered = rows.Where(value => value.Applicability == SharedBindingApplicability.Supported).ToArray();
            var unavailable = rows.Where(value => value.Applicability != SharedBindingApplicability.Supported).ToArray();
            if (!delivered.Any(value => value.Action == action)) action = delivered.FirstOrDefault()?.Action;
#endif
            foreach (var choice in delivered)
                if (ui.Button((choice.Action == action ? "Selected: " : "Edit: ") + choice.Label, !listening)) action = choice.Action;
            if (unavailable.Length > 0 && ui.Button((unsupported ? "Hide" : "Show") + " unsupported controls (" + unavailable.Length + ")", !listening)) unsupported = !unsupported;
            if (unsupported) foreach (var value in unavailable) ui.Help(value.Label + ": " + value.Note);
            if (ui.Button(details ? "Hide details" : "Details")) details = !details;
            if (details) ui.Help("Active source: " + host.ActiveSource);
#if DBCE_INPUT_NET35
            SharedBindingRowView row = null;
            foreach (var value in delivered) if (value.Action == action) { row = value; break; }
#else
            var row = delivered.FirstOrDefault(value => value.Action == action);
#endif
            if (row == null) { Footer(host, ui, saveStatus, listening); return; }
            ui.Label(row.Label);
            ui.Help("Active: " + row.Active.Text);
            if (row.NextStart.HasValue) ui.Help("Next start: " + row.NextStart.Value.Text);
            if (row.Draft.HasValue) ui.Help("Draft: " + row.Draft.Value.Text);
            if (!string.IsNullOrEmpty(row.Note)) ui.Help(row.Note);
            bool editable = SharedBindingUiPolicy.CanEditRow(host.Access, capturing, row.Note, row.Applicability);

            var devices = host.Devices;
            selection.Reconcile(host, devices);
            if (ui.Button("Use this action's saved device", editable)) selection.SelectSaved();
            devicePage = Math.Max(0, Math.Min(devicePage, Math.Max(0, (devices.Count - 1) / 3)));
            for (int i = devicePage * 3; i < Math.Min(devices.Count, devicePage * 3 + 3); i++)
            {
                var device = devices[i];
                bool selected = selection.Mode == SharedBindingSelectionMode.Explicit &&
                    string.Equals(selection.InstanceGuid, device.InstanceGuid, StringComparison.OrdinalIgnoreCase);
                if (ui.Button((selected ? "Selected: " : "Use: ") + device.Name, editable))
                    selection.SelectDevice(device.InstanceGuid, devices);
            }
            if (devices.Count > 3 && ui.Button("More open devices", editable))
                devicePage = (devicePage + 1) % ((devices.Count + 2) / 3);
            if (devices.Count == 0) ui.Help("No fresh admitted capture device. Saved bindings are retained.");
            if (!selection.CanBind) ui.Help(selection.UnavailableReason);
            if (ui.Button("Bind " + row.Label, editable && selection.CanBind && devices.Count > 0))
            {
                if (selection.Mode == SharedBindingSelectionMode.Saved) host.BeginCapture(row.Action);
                else host.BeginCapture(row.Action, selection.InstanceGuid);
            }
            if (ui.Button("Clear " + row.Label, editable && row.Action != "steer")) host.Clear(row.Action);
            if (row.IsAxis && ui.Button("Invert " + row.Label + ": " +
                ((row.DraftInverted ?? row.NextStartInverted ?? row.ActiveInverted) ? "On" : "Off"), editable))
                host.SetInverted(row.Action, !(row.DraftInverted ?? row.NextStartInverted ?? row.ActiveInverted));
            Footer(host, ui, saveStatus, listening);
        }
        private static void Footer(ISharedBindingHost host, ISharedBindingPanelUi ui, string saveStatus, bool listening)
        {
            if (ui.Button("Save bindings for next start", host.Access == SharedBindingAccess.Editable && !listening && host.HasDraft))
                host.Save(out _);
            if (ui.Button("Discard unsaved edits", !listening && host.HasDraft)) host.Discard();
            // This is independent of capture text. A failed Save stays visible
            // when a later capture changes the host's transient message.
            if (!Blank(saveStatus)) ui.Help(saveStatus);
        }
        private static bool Blank(string value)
        {
#if DBCE_INPUT_NET35
            if (value == null) return true;
            foreach (char c in value) if (!char.IsWhiteSpace(c)) return false;
            return true;
#else
            return string.IsNullOrWhiteSpace(value);
#endif
        }

    }
}
