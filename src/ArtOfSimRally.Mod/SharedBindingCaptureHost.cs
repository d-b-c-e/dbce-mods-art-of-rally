using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Input;
using Dbce.Wheel.Ui;

namespace ArtOfSimRally.Mod
{
    // All samples arrive AFTER the existing bridge accepted the whole frame. This class never reads a device.
    internal sealed class SharedBindingCaptureHost : ISharedBindingHost
    {
        private sealed class Device
        {
            internal Guid Instance, Product;
            internal string Name, Path;
            internal ulong Generation;
            internal double Seen;
            internal BindingCaptureSnapshot Sample;
            internal int[] Minimum, Maximum;
        }
        internal static SharedBindingCaptureHost Instance { get; private set; } = new SharedBindingCaptureHost(null, null, () => 0, () => false);
        internal static ISharedBindingHost Current => Instance;
        internal static void Configure(string path, byte[] active, Func<double> clock, Func<bool> canEdit)
        { Instance = new SharedBindingCaptureHost(path, active, clock, canEdit); }

        private readonly string path;
        private readonly Func<double> clock;
        private readonly Func<bool> canEdit;
        private readonly SharedBindingDraft draft;
        private readonly Dictionary<Guid, Device> devices = new Dictionary<Guid, Device>();
        private SharedBindingRowView[] rows;
        private string action, error;
        private Guid captureDevice;
        private ulong captureGeneration;
        private double captureStart, deadline;
        private SharedBindingCaptureSession capture;
        private string message = "Bind a control, review the draft, then Save bindings. Restart to use saved changes.";
        private static readonly string[] Actions = {
            "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
            "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight", "camera", "lookBack",
            "reset", "horn", "modMenu", "muteFfb", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
        private static readonly string[] Labels = {
            "Steering", "Accelerator", "Brake", "Clutch", "Handbrake", "Shift up", "Shift down", "Neutral", "Reverse",
            "Confirm", "Back", "Start / pause", "Select / view", "Navigate up", "Navigate down", "Navigate left", "Navigate right",
            "Change camera", "Look back", "Reset car", "Horn", "Mod menu", "Mute force feedback",
            "Gear 1", "Gear 2", "Gear 3", "Gear 4", "Gear 5", "Gear 6", "Gear 7", "Gear 8" };

        internal SharedBindingCaptureHost(string path, byte[] active, Func<double> clock, Func<bool> canEdit)
        {
            this.path = path; this.clock = clock; this.canEdit = canEdit;
            if (active == null) return;
            try { draft = new SharedBindingDraft(active); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is ArgumentException)
            { error = ex.Message; }
        }
        private bool Fresh(Device d) => clock() - d.Seen <= .25;
        private bool Editable => draft != null && canEdit() && devices.Values.Any(Fresh);
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
        public double CaptureSecondsLeft { get { Refresh(); return action == null ? 0 : Math.Max(0, deadline - clock()); } }
        public string CaptureMessage { get { Refresh(); return message; } }
        public IReadOnlyList<SharedBindingDeviceView> Devices
        {
            get
            {
                Guid preferred = action == null ? DefaultDevice(null) : captureDevice;
                return devices.Values.Where(Fresh).Select(d => new SharedBindingDeviceView {
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
                rows = Actions.Select((id, index) => Row(id, Labels[index])).ToArray(); return rows;
            }
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
            if (id == "select" || id == "lookBack" || id == "reset" || id == "horn") return "This Art candidate does not consume this shared action.";
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
            if (Array.IndexOf(Actions, id) < 0 || why.Length != 0) { message = why; return; }
            if (!Guid.TryParse(instanceGuid, out var instance) || !devices.TryGetValue(instance, out var device) || !Fresh(device))
            { message = "That saved device is not available for capture. Its prior binding was kept; restart or use standalone setup for new devices."; return; }
            if (ControlBinding.ShapeOf(id) == ControlShape.Centred && !device.Maximum.Where((value, i) => value > device.Minimum[i]).Any())
            { message = "This reader has no qualified axis range for steering capture. Its prior binding was kept; use standalone setup."; return; }
            action = id; captureDevice = instance; captureGeneration = device.Generation;
            captureStart = clock(); deadline = captureStart + (ControlBinding.ShapeOf(id) == ControlShape.Digital ? 10 : 11);
            capture = null; rows = null; message = "Release buttons and hats. Keep an axis released or centered for one second, then move one control.";
        }
        public void CancelCapture() { if (action == null) return; action = null; capture = null; rows = null; message = "Binding canceled. The prior binding was kept."; }
        private void Refresh()
        {
            if (action == null) return;
            if (!canEdit()) { CancelCapture(); message = "Capture canceled because the settings lost focus or the main menu closed. The prior binding was kept."; }
            else if (!devices.TryGetValue(captureDevice, out var device) || !Fresh(device) || device.Generation != captureGeneration)
            { CancelCapture(); message = "Capture device disconnected or changed. The prior binding was kept."; }
            else if (clock() >= deadline) { CancelCapture(); message = "Capture timed out. Release other controls and try again; the prior binding was kept."; }
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
                devices.Remove(instance); if (action != null) CancelCapture();
                message = "Capture refused a reader-slot identity mismatch. Restart before editing this device; no alternate identity was substituted.";
                return;
            }
            if (sample[50] != 1)
            {
                devices.Remove(instance);
                if (action != null && captureDevice == instance) { CancelCapture(); message = "Capture device has no valid sample. Its prior binding was kept."; }
                return;
            }
            int[] axes = new int[8], minimum = new int[8], maximum = new int[8], hats = new int[4]; bool[] buttons = new bool[128];
            uint axisMask = BitConverter.ToUInt32(metadata, 48), rangeMask = BitConverter.ToUInt32(metadata, 52), hatMask = BitConverter.ToUInt32(metadata, 56);
            for (int i = 0; i < 8; i++) if ((axisMask & rangeMask & (1u << i)) != 0)
            {
                axes[i] = BitConverter.ToInt32(sample, 52 + i * 4); minimum[i] = BitConverter.ToInt32(metadata, 80 + i * 4); maximum[i] = BitConverter.ToInt32(metadata, 112 + i * 4);
                if (maximum[i] <= minimum[i]) { axes[i] = minimum[i] = maximum[i] = 0; continue; }
            }
            if (!SharedBindingCapture.RangesValid(axes, minimum, maximum))
            {
                devices.Remove(instance); if (action != null && captureDevice == instance) CancelCapture();
                message = "Capture refused an out-of-range raw sample. No control from that frame was bound; the prior binding was kept.";
                return;
            }
            for (int i = 0; i < 128; i++) buttons[i] = (metadata[64 + i / 8] & (1 << (i % 8))) != 0 && (sample[84 + i] & 0x80) != 0;
            for (int i = 0; i < 4; i++) { int hat = BitConverter.ToInt32(sample, 212 + i * 4); hats[i] = (hatMask & (1u << i)) != 0 && hat >= 0 && hat < 36000 ? hat : -1; }
            var latest = new Device { Instance = instance, Product = product, Path = ownedPath, Name = name, Generation = generation, Seen = clock(),
                Sample = new BindingCaptureSnapshot(axes, buttons, hats), Minimum = minimum, Maximum = maximum };
            if (devices.TryGetValue(instance, out var previous) && (previous.Generation != generation || previous.Product != product || previous.Path != ownedPath))
            { if (action != null && captureDevice == instance) CancelCapture(); }
            devices[instance] = latest;
            if (action == null || captureDevice != instance) return;
            Refresh(); if (action == null) return;
            bool analog = ControlBinding.ShapeOf(action) != ControlShape.Digital;
            if (analog && clock() - captureStart < 1) return;
            if (capture == null) { capture = new SharedBindingCaptureSession(latest.Sample, analog, minimum, maximum); message = "Listening: move one control or press one button / hat direction. Release held buttons first."; return; }
            var detected = capture.Read(latest.Sample); if (detected == null) return;
            try
            {
                var kind = (ControlKind)Enum.Parse(typeof(ControlKind), detected.Kind);
                var binding = ControlBinding.FromCapture(action, kind, detected.Index, detected.Direction, detected.Rest, instance, product, name,
                    kind == ControlKind.Axis ? minimum[detected.Index] : 0, kind == ControlKind.Axis ? maximum[detected.Index] : 65535);
                draft.Set(action, binding.ToString()); action = null; capture = null; rows = null;
                message = "Binding captured into the draft. Review it and Save bindings; restart to use it.";
            }
            catch (ArgumentException ex) { CancelCapture(); message = "Capture refused: " + ex.Message + ". The prior binding was kept."; }
        }
        internal void ReaderStopped() { devices.Clear(); CancelCapture(); rows = null; }
        public void Clear(string id) { if (Editable && action == null && Array.IndexOf(Actions, id) >= 0) { draft.Set(id, null); rows = null; } }
        public void SetInverted(string id, bool inverted)
        {
            if (!Editable || action != null) return;
            var binding = draft.Binding(id);
            if (binding?.Kind != ControlKind.Axis) return;
            draft.Set(id, binding.WithInverted(inverted).ToString()); rows = null;
        }
        public bool Save(out string result)
        {
            Refresh();
            if (!Editable || action != null) { result = "Save needs the main-menu binding editor with no capture running. The draft was kept."; return false; }
            bool saved = draft.Save(path, out result); message = result; rows = null; return saved;
        }
        public void Discard() { CancelCapture(); draft?.Discard(); rows = null; message = "Unsaved binding edits discarded. Active and saved bindings are unchanged."; }
    }
}
