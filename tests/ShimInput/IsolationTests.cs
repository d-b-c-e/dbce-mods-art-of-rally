using System;
using System.Collections.Generic;
using System.Threading;
using ArtOfSimRally.Mod.Shim;

internal static class IsolationTests
{
    private sealed class Joystick : IIsolationJoystick
    {
        internal Guid Id;
        internal bool Value = true, ThrowGet, ThrowSet, ThrowAfterSet;
        internal int Writes;
        public object Token => this;
        public Guid Instance { get { if (ThrowGet) throw new Exception("fixture property failure"); return Id; } }
        public bool Enabled { get => Value; set { Writes++; if (ThrowSet) throw new Exception("fixture setter failure"); Value = value; if (ThrowAfterSet) throw new Exception("fixture post-write failure"); } }
    }
    private sealed class Inventory : IJoystickInventory
    {
        internal bool IsReady = true;
        internal readonly List<IIsolationJoystick> Items = new List<IIsolationJoystick>();
        public bool Ready => IsReady;
        public IEnumerable<IIsolationJoystick> Joysticks => Items;
    }
    internal static int Run()
    {
        int checks = 0; Action<bool, string> check = (ok, why) => { checks++; if (!ok) throw new Exception(why); };
        Guid wheel = Guid.NewGuid(), pad = Guid.NewGuid();
        var first = new Joystick { Id = wheel }; var duplicate = new Joystick { Id = wheel }; var other = new Joystick { Id = pad };
        var off = new Joystick { Id = wheel, Value = false }; var inventory = new Inventory(); inventory.Items.AddRange(new[] { first, duplicate, other, off });
        var isolation = new NativeInputIsolation(new[] { wheel }, inventory);
        check(!isolation.Check(10) && !first.Value && !duplicate.Value && other.Value && off.Writes == 0, "exact matching joystick isolation only");
        check(!isolation.Check(10), "first frame reused cached native values");
        check(isolation.Check(11) && first.Writes == 1 && duplicate.Writes == 1 && other.Writes == 0, "next-frame native keyboard qualification");
        first.Value = true; check(!isolation.Check(12) && !first.Value && first.Writes == 2, "external re-enable was missed");
        check(isolation.Check(13), "re-disable barrier did not clear");
        var reconnect = new Joystick { Id = wheel }; inventory.Items.Add(reconnect);
        check(!isolation.Check(14) && !reconnect.Value && isolation.Check(15), "new duplicate/reconnected wheel was missed");
        inventory.Items.Add(new Joystick { Id = Guid.Empty });
        check(!isolation.Check(16) && isolation.Status.Contains("cannot exclude a duplicate"), "unknown identity silently treated unrelated");
        inventory.Items.RemoveAt(inventory.Items.Count - 1); check(isolation.Check(17), "requalified scan did not age out");
        first.ThrowGet = true; check(!isolation.Check(18), "throwing runtime getter marked clean"); first.ThrowGet = false;
        check(isolation.Check(19), "getter recovery did not requalify");
        inventory.IsReady = false; check(!isolation.Check(20), "unready Rewired marked clean"); inventory.IsReady = true;
        check(isolation.Check(21), "ready scan did not recover");
        check(!isolation.Check(20), "old frame accepted");
        bool denied = false; var thread = new Thread(() => { try { isolation.Check(22); } catch (InvalidOperationException) { denied = true; } }); thread.Start(); thread.Join();
        check(denied, "wrong thread touched runtime properties");
        check(isolation.Restore() && first.Value && duplicate.Value && reconnect.Value && !off.Value && off.Writes == 0 && other.Writes == 0, "restore changed unrelated/previously disabled state");
        int writes = first.Writes; check(isolation.Restore() && first.Writes == writes && !isolation.Check(23), "restored session resumed or rewrote flags");
        var missing = new NativeInputIsolation(new[] { Guid.NewGuid() }, inventory);
        check(!missing.Check(1) && missing.Status.Contains("RawInput GUID"), "different RawInput GUID silently accepted as absent");
        first.ThrowSet = true; var failure = new NativeInputIsolation(new[] { wheel }, inventory);
        check(!failure.Check(1), "failed enabled setter marked clean"); first.ThrowSet = false;
        check(failure.Restore(), "refused partial handoff could not restore");
        first.ThrowAfterSet = true; var partial = new NativeInputIsolation(new[] { wheel }, inventory);
        check(!partial.Check(1) && !first.Value, "fixture did not mutate before throwing"); first.ThrowAfterSet = false;
        check(partial.Restore() && first.Value, "setter changed then threw and left owned joystick disabled");
        return checks;
    }
}
