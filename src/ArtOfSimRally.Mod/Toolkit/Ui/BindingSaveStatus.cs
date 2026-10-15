// MIT. Optional binding Save presentation state. No reader, writer, clock or UI dependency.
using System;

namespace Dbce.Wheel.Ui
{
    /// <summary>Optional companion to ISharedBindingHost; the existing host contract stays compatible.</summary>
    public interface ISharedBindingSaveStatus
    {
        string SaveFailure { get; }
    }

    /// <summary>One instance per binding host. Capture and general status messages cannot replace a Save failure.</summary>
    public sealed class BindingSaveStatus : ISharedBindingSaveStatus
    {
        public string SaveFailure { get; private set; } = "";

        public bool Record(bool saved, string message)
        {
            SaveFailure = saved ? "" : string.IsNullOrWhiteSpace(message)
                ? "Bindings could not be saved. The draft was kept." : message;
            return saved;
        }

        /// <summary>Call only after explicit Discard; a new host gets a new instance.</summary>
        public void Discard() { SaveFailure = ""; }
    }
}
