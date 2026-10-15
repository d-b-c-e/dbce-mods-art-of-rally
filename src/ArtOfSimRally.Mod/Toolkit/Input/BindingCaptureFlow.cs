// Clock-injected capture lifecycle; no device, binary-layout, UI, file or force calls.
#nullable enable
using System;
using System.Collections.Generic;

namespace Dbce.Wheel.Input
{
    /// <summary>A new, valid, non-stale read accepted by the existing reader. Masks are already applied.
    /// Generation is compared per instance; IdentityKey is opaque. Seen uses the injected local clock.</summary>
    public sealed class AcceptedBindingObservation
    {
        public Guid Instance { get; }
        public Guid Product { get; }
        public string Name { get; }
        public string IdentityKey { get; }
        public ulong Generation { get; }
        public ulong ReadSequence { get; }
        public double Seen { get; }
        public BindingCaptureSnapshot Sample { get; }
        public int[] Minimum { get; }
        public int[] Maximum { get; }
        public AcceptedBindingObservation(Guid instance, Guid product, string name, string identityKey,
            ulong generation, ulong readSequence, double seen, BindingCaptureSnapshot sample, int[] minimum, int[] maximum)
        {
            Instance = instance; Product = product; Name = name; IdentityKey = identityKey;
            Generation = generation; ReadSequence = readSequence; Seen = seen;
            Sample = new BindingCaptureSnapshot(sample.Axes, sample.Buttons, sample.Hats);
            Minimum = (int[])minimum.Clone(); Maximum = (int[])maximum.Clone();
        }
    }

    /// <summary>Single capture over accepted observations. The host admits actions and applies the canonical
    /// result to its draft; running input and Save remain outside this flow. Call Refresh before rendering.</summary>
    public sealed class SharedBindingCaptureFlow
    {
        private readonly Func<double> clock;
        private readonly Action<string, ControlBinding> captured;
        private readonly Dictionary<Guid, AcceptedBindingObservation> devices = new Dictionary<Guid, AcceptedBindingObservation>();
        // Keep sequence watermarks after Remove/stop so an old cached read cannot regain eligibility.
        private readonly Dictionary<Guid, AcceptedBindingObservation> reads = new Dictionary<Guid, AcceptedBindingObservation>();
        private SharedBindingCaptureSession? capture;
        private ulong generation;
        private double start, deadline;
        public string? CapturingAction { get; private set; }
        public Guid CaptureDevice { get; private set; }
        public string Message { get; set; } = "Bind a control, review the draft, then Save bindings. Restart to use saved changes.";
        public double SecondsLeft => CapturingAction == null ? 0 : Math.Max(0, deadline - clock());
        public bool HasFreshObservation
        { get { foreach (var value in devices.Values) if (Fresh(value)) return true; return false; } }
        public IReadOnlyList<AcceptedBindingObservation> Observations
        {
            get
            {
                var fresh = new List<AcceptedBindingObservation>();
                foreach (var value in devices.Values) if (Fresh(value)) fresh.Add(value);
                return fresh.AsReadOnly();
            }
        }
        public SharedBindingCaptureFlow(Func<double> clock, Action<string, ControlBinding> captured)
        { this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); this.captured = captured ?? throw new ArgumentNullException(nameof(captured)); }
        private bool Fresh(AcceptedBindingObservation value)
        {
            double age = clock() - value.Seen;
            return !double.IsNaN(age) && age >= 0 && age <= .25;
        }
        public bool Begin(string action, Guid instance, bool canEdit)
        {
            Refresh(canEdit);
            if (!canEdit || CapturingAction != null && CapturingAction != action) return false;
            if (ControlBinding.ShapeOf(action) == ControlShape.Unknown) return false;
            if (!devices.TryGetValue(instance, out var device) || !Fresh(device))
            { Message = "That saved device is not available for capture. Its prior binding was kept; restart or use standalone setup for new devices."; return false; }
            if (ControlBinding.ShapeOf(action) == ControlShape.Centred)
            {
                bool range = false;
                for (int i = 0; i < device.Sample.Axes.Length; i++) range |= device.Maximum[i] > device.Minimum[i];
                if (!range)
                { Message = "This reader has no qualified axis range for steering capture. Its prior binding was kept; use standalone setup."; return false; }
            }
            CapturingAction = action; CaptureDevice = instance; generation = device.Generation;
            start = clock(); deadline = start + (ControlBinding.ShapeOf(action) == ControlShape.Digital ? 10 : 11);
            capture = null;
            Message = "Release buttons and hats. Keep an axis released or centered for one second, then move one control.";
            return true;
        }
        public void Cancel(string? reason = null)
        {
            if (CapturingAction == null) return;
            CapturingAction = null; CaptureDevice = Guid.Empty; capture = null;
            Message = reason ?? "Binding canceled. The prior binding was kept.";
        }
        public void Remove(Guid instance, string reason)
        {
            devices.Remove(instance);
            if (CapturingAction != null && CaptureDevice == instance) Cancel(reason);
        }
        public void ReaderStopped()
        { devices.Clear(); Cancel("Capture reader stopped. The prior binding was kept."); }
        public void Refresh(bool canEdit)
        {
            if (CapturingAction == null) return;
            if (!canEdit) Cancel("Capture canceled because the settings lost focus or the main menu closed. The prior binding was kept.");
            else if (!devices.TryGetValue(CaptureDevice, out var device) || !Fresh(device) || device.Generation != generation)
                Cancel("Capture device disconnected or changed. The prior binding was kept.");
            else if (clock() >= deadline) Cancel("Capture timed out. Release other controls and try again; the prior binding was kept.");
        }
        public bool Observe(AcceptedBindingObservation value, bool canEdit)
        {
            if (value.Instance == Guid.Empty || value.Product == Guid.Empty || string.IsNullOrEmpty(value.IdentityKey) ||
                value.Generation == 0 || value.ReadSequence == 0 || !Fresh(value))
            { Remove(value.Instance, "Capture refused an unqualified or stale observation. The prior binding was kept."); return false; }
            if (value.Sample.Axes.Length > 8 || value.Sample.Buttons.Length > 128 || value.Sample.Hats.Length > 4 ||
                !SharedBindingCapture.RangesValid(value.Sample.Axes, value.Minimum, value.Maximum))
            { Remove(value.Instance, "Capture refused an out-of-range raw sample. No control from that frame was bound; the prior binding was kept."); return false; }
            if (reads.TryGetValue(value.Instance, out var previous))
            {
                bool changed = previous.Generation != value.Generation || previous.Product != value.Product || previous.IdentityKey != value.IdentityKey;
                if (changed && CapturingAction != null && CaptureDevice == value.Instance)
                    Cancel("Capture device disconnected or changed. The prior binding was kept.");
                if (value.Generation < previous.Generation || value.Generation == previous.Generation && value.ReadSequence <= previous.ReadSequence)
                { Refresh(canEdit); return false; } // A repeated read never refreshes Seen or advances capture edges.
            }
            devices[value.Instance] = value;
            reads[value.Instance] = value;
            Refresh(canEdit);
            if (CapturingAction == null || CaptureDevice != value.Instance) return true;
            bool analog = ControlBinding.ShapeOf(CapturingAction) != ControlShape.Digital;
            if (analog && clock() - start < 1) return true;
            if (capture == null)
            {
                capture = new SharedBindingCaptureSession(value.Sample, analog, value.Minimum, value.Maximum);
                Message = "Listening: move one control or press one button / hat direction. Release held buttons first.";
                return true;
            }
            var detected = capture.Read(value.Sample);
            if (detected == null) return true;
            try
            {
                var kind = (ControlKind)Enum.Parse(typeof(ControlKind), detected.Kind);
                var binding = ControlBinding.FromCapture(CapturingAction, kind, detected.Index, detected.Direction, detected.Rest,
                    value.Instance, value.Product, value.Name, kind == ControlKind.Axis ? value.Minimum[detected.Index] : 0,
                    kind == ControlKind.Axis ? value.Maximum[detected.Index] : 65535);
                string action = CapturingAction;
                Cancel(); // A callback is delivered at most once, including if its host refuses the draft edit.
                captured(action, binding);
                Message = "Binding captured into the draft. Review it and Save bindings; restart to use it.";
            }
            catch (ArgumentException ex) { Cancel(); Message = "Capture refused: " + ex.Message + ". The prior binding was kept."; }
            return true;
        }
    }
}
