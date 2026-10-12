using System;
using System.Collections.Generic;
using ArtOfSimRally.Mod.Shim;

internal static class IdentityLeaseTests
{
    private sealed class Device : IRawIsolationJoystick, IRawIdentityDiagnostic, IIdentityLifetimeDiagnostic
    {
        internal Guid Id = Guid.NewGuid(), Product = Guid.NewGuid();
        internal object Extension = new object();
        internal string Path = @"\\?\hid#fixture#col01";
        internal bool On = true, Present = true, HideWhileEnabled, ThrowIdentity;
        public object Token => this;
        public Guid Instance => Id;
        public bool Enabled { get => On; set => On = value; }
        public string Name => "fixture";
        public float[] ReadRawAxes() => new[] { On ? -1f : 0f };
        public string InterfacePath { get { if (ThrowIdentity) throw new Exception("getter threw"); return On && !HideWhileEnabled ? Path : ""; } }
        public string ProductGuid => (On && !HideWhileEnabled ? Product : Guid.Empty).ToString("D");
        public object ExtensionToken => Extension;
        public bool Connected => Present;
    }
    private sealed class Inventory : IJoystickInventory
    {
        internal List<IIsolationJoystick> Items = new List<IIsolationJoystick>();
        public bool Ready => true;
        public IEnumerable<IIsolationJoystick> Joysticks => Items;
    }
    private sealed class Fixture
    {
        internal Device Wheel = new Device(), Other = new Device { Path = @"\\?\hid#other#col02" };
        internal Inventory Inventory = new Inventory();
        internal NativeInputIsolation Isolation;
        internal ProbeIdentityLease Lease;
        internal JoystickObservation[] Before;
        internal Fixture()
        {
            Inventory.Items.AddRange(new[] { Wheel, Other });
            Before = ProbeObservation.Capture(Inventory);
            Lease = new ProbeIdentityLease(Before, new[] { Wheel.Id }, 17);
            Isolation = new NativeInputIsolation(new[] { Wheel.Id }, Inventory);
        }
        internal JoystickObservation[] Verify(long epoch = 17) => Lease.Verify(ProbeObservation.Capture(Inventory), epoch, Isolation.OwnsDisabled);
    }
    internal static int Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, why) => { count++; if (!ok) throw new Exception(why); };
        var f = new Fixture();
        check(f.Verify()[0].InterfacePath == f.Wheel.Path, "live admission unavailable");
        check(!f.Isolation.Check(1) && f.Isolation.OwnsDisabled(f.Wheel), "owned disabled witness not tied to actual setter");
        var raw = ProbeObservation.Capture(f.Inventory);
        check(raw[0].InterfacePath == "" && raw[0].ProductGuid == Guid.Empty.ToString("D"), "fixture must hide identity like shipped RawInput getter");
        var joined = f.Lease.Verify(raw, 17, f.Isolation.OwnsDisabled);
        check(joined[0].InterfacePath == f.Wheel.Path && joined[0].ProductGuid == f.Wheel.Product.ToString("D"), "captured identity unavailable while same object disabled");
        check(raw[0].InterfacePath == "" && raw[0].ProductGuid == Guid.Empty.ToString("D"), "witness rewrote raw diagnostic observation");
        check(f.Isolation.Check(2), "identity lease bypassed one-frame isolation cache wait");
        check(f.Isolation.Restore() && !f.Isolation.OwnsDisabled(f.Wheel), "restore retained disabled ownership");
        check(f.Verify()[0].Enabled && f.Verify()[0].InterfacePath == f.Wheel.Path, "fresh restored identity missing");
        f = new Fixture(); f.Before[0].InterfacePath = "mutated receipt";
        f.Isolation.Check(1); check(f.Verify()[0].InterfacePath == f.Wheel.Path, "receipt mutation changed frozen identity witness");
        Action<Action<Fixture>, string> rejects = (mutate, why) =>
        {
            var test = new Fixture(); test.Isolation.Check(1); mutate(test);
            bool refused = false; try { test.Verify(); } catch { refused = true; }
            check(refused, why);
            test.Wheel.On = true; test.Wheel.Present = true; test.Wheel.HideWhileEnabled = false; test.Wheel.ThrowIdentity = false;
            refused = false; try { test.Verify(); } catch { refused = true; }
            check(refused, "failed lease revived: " + why);
        };
        rejects(t => t.Wheel.Extension = new object(), "extension replacement admitted cached identity");
        rejects(t => t.Wheel.Id = Guid.NewGuid(), "persistent GUID change admitted cache");
        rejects(t => t.Wheel.Present = false, "disconnected controller admitted cache");
        rejects(t => t.Inventory.Items[0] = new Device { Id = t.Wheel.Id, Product = t.Wheel.Product, Extension = t.Wheel.Extension }, "replacement joystick object admitted cache");
        rejects(t => t.Inventory.Items.RemoveAt(1), "missing unrelated object admitted");
        rejects(t => t.Inventory.Items.Add(new Device()), "new unrelated object admitted");
        rejects(t => t.Other.On = false, "external disabled controller borrowed witness");
        rejects(t => { t.Wheel.On = true; t.Wheel.HideWhileEnabled = true; }, "enabled missing identity borrowed cache");
        rejects(t => { t.Wheel.On = true; t.Wheel.Path += "-different"; }, "same GUID new interface reused lease");
        rejects(t => { t.Wheel.On = true; t.Wheel.Product = Guid.NewGuid(); }, "same path new product reused lease");
        rejects(t => t.Wheel.ThrowIdentity = true, "throwing identity getter borrowed cache");
        f = new Fixture(); f.Isolation.Check(1);
        bool bad = false; try { f.Verify(19); } catch { bad = true; }
        check(bad, "same-frame disconnect and reconnect reused witness");
        bad = false; try { f.Verify(17); } catch { bad = true; }
        check(bad, "epoch regression revived invalid lease");
        f = new Fixture(); f.Wheel.On = false;
        bad = false; try { f.Verify(); } catch { bad = true; }
        check(bad, "externally disabled mapped wheel borrowed ownership");
        f = new Fixture(); f.Isolation.Check(1); f.Isolation.Restore(); f.Wheel.Path += "-new";
        bad = false; try { f.Verify(); } catch { bad = true; }
        check(bad, "restore qualified without fresh original path");
        foreach (string malformed in new[] { "not-a-guid", null, "partial" })
        {
            f = new Fixture(); f.Isolation.Check(1); var rows = ProbeObservation.Capture(f.Inventory);
            if (malformed == "partial") rows[0].InterfacePath = f.Wheel.Path; else rows[0].ProductGuid = malformed;
            bad = false; try { f.Lease.Verify(rows, 17, f.Isolation.OwnsDisabled); } catch { bad = true; }
            check(bad, "unexpected hidden-identity representation admitted: " + malformed);
        }
        return count;
    }
}
