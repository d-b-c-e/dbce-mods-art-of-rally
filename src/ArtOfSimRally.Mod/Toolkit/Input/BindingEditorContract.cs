using System.Collections.Generic;

namespace Dbce.Wheel.Input
{
    public enum SharedBindingAccess { Missing, ReadOnly, Editable }
    public enum SharedBindingRowState { Unbound, Bound, Changed, Capturing, Error }
    public interface ISharedBindingHost
    {
        SharedBindingAccess Access { get; }
        string AccessReason { get; }
        string ActiveSource { get; }
        IReadOnlyList<SharedBindingRowView> Rows { get; }
        IReadOnlyList<SharedBindingDeviceView> Devices { get; }
        string CapturingAction { get; }
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
        public string Action { get; internal set; }
        public string Label { get; internal set; }
        public SharedBindingValue Active { get; internal set; }
        public SharedBindingValue? Draft { get; internal set; }
        public SharedBindingValue? NextStart { get; internal set; }
        public string Device { get; internal set; }
        public bool IsAxis { get; internal set; }
        public bool ActiveInverted { get; internal set; }
        public bool? NextStartInverted { get; internal set; }
        public bool? DraftInverted { get; internal set; }
        public SharedBindingRowState State { get; internal set; }
        public string Note { get; internal set; }
    }
    public struct SharedBindingValue
    {
        public bool Bound { get; private set; }
        public string Text { get; private set; }
        public SharedBindingValue(bool bound, string text) { Bound = bound; Text = text; }
    }
    public sealed class SharedBindingDeviceView
    {
        public string InstanceGuid { get; internal set; }
        public string ProductGuid { get; internal set; }
        public string Name { get; internal set; }
        public bool IsDefault { get; internal set; }
    }
}
