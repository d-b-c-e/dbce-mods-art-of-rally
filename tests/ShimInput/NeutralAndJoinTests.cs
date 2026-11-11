using System;
using System.Collections.Generic;
using ArtOfSimRally.Mod.Shim;

// The isolated wheel must read neutral before the first frame (JoystickObservations.NeutralFailure), restoring
// re-enables exactly the original objects, and IdentityJoin joins Art's readers to Rewired only by exact path and product.
internal static class NeutralAndJoinTests
{
    private sealed class Joystick : IRawIsolationJoystick
    {
        internal Guid Id = Guid.NewGuid();
        internal bool On = true, Stuck, ThrowRaw;
        internal float Raw = -1;
        public object Token => this;
        public Guid Instance => Id;
        public bool Enabled { get => On; set => On = value; }
        public string Name => "pedal resting at -1";
        public float[] ReadRawAxes() { if (ThrowRaw) throw new Exception("raw getter failure"); return new[] { On || Stuck ? Raw : 0f }; }
    }
    private sealed class Inventory : IJoystickInventory
    {
        internal List<IIsolationJoystick> Items = new List<IIsolationJoystick>();
        public bool Ready => true;
        public IEnumerable<IIsolationJoystick> Joysticks => Items;
    }
    // Every original object is present again with its original enabled flag.
    private static bool Restored(JoystickObservation[] before, JoystickObservation[] after)
    {
        if (before.Length != after.Length) return false;
        foreach (var row in before)
        {
            int matches = 0;
            foreach (var now in after) if (ReferenceEquals(row.Token, now.Token) && row.Instance == now.Instance && row.Enabled == now.Enabled) matches++;
            if (matches != 1) return false;
        }
        return true;
    }
    internal static int Run()
    {
        int n = 0; Action<bool, string> check = (ok, why) => { n++; if (!ok) throw new Exception(why); };
        var wheel = new Joystick(); var other = new Joystick(); var inventory = new Inventory(); inventory.Items.AddRange(new[] { wheel, other });
        var ids = new[] { wheel.Id };
        var before = JoystickObservations.Capture(inventory);
        var isolation = new NativeInputIsolation(ids, inventory);
        check(!isolation.Check(1) && isolation.Check(2), "isolation must wait for a new Unity frame");
        check(JoystickObservations.NeutralFailure(before, JoystickObservations.Capture(inventory), ids) == null && other.On,
            "resting pedal -1 transition did not qualify without physical movement");
        check(isolation.Restore() && Restored(before, JoystickObservations.Capture(inventory)), "exact flag restoration unproved");
        wheel.Raw = 0; before = JoystickObservations.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(3); isolation.Check(4);
        check(JoystickObservations.NeutralFailure(before, JoystickObservations.Capture(inventory), ids) == null, "zero at rest refused"); isolation.Restore();
        wheel.Raw = -1; wheel.Stuck = true; before = JoystickObservations.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(5); isolation.Check(6);
        check((JoystickObservations.NeutralFailure(before, JoystickObservations.Capture(inventory), ids) ?? "").StartsWith("unqualified"), "stuck raw cache marked qualified");
        isolation.Restore(); wheel.Stuck = false;
        wheel.ThrowRaw = true; before = JoystickObservations.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(7); isolation.Check(8); var disabled = JoystickObservations.Capture(inventory);
        check(disabled[0].RawError != null && !disabled[0].Enabled && (JoystickObservations.NeutralFailure(before, disabled, ids) ?? "").StartsWith("unqualified"),
            "raw failure lost independent identity/flag result");
        isolation.Restore(); wheel.ThrowRaw = false;
        // A flag the session did not change is a failure even with neutral axes.
        before = JoystickObservations.Capture(inventory);
        check((JoystickObservations.NeutralFailure(before, JoystickObservations.Capture(inventory), ids) ?? "").Length > 0, "an enabled owned wheel passed as isolated");
        var alien = new Joystick { Id = wheel.Id }; inventory.Items[0] = alien;
        check(!Restored(before, JoystickObservations.Capture(inventory)), "replacement object masqueraded as restored original");
        Guid rawId = Guid.NewGuid(), productId = Guid.NewGuid();
        var owner = new OwnedDeviceIdentity { Instance = wheel.Id, Product = productId, Generation = 5, InterfacePath = @"\\?\hid#vid_x&mi_00&col01#exact" };
        var row = new JoystickObservation { Instance = rawId.ToString(), ProductGuid = productId.ToString(), InterfacePath = owner.InterfacePath.ToUpperInvariant() };
        var mapped = IdentityJoin.Match(ids, new[] { owner }, new[] { row });
        check(mapped.Length == 1 && mapped[0] == rawId && rawId != wheel.Id, "exact path/product did not join distinct GUID namespaces");
        Action<Action, string> reject = (call, why) => { bool refused = false; try { call(); } catch { refused = true; } check(refused, why); };
        row.ProductGuid = Guid.NewGuid().ToString(); reject(() => IdentityJoin.Match(ids, new[] { owner }, new[] { row }), "path alone bypassed wrong product"); row.ProductGuid = productId.ToString();
        row.InterfacePath = owner.InterfacePath.Replace("col01", "col02"); row.Instance = wheel.Id.ToString();
        reject(() => IdentityJoin.Match(ids, new[] { owner }, new[] { row }), "same GUID bypassed wrong collection path"); row.InterfacePath = owner.InterfacePath; row.Instance = rawId.ToString();
        var unknown = new JoystickObservation { Instance = Guid.NewGuid().ToString(), ProductGuid = productId.ToString() };
        reject(() => IdentityJoin.Match(ids, new[] { owner }, new[] { row, unknown }), "unknown unrelated path could conceal duplicate wheel");
        reject(() => IdentityJoin.Match(ids, new[] { owner, owner }, new[] { row }), "ambiguous owner path accepted");
        var alias = new JoystickObservation { Instance = rawId.ToString(), ProductGuid = productId.ToString(), InterfacePath = owner.InterfacePath + "-different" };
        reject(() => IdentityJoin.Match(ids, new[] { owner }, new[] { row, alias }), "RawInput GUID alias disabled unrelated path");
        check(IdentityJoin.Match(ids, new[] { owner }, new[] { row, row }).Length == 1, "same-interface duplicate cannot be disabled by isolation inventory");
        owner.Generation = 0; reject(() => IdentityJoin.Match(ids, new[] { owner }, new[] { row }), "unqualified source epoch accepted");
        return n;
    }
}
