using System;
using System.Collections.Generic;
using System.IO;
using ArtOfSimRally.Mod.Shim;

internal static class ProbeTests
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
    internal static int Run()
    {
        int n = 0; Action<bool, string> check = (ok, why) => { n++; if (!ok) throw new Exception(why); };
        var wheel = new Joystick(); var other = new Joystick(); var inventory = new Inventory(); inventory.Items.AddRange(new[] { wheel, other });
        var ids = new[] { wheel.Id }; var receipt = new ProbeReceipt();
        receipt.Before = ProbeObservation.Capture(inventory);
        var isolation = new NativeInputIsolation(ids, inventory);
        check(!isolation.Check(1) && isolation.Check(2), "probe must wait for a new Unity frame");
        receipt.Disabled = ProbeObservation.Capture(inventory); ProbeObservation.CompareDisabled(receipt, ids);
        check(receipt.FlagsDisabled && receipt.RawAxesNeutralAfterDisable && receipt.NonzeroCacheClearObserved && other.On,
            "resting pedal -1 transition did not qualify without physical movement");
        check(isolation.Restore() && ProbeObservation.Restored(receipt.Before, ProbeObservation.Capture(inventory)), "exact flag restoration unproved");
        wheel.Raw = 0; receipt.Before = ProbeObservation.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(3); isolation.Check(4); receipt.Disabled = ProbeObservation.Capture(inventory); ProbeObservation.CompareDisabled(receipt, ids);
        check(receipt.FlagsDisabled && receipt.RawAxesNeutralAfterDisable && !receipt.NonzeroCacheClearObserved && receipt.RawAxisQualification.StartsWith("inconclusive"), "zero at rest falsely claimed cache transition"); isolation.Restore();
        wheel.Raw = -1; wheel.Stuck = true; receipt.Before = ProbeObservation.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(5); isolation.Check(6); receipt.Disabled = ProbeObservation.Capture(inventory); ProbeObservation.CompareDisabled(receipt, ids);
        check(receipt.FlagsDisabled && !receipt.RawAxesNeutralAfterDisable, "stuck raw cache marked qualified"); isolation.Restore(); wheel.Stuck = false;
        wheel.ThrowRaw = true; receipt.Before = ProbeObservation.Capture(inventory); isolation = new NativeInputIsolation(ids, inventory);
        isolation.Check(7); isolation.Check(8); receipt.Disabled = ProbeObservation.Capture(inventory); ProbeObservation.CompareDisabled(receipt, ids);
        check(receipt.FlagsDisabled && !receipt.RawAxesNeutralAfterDisable && receipt.Disabled[0].RawError != null, "raw failure lost independent identity/flag result"); isolation.Restore();
        var alien = new Joystick { Id = wheel.Id }; inventory.Items[0] = alien;
        check(!ProbeObservation.Restored(receipt.Before, ProbeObservation.Capture(inventory)), "replacement object masqueraded as restored original");
        Guid rawId = Guid.NewGuid(), productId = Guid.NewGuid();
        var owner = new OwnedDeviceIdentity { Instance = wheel.Id, Product = productId, Generation = 5, InterfacePath = @"\\?\hid#vid_x&mi_00&col01#exact" };
        var row = new JoystickObservation { Instance = rawId.ToString(), ProductGuid = productId.ToString(), InterfacePath = owner.InterfacePath.ToUpperInvariant() };
        var mapped = ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row });
        check(mapped.Length == 1 && mapped[0] == rawId && rawId != wheel.Id, "exact path/product did not join distinct GUID namespaces");
        Action<Action, string> reject = (call, why) => { bool refused = false; try { call(); } catch { refused = true; } check(refused, why); };
        row.ProductGuid = Guid.NewGuid().ToString(); reject(() => ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row }), "path alone bypassed wrong product"); row.ProductGuid = productId.ToString();
        row.InterfacePath = owner.InterfacePath.Replace("col01", "col02"); row.Instance = wheel.Id.ToString();
        reject(() => ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row }), "same GUID bypassed wrong collection path"); row.InterfacePath = owner.InterfacePath; row.Instance = rawId.ToString();
        var unknown = new JoystickObservation { Instance = Guid.NewGuid().ToString(), ProductGuid = productId.ToString() };
        reject(() => ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row, unknown }), "unknown unrelated path could conceal duplicate wheel");
        reject(() => ProbeIdentityMapper.Match(ids, new[] { owner, owner }, new[] { row }), "ambiguous owner path accepted");
        var alias = new JoystickObservation { Instance = rawId.ToString(), ProductGuid = productId.ToString(), InterfacePath = owner.InterfacePath + "-different" };
        reject(() => ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row, alias }), "RawInput GUID alias disabled unrelated path");
        check(ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row, row }).Length == 1, "same-interface duplicate cannot be disabled by isolation inventory");
        owner.Generation = 0; reject(() => ProbeIdentityMapper.Match(ids, new[] { owner }, new[] { row }), "unqualified source epoch accepted");
        owner.Generation = 5;

        string root = Path.Combine(Path.GetTempPath(), "art-shim-probe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string game = Path.Combine(root, "copy"), result = Path.Combine(root, "result"); Directory.CreateDirectory(game); Directory.CreateDirectory(result);
            string mod = Path.Combine(game, "mod.dll"), native = Path.Combine(game, "native.dll"), settings = Path.Combine(game, "Settings.xml");
            File.WriteAllText(mod, "fake artifact"); File.WriteAllText(native, "fake native"); File.WriteAllText(settings, "accepted bytes");
            var request = new ProbeRequest { Schema = 1, Mode = "Observe", Nonce = Guid.NewGuid().ToString("N"), ExpiresUtc = DateTime.UtcNow.AddMinutes(5).ToString("o"),
                GameRoot = game, SourceGameRoot = Path.Combine(root, "owner"), Result = result, ShimPath = native, ProfilePath = settings,
                ModSha256 = ProbeRequest.Hash(mod), NativeSha256 = ProbeRequest.Hash(native), ShimSha256 = ProbeRequest.Hash(native), SettingsSha256 = ProbeRequest.Hash(settings), ProfileSha256 = ProbeRequest.Hash(settings) };
            request.Validate(DateTime.UtcNow, game, mod, native, settings); check(true, "valid probe pins");
            Action<Action, string> refuses = (call, why) => { bool refused = false; try { call(); } catch { refused = true; } check(refused, why); };
            refuses(() => request.Validate(DateTime.UtcNow.AddHours(1), game, mod, native, settings), "expired request accepted");
            refuses(() => request.Validate(DateTime.UtcNow, root, mod, native, settings), "wrong running game root accepted");
            request.SourceGameRoot = game; refuses(() => request.Validate(DateTime.UtcNow, game, mod, native, settings), "owner install accepted as disposable clone"); request.SourceGameRoot = root;
            File.AppendAllText(settings, "changed"); refuses(() => request.Validate(DateTime.UtcNow, game, mod, native, settings), "changed settings accepted"); File.WriteAllText(settings, "accepted bytes");
            var owned = new[] { owner }; var rewired = new[] { rawId };
            var prior = new ProbeReceipt(request) { IdentityQualified = true, FlagsDisabled = true, FlagsRestored = true, RawAxesNeutralAfterDisable = true, Instances = ids, OwnedDevices = owned, RewiredInstances = rewired };
            request.Mode = "Apply"; request.ValidateApply(prior, ids, owned, rewired); check(true, "neutral-at-rest receipt must preserve cache inconclusive separately");
            prior.RawAxesNeutralAfterDisable = false; refuses(() => request.ValidateApply(prior, ids, owned, rewired), "Apply used nonneutral raw cache"); prior.RawAxesNeutralAfterDisable = true;
            refuses(() => request.ValidateApply(prior, new[] { Guid.NewGuid() }, owned, rewired), "Apply used another identity");
            prior.FlagsRestored = false; refuses(() => request.ValidateApply(prior, ids, owned, rewired), "Apply used failed restore"); prior.FlagsRestored = true;
            refuses(() => request.ValidateApply(prior, ids, owned, new[] { Guid.NewGuid() }), "Apply used a different RawInput identity");
            var moved = new OwnedDeviceIdentity { Instance = owner.Instance, Product = owner.Product, Generation = 999, InterfacePath = owner.InterfacePath + "different" };
            refuses(() => request.ValidateApply(prior, ids, new[] { moved }, rewired), "Apply used another HID path");
            moved.InterfacePath = owner.InterfacePath; request.ValidateApply(prior, ids, new[] { moved }, rewired); check(true, "cold-process source generation must not be mistaken for identity");
            string path = Path.Combine(result, "receipt.json"); ProbeRequest.Write(path, prior);
            var read = ProbeRequest.Read<ProbeReceipt>(path); check(read.FlagsRestored && read.Instances[0] == ids[0], "Unity-compatible receipt serializer lost identity");
            refuses(() => ProbeRequest.Write(path, prior), "probe overwrote prior receipt");
        }
        finally { Directory.Delete(root, true); }
        return n;
    }
}
