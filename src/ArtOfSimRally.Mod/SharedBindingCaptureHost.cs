using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Input;
using Dbce.Wheel.Ui;

namespace ArtOfSimRally.Mod
{
    // All samples arrive AFTER the existing bridge accepted the whole frame. This class never reads a device.
    internal sealed class SharedBindingCaptureHost : ISharedBindingHost, ISharedBindingSaveStatus
    {
        internal static SharedBindingCaptureHost Instance { get; private set; } = new SharedBindingCaptureHost(null, null, () => 0, () => false);
        internal static ISharedBindingHost Current => Instance;
        internal static void Configure(string path, byte[] active, Func<double> clock, Func<bool> canEdit)
        { Instance = new SharedBindingCaptureHost(path, active, clock, canEdit); }

        private readonly string path;
        private readonly Func<double> clock;
        private readonly Func<long> sourceClock;
        private readonly Func<bool> canEdit;
        private readonly SharedBindingDraft draft;
        private readonly SharedBindingCaptureFlow flow;
        private readonly BindingSaveStatus saveStatus = new BindingSaveStatus();
        public string SaveFailure => saveStatus.SaveFailure;
        private SharedBindingRowView[] rows;
        private string error;
        private string action => flow.CapturingAction;
        private string message { get => flow.Message; set => flow.Message = value; }
        private static readonly string[] Actions = {
            "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
            "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight", "camera", "lookBack",
            "reset", "horn", "modMenu", "muteFfb", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
        private static readonly string[] Labels = {
            "Steering", "Accelerator", "Brake", "Clutch", "Handbrake", "Shift up", "Shift down", "Neutral", "Reverse",
            "Confirm", "Back", "Start / pause", "Select / view", "Navigate up", "Navigate down", "Navigate left", "Navigate right",
            "Change camera", "Look back", "Recover car (hold 1 s)", "Horn", "Mod menu", "Mute force feedback",
            "Gear 1", "Gear 2", "Gear 3", "Gear 4", "Gear 5", "Gear 6", "Gear 7", "Gear 8" };

        internal SharedBindingCaptureHost(string path, byte[] active, Func<double> clock, Func<bool> canEdit, Func<long> sourceClock = null)
        {
            this.path = path; this.clock = clock; this.canEdit = canEdit;
            this.sourceClock = sourceClock ?? (Func<long>)Shim.BindingSampleClock.Microseconds;
            flow = new SharedBindingCaptureFlow(clock, (id, binding) => {
                if (draft == null || !canEdit()) return "The binding editor is no longer editable";
                string why = ActionReason(id);
                if (why.Length != 0) return why;
                draft.Set(id, binding.ToString()); rows = null; return null;
            });
            if (active == null) return;
            try { draft = new SharedBindingDraft(active); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is ArgumentException)
            { error = ex.Message; }
        }
        private bool Editable => draft != null && canEdit() && flow.HasFreshObservation;
        public SharedBindingAccess Access { get { Refresh(); return draft == null ? (error == null ? SharedBindingAccess.Missing : SharedBindingAccess.ReadOnly) : Editable ? SharedBindingAccess.Editable : SharedBindingAccess.ReadOnly; } }
        public string AccessReason => draft == null
            ? error ?? "No shared profile is available. Close the game, extract the matching Controls Setup download outside the game folder, run Set Up Controls.bat, then restart. Wheelkit is optional."
            : Editable ? "Shared binding edits are a draft. Save bindings, then restart the game to use them."
            : "Shared binding capture needs the mod settings open in the main menu and a qualified, connected reader. New or unavailable devices need a restart or the standalone Controls Setup download.";
        public string ActiveSource => draft == null ? (error == null ? "No active shared profile" : "Startup profile unavailable to this editor") : draft.Active.Name + " · " +
            (draft.Active.Revision.Length > 12 ? draft.Active.Revision.Substring(0, 12) : draft.Active.Revision);
        public bool HasDraft => draft != null && draft.HasDraft;
        public bool SavedPendingRestart => draft != null && draft.SavedPendingRestart;
        public string CapturingAction { get { Refresh(); return action; } }
        public double CaptureSecondsLeft { get { Refresh(); return flow.SecondsLeft; } }
        public string CaptureMessage { get { Refresh(); return message; } }
        public IReadOnlyList<SharedBindingDeviceView> Devices
        {
            get
            {
                Refresh();
                Guid preferred = action == null ? DefaultDevice(null) : flow.CaptureDevice;
                return flow.Observations.Select(d => new SharedBindingDeviceView {
                    InstanceGuid = d.Instance.ToString("D"), ProductGuid = d.Product.ToString("D"), Name = d.Name,
                    IsDefault = d.Instance == preferred }).OrderBy(d => d.Name).ToArray();
            }
        }
        public IReadOnlyList<SharedBindingRowView> Rows
        {
            get
            {
                Refresh();
                if (rows != null) return rows;
                rows = Actions.Select((id, index) => Shown(id) ? Row(id, Labels[index]) : null).Where(r => r != null).ToArray(); return rows;
            }
        }
        // Art has no select action (the shim marks it unsupported), so it gets no row, capture or clear here.
        private static bool Shown(string id) => Array.IndexOf(Actions, id) >= 0 && id != "select";
        // Prompt labels: the startup Active profile only. Never Saved or Draft, no Refresh, no device read.
        internal bool TryActiveBinding(string sharedAction, out ControlBinding binding)
        {
            binding = null;
            return draft != null && sharedAction != null && draft.Active.Bindings.TryGetValue(sharedAction, out binding) && binding != null;
        }
        private SharedBindingRowView Row(string id, string label)
        {
            ControlBinding active = null, saved = null;
            if (draft != null) { draft.Active.Bindings.TryGetValue(id, out active); draft.Saved.Bindings.TryGetValue(id, out saved); }
            var current = draft == null ? null : draft.Binding(id);
            bool changed = draft != null && draft.Changes.ContainsKey(id);
            bool pending = Text(saved) != Text(active);
            return new SharedBindingRowView { Action = id, Label = label, Active = draft == null && error != null ? new SharedBindingValue(false, "Unavailable") : Value(active),
                NextStart = pending ? (SharedBindingValue?)Value(saved) : null,
                Draft = changed ? (SharedBindingValue?)Value(current) : null,
                Device = (current ?? saved ?? active)?.Name ?? "No device bound", IsAxis = current?.Kind == ControlKind.Axis,
                ActiveInverted = active?.Inverted == true, NextStartInverted = pending ? (bool?)(saved?.Inverted == true) : null,
                DraftInverted = changed ? (bool?)(current?.Inverted == true) : null,
                State = draft == null && error != null ? SharedBindingRowState.Error : action == id ? SharedBindingRowState.Capturing : changed ? SharedBindingRowState.Changed : current == null ? SharedBindingRowState.Unbound : SharedBindingRowState.Bound,
                Note = draft == null && error != null ? error : ActionReason(id) };
        }
        private static string Text(ControlBinding b) => b?.ToString();
        private static SharedBindingValue Value(ControlBinding b) => new SharedBindingValue(b != null, b == null ? "Unbound" :
            (b.Kind == ControlKind.Axis ? ControlNames.Axis(b.Index) : b.Kind == ControlKind.Button ? ControlNames.Button(b.Index) : ControlNames.Hat(b.Index, b.Angle)) +
            (b.Inverted ? " · inverted" : "") + " · " + b.Name);
        private string ActionReason(string id)
        {
            if (!Shown(id)) return "Art has no such action.";
            if (draft == null) return "";
            if ((id == "shiftUp" || id == "shiftDown") && draft.Saved.Transmission != "Sequential") return "This profile does not select Sequential. Change transmission with the standalone setup helper.";
            if ((id == "neutral" || id == "reverse" || id.StartsWith("gear", StringComparison.Ordinal)) && draft.Saved.Transmission != "H-pattern") return "This profile does not select H-pattern. Change transmission with the standalone setup helper.";
            return "";
        }
        private Guid DefaultDevice(string id)
        {
            var binding = draft == null || id == null ? null : draft.Binding(id);
            if (binding != null) return binding.Device;
            return draft != null && draft.Saved.Bindings.TryGetValue("steer", out var steer) ? steer.Device : Guid.Empty;
        }
        public void BeginCapture(string id) => BeginCapture(id, DefaultDevice(id).ToString("D"));
        public void BeginCapture(string id, string instanceGuid)
        {
            Refresh();
            if (!Editable || action != null && action != id) return;
            string why = ActionReason(id);
            if (!Shown(id) || why.Length != 0) { message = why; return; }
            if (!Guid.TryParse(instanceGuid, out var instance))
            { message = "That saved device is not available for capture. Its prior binding was kept; restart or use standalone setup for new devices."; return; }
            flow.Begin(id, instance, Editable); rows = null;
        }
        public void CancelCapture() { flow.Cancel(); rows = null; }
        private void Refresh()
        {
            string before = action;
            flow.Refresh(canEdit());
            if (before != action) rows = null;
        }

        internal void ObserveAccepted(string name, string ownedPath, byte[] metadata, byte[] sample, Guid? expectedInstance = null)
        {
            if (metadata == null || metadata.Length != 144 || sample == null || sample.Length != 232 || string.IsNullOrEmpty(ownedPath)) return;
            ulong generation = BitConverter.ToUInt64(metadata, 8);
            if (generation == 0 || BitConverter.ToUInt64(sample, 8) != generation) return;
            var instanceBytes = new byte[16]; var productBytes = new byte[16];
            Buffer.BlockCopy(metadata, 16, instanceBytes, 0, 16); Buffer.BlockCopy(metadata, 32, productBytes, 0, 16);
            var instance = new Guid(instanceBytes); var product = new Guid(productBytes);
            if (instance == Guid.Empty || product == Guid.Empty) return;
            if (expectedInstance.HasValue && expectedInstance.Value != instance)
            {
                flow.Remove(instance, "Capture refused a reader-slot identity mismatch.");
                flow.Remove(expectedInstance.Value, "Capture refused a reader-slot identity mismatch.");
                CancelCapture();
                message = "Capture refused a reader-slot identity mismatch. Restart before editing this device; no alternate identity was substituted.";
                return;
            }
            if (sample[50] != 1)
            {
                flow.Remove(instance, "Capture device has no valid sample. Its prior binding was kept."); rows = null;
                return;
            }
            // Publish accepts a whole frame; an individual valid read may still be Core-Stale.
            // A QPC check AFTER Publish conservatively bounds age at Core's earlier decision boundary.
            // The pinned copied provider uses the ordinary 100000-us bound, in this same system-QPC domain.
            long startUs = BitConverter.ToInt64(sample, 24), endUs = BitConverter.ToInt64(sample, 32), nowUs = sourceClock();
            if (startUs < 0 || endUs < startUs || nowUs < endUs || nowUs < 0 || nowUs - endUs > 100000)
            {
                flow.Remove(instance, "Capture refused a stale or unqualified source timestamp. The prior binding was kept."); rows = null;
                return;
            }
            int[] axes = new int[8], minimum = new int[8], maximum = new int[8], hats = new int[4]; bool[] buttons = new bool[128];
            uint axisMask = BitConverter.ToUInt32(metadata, 48), rangeMask = BitConverter.ToUInt32(metadata, 52), hatMask = BitConverter.ToUInt32(metadata, 56);
            for (int i = 0; i < 8; i++) if ((axisMask & rangeMask & (1u << i)) != 0)
            {
                axes[i] = BitConverter.ToInt32(sample, 52 + i * 4); minimum[i] = BitConverter.ToInt32(metadata, 80 + i * 4); maximum[i] = BitConverter.ToInt32(metadata, 112 + i * 4);
                if (maximum[i] <= minimum[i]) { axes[i] = minimum[i] = maximum[i] = 0; continue; }
            }
            for (int i = 0; i < 128; i++) buttons[i] = (metadata[64 + i / 8] & (1 << (i % 8))) != 0 && (sample[84 + i] & 0x80) != 0;
            for (int i = 0; i < 4; i++) { int hat = BitConverter.ToInt32(sample, 212 + i * 4); hats[i] = (hatMask & (1u << i)) != 0 && hat >= 0 && hat < 36000 ? hat : -1; }
            string before = action;
            flow.Observe(new AcceptedBindingObservation(instance, product, name, ownedPath, generation, BitConverter.ToUInt64(sample, 16), clock(),
                new BindingCaptureSnapshot(axes, buttons, hats), minimum, maximum), draft != null && canEdit());
            if (before != action) rows = null;
        }
        internal void ReaderStopped() { flow.ReaderStopped(); rows = null; }
        public void Clear(string id) { if (Editable && action == null && Shown(id)) { draft.Set(id, null); rows = null; } }
        public void SetInverted(string id, bool inverted)
        {
            if (!Editable || action != null || !Shown(id)) return;
            var binding = draft.Binding(id);
            if (binding?.Kind != ControlKind.Axis) return;
            draft.Set(id, binding.WithInverted(inverted).ToString()); rows = null;
        }
        public bool Save(out string result)
        {
            Refresh();
            if (!Editable || action != null) { result = "Save needs the main-menu binding editor with no capture running. The draft was kept."; return saveStatus.Record(false, result); }
            bool saved = draft.Save(path, out result); message = result; rows = null; return saveStatus.Record(saved, result);
        }
        public void Discard() { CancelCapture(); draft?.Discard(); rows = null; saveStatus.Discard(); message = "Unsaved binding edits discarded. Active and saved bindings are unchanged."; }
    }
}
