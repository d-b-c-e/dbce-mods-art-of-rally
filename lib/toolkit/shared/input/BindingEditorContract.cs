#nullable enable
using System.Collections.Generic;

#if DBCE_INPUT_NET35
using BindingRows = System.Collections.Generic.IList<Dbce.Wheel.Input.SharedBindingRowView>;
using DeviceRows = System.Collections.Generic.IList<Dbce.Wheel.Input.SharedBindingDeviceView>;
#else
using BindingRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Input.SharedBindingRowView>;
using DeviceRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Input.SharedBindingDeviceView>;
#endif

namespace Dbce.Wheel.Input
{
    public enum SharedBindingAccess { Missing, ReadOnly, Editable }
    public enum SharedBindingRowState { Unbound, Bound, Changed, Capturing, Error }
    // Applicability is host-declared, not inferred from a temporary Note or editing state.
    // Zero/default keeps existing filtered hosts source-compatible and their rows visible.
    public enum SharedBindingApplicability { Supported, Unsupported }
    public interface ISharedBindingHost
    {
        SharedBindingAccess Access { get; }
        string AccessReason { get; }
        string ActiveSource { get; }
        BindingRows Rows { get; }
        DeviceRows Devices { get; }
        string? CapturingAction { get; }
        double CaptureSecondsLeft { get; }
        string CaptureMessage { get; }
        bool HasDraft { get; }
        bool SavedPendingRestart { get; }
        void BeginCapture(string action);
        void BeginCapture(string action, string instanceGuid);
        void CancelCapture();
        void Clear(string action);
        void SetInverted(string action, bool inverted);
        bool Save(out string message);
        void Discard();
    }
    public sealed class SharedBindingRowView
    {
        public string Action { get; internal set; } = "";
        public string Label { get; internal set; } = "";
        public SharedBindingValue Active { get; internal set; }
        public SharedBindingValue? Draft { get; internal set; }
        public SharedBindingValue? NextStart { get; internal set; }
        public string Device { get; internal set; } = "";
        public bool IsAxis { get; internal set; }
        public bool ActiveInverted { get; internal set; }
        public bool? NextStartInverted { get; internal set; }
        public bool? DraftInverted { get; internal set; }
        public SharedBindingRowState State { get; internal set; }
        public SharedBindingApplicability Applicability { get; internal set; }
        public string Note { get; internal set; } = "";
    }
    public struct SharedBindingValue
    {
#if DBCE_INPUT_NET35
        // Auto-property getters on a struct are implicitly readonly, which references IsReadOnlyAttribute: mscorlib has
        // it only from 4.7.2, so Mono 2.0 lacks it. Explicit getters emit no attribute; the public surface is the same.
        private readonly bool _bound;
        private readonly string _text;
        public bool Bound { get { return _bound; } }
        public string Text { get { return _text; } }
        public SharedBindingValue(bool bound, string text) { _bound = bound; _text = text; }
#else
        public bool Bound { get; private set; }
        public string Text { get; private set; }
        public SharedBindingValue(bool bound, string text) { Bound = bound; Text = text; }
#endif
    }
    public sealed class SharedBindingDeviceView
    {
        public string InstanceGuid { get; internal set; } = "";
        public string ProductGuid { get; internal set; } = "";
        public string Name { get; internal set; } = "";
        public bool IsDefault { get; internal set; }
    }
}
