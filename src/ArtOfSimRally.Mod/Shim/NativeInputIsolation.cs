using System;
using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Shim
{
    internal interface IIsolationJoystick
    {
        object Token { get; }
        Guid Instance { get; }
        bool Enabled { get; set; }
    }
    internal interface IJoystickInventory
    {
        bool Ready { get; }
        IEnumerable<IIsolationJoystick> Joysticks { get; }
    }

    // Joystick enabled flags only. Never changes a player, map, backend, keyboard
    // or mouse. Each complete scan proves every mapped wheel was disabled before
    // this Unity frame; newly disabled values must age out of Rewired's cache.
    internal sealed class NativeInputIsolation
    {
        private sealed class Prior
        {
            internal IIsolationJoystick Joystick;
            internal bool Enabled, Changed;
        }
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        private readonly HashSet<Guid> owned;
        private readonly Dictionary<object, Prior> prior = new Dictionary<object, Prior>(new ReferenceComparer());
        private readonly IJoystickInventory inventory;
        private readonly int owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private int changedFrame = -1, scannedFrame = -1;
        private bool begun, restored;
        internal string Status { get; private set; } = "native isolation not started";

        internal NativeInputIsolation(IEnumerable<Guid> instances, IJoystickInventory inventory)
        {
            if (instances == null || inventory == null) throw new ArgumentNullException();
            owned = new HashSet<Guid>(instances); this.inventory = inventory;
            if (owned.Count == 0 || owned.Contains(Guid.Empty)) throw new ArgumentException("Core-bound instance identities required");
        }
        private void CheckThread() { if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("isolation owner thread required"); }
        internal bool Check(int frame)
        {
            CheckThread();
            if (restored) { Status = "isolation restored; new session required"; return false; }
            if (frame < scannedFrame) { Status = "isolation frame regressed"; return false; }
            scannedFrame = frame;
            try
            {
                if (!inventory.Ready) { changedFrame = frame; Status = "Rewired is not ready"; return false; }
                var all = new List<IIsolationJoystick>(); var seen = new HashSet<object>(new ReferenceComparer()); var found = new HashSet<Guid>();
                foreach (var joystick in inventory.Joysticks)
                {
                    if (joystick == null || joystick.Token == null || !seen.Add(joystick.Token) || all.Count == 256)
                        throw new InvalidOperationException("Rewired joystick inventory is incomplete or ambiguous");
                    // First qualify the entire inventory, before changing flags.
                    if (joystick.Instance == Guid.Empty)
                        throw new InvalidOperationException("Rewired joystick identity unavailable: cannot exclude a duplicate mapped wheel; no name/index fallback");
                    found.Add(joystick.Instance);
                    all.Add(joystick);
                }
                foreach (var id in owned)
                    if (!found.Contains(id)) throw new InvalidOperationException("Core-bound instance not observed in Rewired: absent device or RawInput GUID mapping remains unqualified");
                if (!begun) { begun = true; changedFrame = frame; }
                foreach (var joystick in all)
                {
                    if (!owned.Contains(joystick.Instance)) continue;
                    Prior state;
                    if (!prior.TryGetValue(joystick.Token, out state))
                    {
                        state = new Prior { Joystick = joystick, Enabled = joystick.Enabled };
                        prior.Add(joystick.Token, state); changedFrame = frame;
                    }
                    if (joystick.Enabled)
                    {
                        // A setter can change the flag and then throw. Record
                        // our attempted mutation before entering runtime code.
                        state.Changed = true; changedFrame = frame; joystick.Enabled = false;
                        if (joystick.Enabled) throw new InvalidOperationException("Rewired did not disable the mapped joystick");
                    }
                }
                bool clean = frame > changedFrame;
                Status = clean ? "mapped joystick identities isolated; independent native input" : "waiting one frame for cached wheel input to clear";
                return clean;
            }
            catch (Exception ex) { changedFrame = frame; Status = "native isolation unqualified: " + ex.Message; return false; }
        }
        internal bool Restore()
        {
            CheckThread(); restored = true; bool ok = true;
            foreach (var state in prior.Values)
            {
                if (!state.Changed) continue;
                try
                {
                    // Do not overwrite a later external re-enable. Only undo
                    // the false value this session itself wrote.
                    if (!state.Joystick.Enabled && state.Enabled) state.Joystick.Enabled = true;
                    state.Changed = false;
                }
                catch { ok = false; }
            }
            Status = ok ? "native joystick flags restored" : "one or more joystick flags could not be restored";
            return ok;
        }
    }
}
