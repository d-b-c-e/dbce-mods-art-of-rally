using System.Collections.Generic;
using Dbce.Wheel.Input;

namespace ArtOfSimRally.Mod
{
    // Art's pure inspection export accepts at most 64 KiB for the entire profile.
    // All pending-state and checked-save behavior is shared with other in-game hosts.
    internal sealed class SharedBindingDraft
    {
        private readonly ControlProfileDraft shared;
        internal ControlProfileDocument Active => shared.Active;
        internal ControlProfileDocument Saved => shared.Saved;
        internal IReadOnlyDictionary<string, string> Changes => shared.Changes;
        internal bool SavedPendingRestart => shared.SavedPendingRestart;
        internal bool HasDraft => shared.HasDraft;
        internal SharedBindingDraft(byte[] bytes) { shared = new ControlProfileDraft(bytes, 65536); }
        internal ControlBinding Binding(string action) => shared.Binding(action);
        internal void Set(string action, string value) => shared.Set(action, value);
        internal void Discard() => shared.Discard();
        internal bool Save(string path, out string message) => shared.Save(path, out message);
    }
}