using System;
using System.Collections.Generic;
using System.Reflection;
using ArtOfSimRally.Mod.Shim;
using Rewired.ControllerExtensions;
using Rewired.HID.Drivers;

// DualShock 4-shaped fixtures carrying the reviewed pair's exact type and
// field names (diag-03), so the production reader walks its real chain here.
namespace Rewired.ControllerExtensions
{
    internal interface IFixtureDriver { }
    internal sealed class DualShock4Extension
    {
        internal class bCAGPoVsjuhfnJyRuyREpdPDdAUT
        {
            internal IFixtureDriver QYKkhsALSllcmVrwQkNAjMiAhLuHA;
        }
        internal bCAGPoVsjuhfnJyRuyREpdPDdAUT sUHnBwREVEYFPxdqqIRaVAMCiTIW;
        internal ushort Vendor = 0x054C, Product = 0x05C4;
    }
}
namespace Rewired.HID.Drivers
{
    internal sealed class DualShock4Driver : IFixtureDriver
    {
        internal object oNZzuYgyerFmIKdVWnZydeacAQSD;
    }
}
internal interface EwvLJlmwdeVauFlwEvWtjvDditYI { }
internal sealed class aTbFSaxgzEWBvObtdpuQSYzCiioT
{
    internal class hGVykObfUvbkhQHpTnWBQJESEppf
    {
        internal EwvLJlmwdeVauFlwEvWtjvDditYI oNZzuYgyerFmIKdVWnZydeacAQSD;
    }
}
internal sealed class eOjwogCoQuQOjGajnUgPmfeWSfsn : EwvLJlmwdeVauFlwEvWtjvDditYI
{
    internal string dCANkCDaitOZGMJDNGKkRJJgljXi;
}
// The interface's other implementer: same field, not the observed type.
internal sealed class vUeJqYJdCDgSVsLYxtlhrEubOqiC : EwvLJlmwdeVauFlwEvWtjvDditYI
{
    internal string dCANkCDaitOZGMJDNGKkRJJgljXi;
}

internal static class Ds4IdentityTests
{
    internal const string Path = @"\\?\hid#vid_054c&pid_05c4&rev_0100#2&fb2d4e8&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private sealed class OtherDriver : IFixtureDriver { }
    private sealed class IntPath { internal int dCANkCDaitOZGMJDNGKkRJJgljXi = 0; }
    private static readonly Assembly Here = typeof(Ds4IdentityTests).Assembly;
    private const string Admitted = "(admitted)";

    private static DualShock4Extension Pad(string path = Path) => new DualShock4Extension
    {
        sUHnBwREVEYFPxdqqIRaVAMCiTIW = new DualShock4Extension.bCAGPoVsjuhfnJyRuyREpdPDdAUT
        {
            QYKkhsALSllcmVrwQkNAjMiAhLuHA = new DualShock4Driver
            {
                oNZzuYgyerFmIKdVWnZydeacAQSD = new aTbFSaxgzEWBvObtdpuQSYzCiioT.hGVykObfUvbkhQHpTnWBQJESEppf
                { oNZzuYgyerFmIKdVWnZydeacAQSD = new eOjwogCoQuQOjGajnUgPmfeWSfsn { dCANkCDaitOZGMJDNGKkRJJgljXi = path } }
            }
        }
    };
    private static aTbFSaxgzEWBvObtdpuQSYzCiioT.hGVykObfUvbkhQHpTnWBQJESEppf Device(DualShock4Extension pad) =>
        (aTbFSaxgzEWBvObtdpuQSYzCiioT.hGVykObfUvbkhQHpTnWBQJESEppf)((DualShock4Driver)pad.sUHnBwREVEYFPxdqqIRaVAMCiTIW.QYKkhsALSllcmVrwQkNAjMiAhLuHA).oNZzuYgyerFmIKdVWnZydeacAQSD;
    private static eOjwogCoQuQOjGajnUgPmfeWSfsn Info(DualShock4Extension pad) => (eOjwogCoQuQOjGajnUgPmfeWSfsn)Device(pad).oNZzuYgyerFmIKdVWnZydeacAQSD;

    // Hashes in call order: Rewired_Core first, then Rewired_Windows.
    private sealed class Hashes
    {
        internal readonly string[] Values; internal int Calls;
        internal Hashes(string core, string windows) { Values = new[] { core, windows }; }
        internal string Next(Assembly ignored) => Values[Calls++ % 2];
    }
    private static PinnedDs4Identity Reader(Hashes hashes = null, (bool, string)[] types = null, string[] fields = null) =>
        new PinnedDs4Identity(Here, Here, (hashes ?? new Hashes(PinnedDs4Identity.CoreSha256, PinnedDs4Identity.WindowsSha256)).Next,
            types ?? PinnedDs4Identity.Types, fields ?? PinnedDs4Identity.Fields);
    private static RewiredIdentity Read(PinnedDs4Identity reader, DualShock4Extension pad) => reader.Read(pad, () => pad.Vendor, () => pad.Product);

    // The joystick view's DualShock 4 branch: identity from the production reader on every query.
    private sealed class Joystick : IRawIsolationJoystick, IRawIdentityDiagnostic, IIdentityLifetimeDiagnostic
    {
        internal Guid Id = Guid.NewGuid();
        internal DualShock4Extension Extension = Pad();
        internal PinnedDs4Identity Reader = Ds4IdentityTests.Reader();
        internal bool On = true, Present = true;
        public object Token => this;
        public Guid Instance => Id;
        public bool Enabled { get => On; set => On = value; }
        public string Name => "Sony DualShock 4";
        public float[] ReadRawAxes() => new[] { On ? .5f : 0f };
        public string InterfacePath => Read(Reader, Extension).Path;
        public string ProductGuid => Read(Reader, Extension).Product;
        public object ExtensionToken => Read(Reader, Extension).Token;
        public bool Connected => Present;
    }
    // A RawInput-read wheel, as IdentityLeaseTests' device.
    private sealed class Wheel : IRawIsolationJoystick, IRawIdentityDiagnostic, IIdentityLifetimeDiagnostic
    {
        internal Guid Id = Guid.NewGuid(), Product = new Guid("0006346e-0000-0000-0000-504944564944");
        internal object Extension = new object();
        internal string Path = @"\\?\hid#vid_346e&pid_0006&mi_02#8&32e6eb88&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
        internal bool On = true, Identity = true;
        public object Token => this;
        public Guid Instance => Id;
        public bool Enabled { get => On; set => On = value; }
        public string Name => "MOZA R12 Base";
        public float[] ReadRawAxes() => new[] { On ? -1f : 0f };
        public string InterfacePath => On && Identity ? Path : (Identity ? "" : null);
        public string ProductGuid => Identity ? (On ? Product : Guid.Empty).ToString("D") : null;
        public object ExtensionToken => Identity ? Extension : null;
        public bool Connected => true;
    }
    private sealed class Inventory : IJoystickInventory
    {
        internal List<IIsolationJoystick> Items = new List<IIsolationJoystick>();
        public bool Ready => true;
        public IEnumerable<IIsolationJoystick> Joysticks => Items;
    }
    // Core binds the DualShock 4 (shared pad) beside an owned wheel reader.
    private sealed class Session
    {
        internal Joystick Pad = new Joystick();
        internal Wheel Wheel = new Wheel();
        internal Inventory Inventory = new Inventory();
        internal OwnedDeviceIdentity[] Owned;
        internal Guid[] Required, Mapped;
        internal ProbeIdentityLease Lease;
        internal NativeInputIsolation Isolation;
        internal Session(bool bindPad = true)
        {
            Inventory.Items.AddRange(new IIsolationJoystick[] { Wheel, Pad });
            var ownedPad = new OwnedDeviceIdentity { Instance = Guid.NewGuid(), Product = new Guid("05c4054c-0000-0000-0000-504944564944"), Generation = 5, InterfacePath = Path };
            var ownedWheel = new OwnedDeviceIdentity { Instance = Guid.NewGuid(), Product = Wheel.Product, Generation = 3, InterfacePath = Wheel.Path };
            Owned = new[] { ownedWheel, ownedPad };
            Required = new[] { bindPad ? ownedPad.Instance : ownedWheel.Instance };
        }
        internal Session Begin()
        {
            var before = ProbeObservation.Capture(Inventory);
            Mapped = ProbeIdentityMapper.Match(Required, Owned, before);
            Lease = new ProbeIdentityLease(before, Mapped, 7);
            Isolation = new NativeInputIsolation(Mapped, Inventory);
            return this;
        }
        // SharedInputSession.VerifyIsolation's join, then IdentityRestored's.
        internal Guid[] Verify() => ProbeIdentityMapper.Match(Required, Owned, Lease.Verify(ProbeObservation.Capture(Inventory), 7, Isolation.OwnsDisabled));
        internal Guid[] Restored() => ProbeIdentityMapper.Match(Required, Owned, Lease.Verify(ProbeObservation.Capture(Inventory), 7, token => false));
    }

    internal static int Run()
    {
        int count = 0;
        Action<bool, string> check = (ok, why) => { count++; if (!ok) throw new Exception(why); };
        Func<Action, string> refusal = act => { try { act(); return Admitted; } catch (Exception ex) { return ex.Message; } };

        // The reviewed pair and chain are exactly diag-03's.
        check(PinnedDs4Identity.CoreSha256 == "2DD147A1E7EA3A8ED7FECE35EA6554784ED3F039F5E291F3F0B6F877379B0655" &&
              PinnedDs4Identity.WindowsSha256 == "4389BCA4D00D68B600972FCD2A0D5A21208517DF7893A6A4602E740D6691417C", "reviewed hashes changed");
        check(String.Join("|", Array.ConvertAll(PinnedDs4Identity.Types, t => (t.Core ? "core:" : "windows:") + t.Type)) ==
              "core:Rewired.ControllerExtensions.DualShock4Extension|core:Rewired.ControllerExtensions.DualShock4Extension+bCAGPoVsjuhfnJyRuyREpdPDdAUT|" +
              "windows:Rewired.HID.Drivers.DualShock4Driver|windows:aTbFSaxgzEWBvObtdpuQSYzCiioT+hGVykObfUvbkhQHpTnWBQJESEppf|windows:eOjwogCoQuQOjGajnUgPmfeWSfsn", "reviewed chain types changed");
        check(String.Join("|", PinnedDs4Identity.Fields) == "sUHnBwREVEYFPxdqqIRaVAMCiTIW|QYKkhsALSllcmVrwQkNAjMiAhLuHA|oNZzuYgyerFmIKdVWnZydeacAQSD|oNZzuYgyerFmIKdVWnZydeacAQSD" &&
              PinnedDs4Identity.PathField == "dCANkCDaitOZGMJDNGKkRJJgljXi", "reviewed chain fields changed");

        // The pinned read: the exact path, the extension as token, product in the owned readers' layout.
        var pad = Pad(); var hashes = new Hashes(PinnedDs4Identity.CoreSha256, PinnedDs4Identity.WindowsSha256); var reader = Reader(hashes);
        var id = Read(reader, pad);
        check(id.Path == Path && ReferenceEquals(id.Token, pad) && id.Product == "05c4054c-0000-0000-0000-504944564944", "pinned DualShock 4 identity not read exactly");
        pad.Vendor = 0x346E; pad.Product = 0x0006;
        check(Read(reader, pad).Product == "0006346e-0000-0000-0000-504944564944", "product layout differs from RawInput's");
        pad.Vendor = 0; pad.Product = 0;
        check(Read(reader, pad).Product == Guid.Empty.ToString("D") && Read(reader, pad).Path == Path, "unavailable product not reported as unavailable");
        for (int i = 0; i < 50; i++) Read(reader, pad);
        check(reader.Validations == 1 && hashes.Calls == 2, "assembly pair hashed per read, not once");

        // Hash gate: refused by name before any chain field is read, and the refusal is cached.
        foreach (var (core, windows, named) in new[] { ("00", PinnedDs4Identity.WindowsSha256, "Rewired_Core"), (PinnedDs4Identity.CoreSha256, "00", "Rewired_Windows"),
                                                      (PinnedDs4Identity.WindowsSha256, PinnedDs4Identity.CoreSha256, "Rewired_Core") })
        {
            var wrong = new Hashes(core, windows); var gated = Reader(wrong); var broken = Pad(); broken.sUHnBwREVEYFPxdqqIRaVAMCiTIW = null;
            string why = refusal(() => Read(gated, broken));
            check(why.StartsWith("DualShock 4 identity refused: " + named) && why.IndexOf("null", StringComparison.Ordinal) < 0, "hash gate not first or unnamed: " + why);
            check(refusal(() => Read(gated, Pad())) == why && wrong.Calls == 2, "hash refusal not cached for the pair");
        }
        int vendorReads = 0;
        check(refusal(() => Reader(new Hashes("00", "00")).Read(Pad(), () => { vendorReads++; return 1; }, () => { vendorReads++; return 1; })) != Admitted && vendorReads == 0,
            "public HID getters read after a refusal");

        // Chain: exact types at every link, no other implementer, no null, no empty path.
        reader = Reader();
        check(refusal(() => reader.Read(new object(), () => 1, () => 1)).Contains("extension System.Object is not"), "non-DualShock 4 extension walked");
        var p = Pad(); p.sUHnBwREVEYFPxdqqIRaVAMCiTIW = null;
        check(refusal(() => Read(reader, p)).Contains(".sUHnBwREVEYFPxdqqIRaVAMCiTIW is null"), "null source admitted");
        p = Pad(); p.sUHnBwREVEYFPxdqqIRaVAMCiTIW.QYKkhsALSllcmVrwQkNAjMiAhLuHA = new OtherDriver();
        check(refusal(() => Read(reader, p)).Contains("not the reviewed Rewired.HID.Drivers.DualShock4Driver"), "other driver admitted");
        p = Pad(); ((DualShock4Driver)p.sUHnBwREVEYFPxdqqIRaVAMCiTIW.QYKkhsALSllcmVrwQkNAjMiAhLuHA).oNZzuYgyerFmIKdVWnZydeacAQSD = null;
        check(refusal(() => Read(reader, p)).Contains("DualShock4Driver.oNZzuYgyerFmIKdVWnZydeacAQSD is null"), "null HID device admitted");
        p = Pad(); Device(p).oNZzuYgyerFmIKdVWnZydeacAQSD = new vUeJqYJdCDgSVsLYxtlhrEubOqiC { dCANkCDaitOZGMJDNGKkRJJgljXi = Path };
        check(refusal(() => Read(reader, p)).Contains("not the reviewed eOjwogCoQuQOjGajnUgPmfeWSfsn"), "unobserved device-information implementer admitted");
        foreach (var empty in new[] { "", null })
            check(refusal(() => Read(reader, Pad(empty))) == "DualShock 4 identity refused: interface path empty", "empty path admitted: " + (empty ?? "null"));
        Info(p = Pad()).dCANkCDaitOZGMJDNGKkRJJgljXi = Path.ToUpperInvariant();
        check(Read(reader, p).Path == Path.ToUpperInvariant(), "adapter normalized the path (the mapper alone folds case)");

        // Members: an absent type, field or a non-String path refuses by name, once.
        var types = (((bool, string)[])PinnedDs4Identity.Types.Clone()); types[3] = (false, "aTbFSaxgzEWBvObtdpuQSYzCiioT+absent");
        check(refusal(() => Read(Reader(null, types), Pad())) == "DualShock 4 identity refused: type aTbFSaxgzEWBvObtdpuQSYzCiioT+absent absent", "absent type not named");
        var fields = (string[])PinnedDs4Identity.Fields.Clone(); fields[1] = "renamed";
        var renamed = Reader(null, null, fields);
        check(refusal(() => Read(renamed, Pad())).EndsWith("DualShock4Extension+bCAGPoVsjuhfnJyRuyREpdPDdAUT.renamed absent") && renamed.Validations == 1, "absent member not named");
        types = (((bool, string)[])PinnedDs4Identity.Types.Clone()); types[4] = (false, typeof(IntPath).FullName);
        check(refusal(() => Read(Reader(null, types), Pad())).EndsWith("absent or not a String"), "non-String path member admitted");

        // The production mapper and lease with the DualShock 4 bound: enabled, owned-disabled, restored.
        var s = new Session().Begin();
        check(s.Mapped.Length == 1 && s.Mapped[0] == s.Pad.Id, "pinned DualShock 4 not joined by exact path and product");
        check(new HashSet<Guid>(s.Verify()).SetEquals(s.Mapped), "enabled lease join failed");
        check(!s.Isolation.Check(1) && !s.Pad.On && s.Wheel.On && s.Isolation.OwnsDisabled(s.Pad), "isolation did not disable only the DualShock 4");
        check(new HashSet<Guid>(s.Verify()).SetEquals(s.Mapped) && s.Isolation.Check(2), "owned-disabled DualShock 4 lost its identity join");
        check(s.Isolation.Restore() && s.Pad.On && new HashSet<Guid>(s.Restored()).SetEquals(s.Mapped), "restored DualShock 4 identity not fresh");

        Action<Action<Session>, string> rejects = (mutate, why) =>
        {
            var t = new Session().Begin(); t.Isolation.Check(1); mutate(t);
            string first = refusal(() => t.Verify());
            check(first != Admitted, why);
            t.Pad.Extension = Pad(); t.Pad.Reader = Reader(); t.Pad.On = true;
            check(refusal(() => t.Verify()) != Admitted, "failed lease revived: " + why);
        };
        var kept = new Session().Begin(); kept.Isolation.Check(1); var original = kept.Pad.Extension; kept.Pad.Extension = Pad();
        check(refusal(() => kept.Verify()) != Admitted, "replaced DualShock 4 extension admitted the cached identity");
        kept.Pad.Extension = original;
        check(refusal(() => kept.Verify()) != Admitted, "original extension revived an invalidated lease");
        rejects(t => t.Pad.Extension.sUHnBwREVEYFPxdqqIRaVAMCiTIW = null, "broken chain while disabled admitted");
        rejects(t => Info(t.Pad.Extension).dCANkCDaitOZGMJDNGKkRJJgljXi = Path.Replace("0000#", "0001#"), "new device path while disabled admitted");
        rejects(t => { t.Pad.Extension.Vendor = 0; t.Pad.Extension.Product = 0; }, "partial identity (path without product) admitted while disabled");
        rejects(t => { t.Pad.On = true; t.Pad.Extension.Product = 0x09CC; }, "same path new product admitted");
        rejects(t => t.Pad.Present = false, "disconnected DualShock 4 admitted");
        var broke = new Session().Begin(); broke.Isolation.Check(1); broke.Pad.Extension.sUHnBwREVEYFPxdqqIRaVAMCiTIW = null;
        check(refusal(() => broke.Verify()).EndsWith("(DualShock 4 identity refused: Rewired.ControllerExtensions.DualShock4Extension.sUHnBwREVEYFPxdqqIRaVAMCiTIW is null)"),
            "lease refusal lost the DualShock 4 reason");

        // Refusals stay refusals at the join, with the reason named.
        s = new Session(); s.Pad.Reader = Reader(new Hashes(PinnedDs4Identity.CoreSha256, "00"));
        string hashRefusal = refusal(() => s.Begin());
        check(hashRefusal.StartsWith("Rewired identity/path unavailable (DualShock 4 identity refused: Rewired_Windows 00 is not the reviewed 4389BCA4)"), "hash refusal unnamed at the join: " + hashRefusal);
        s = new Session(); s.Pad.Extension.Product = 0x09CC;
        check(refusal(() => s.Begin()) == "owned HID path has a different Rewired product identity", "product second check skipped");
        s = new Session(); s.Pad.Extension.Vendor = 0; s.Pad.Extension.Product = 0;
        check(refusal(() => s.Begin()) == "Rewired identity/path unavailable: cannot exclude a duplicate mapped wheel", "unavailable product admitted at the join");
        s = new Session(); Info(s.Pad.Extension).dCANkCDaitOZGMJDNGKkRJJgljXi = Path.Replace("0000#", "0001#");
        check(refusal(() => s.Begin()) == "Core-owned HID path not observed in Rewired; no identity fallback", "unowned DualShock 4 path joined");

        // An unrelated shared wheel beside a connected DualShock 4: the pad's exact path proves it is not a duplicate.
        s = new Session(false).Begin();
        check(s.Mapped.Length == 1 && s.Mapped[0] == s.Wheel.Id, "wheel not joined beside a DualShock 4");
        s.Isolation.Check(1);
        check(!s.Wheel.On && s.Pad.On && new HashSet<Guid>(s.Verify()).SetEquals(s.Mapped), "isolation touched the unrelated DualShock 4");
        // No unknown-row skip: a row with neither RawInput nor DualShock 4 identity still refuses.
        s = new Session(false); s.Inventory.Items.Add(new Wheel { Identity = false });
        check(refusal(() => s.Begin()) == "Rewired identity/path unavailable: cannot exclude a duplicate mapped wheel", "unknown row skipped");
        return count;
    }
}
