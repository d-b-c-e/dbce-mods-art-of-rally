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
            var prior = new ProbeReceipt(request) { IdentityQualified = true, FlagsDisabled = true, FlagsRestored = true, RawAxesNeutralAfterDisable = true, Instances = ids };
            request.Mode = "Apply"; request.ValidateApply(prior, ids); check(true, "neutral-at-rest receipt must preserve cache inconclusive separately");
            prior.RawAxesNeutralAfterDisable = false; refuses(() => request.ValidateApply(prior, ids), "Apply used nonneutral raw cache"); prior.RawAxesNeutralAfterDisable = true;
            refuses(() => request.ValidateApply(prior, new[] { Guid.NewGuid() }), "Apply used another identity");
            prior.FlagsRestored = false; refuses(() => request.ValidateApply(prior, ids), "Apply used failed restore"); prior.FlagsRestored = true;
            string path = Path.Combine(result, "receipt.json"); ProbeRequest.Write(path, prior);
            var read = ProbeRequest.Read<ProbeReceipt>(path); check(read.FlagsRestored && read.Instances[0] == ids[0], "Unity-compatible receipt serializer lost identity");
            refuses(() => ProbeRequest.Write(path, prior), "probe overwrote prior receipt");
        }
        finally { Directory.Delete(root, true); }
        return n;
    }
}
