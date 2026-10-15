using System;
using System.IO;
using System.Linq;
using System.Text;
using ArtOfSimRally.Mod;
using Dbce.Wheel.Input;

internal static class Program
{
    private static int checks;
    private static readonly Guid Wheel = new Guid("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Product = new Guid("22222222-2222-2222-2222-222222222222");
    private static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    private static void Put(byte[] bytes, int at, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, at, 4);
    private static byte[] Metadata(ulong generation = 1, int maximum = 65535)
    {
        var bytes = new byte[144]; Put(bytes, 0, 144); Put(bytes, 4, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(generation), 0, bytes, 8, 8);
        Buffer.BlockCopy(Wheel.ToByteArray(), 0, bytes, 16, 16); Buffer.BlockCopy(Product.ToByteArray(), 0, bytes, 32, 16);
        Put(bytes, 48, 1); Put(bytes, 52, 1); Put(bytes, 56, 1); bytes[64] = 128;
        Put(bytes, 112, maximum); return bytes;
    }
    private static byte[] Sample(int axis = 32767, bool button7 = false, bool invalid = false, ulong generation = 1)
    {
        var bytes = new byte[232]; Put(bytes, 0, 232); Put(bytes, 4, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(generation), 0, bytes, 8, 8);
        bytes[50] = invalid ? (byte)0 : (byte)1; Put(bytes, 52, axis);
        bytes[91] = button7 ? (byte)128 : (byte)0;
        for (int i = 0; i < 4; i++) Put(bytes, 212 + i * 4, -1);
        return bytes;
    }
    private static SharedBindingRowView Row(ISharedBindingHost host, string id) => host.Rows.Single(r => r.Action == id);
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "art-shared-bindings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string axis = ControlBinding.FromCapture("steer", ControlKind.Axis, 0, 1, 32767, Wheel, Product, "Fixture wheel").WithInverted(true).ToString();
            string button = ControlBinding.FromCapture("confirm", ControlKind.Button, 7, 1, 0, Wheel, Product, "Fixture wheel").ToString();
            string text = "; owner note\r\n[Controls]\r\nSchema = 1\r\nProfile = Fixture\r\nRevision = active\r\nTransmission = Automatic\r\nsteer = " + axis +
                "\r\nconfirm = " + button + "\r\nFuture = keep\r\n[Other]\r\nOwner = untouched\r\n";
            string path = Path.Combine(root, "dbce-controls.ini"); File.WriteAllText(path, text, new UTF8Encoding(false));
            double time = 0; bool allowed = true;
            var host = new SharedBindingCaptureHost(path, File.ReadAllBytes(path), () => time, () => allowed);
            var metadata = Metadata();
            host.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample());
            Check(host.Access == SharedBindingAccess.Editable && host.Devices.Count == 1, "only an accepted qualified cache enables capture");
            var mismatched = new SharedBindingCaptureHost(path, File.ReadAllBytes(path), () => time, () => true);
            mismatched.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample(), Guid.NewGuid());
            Check(mismatched.Access == SharedBindingAccess.ReadOnly && !mismatched.HasDraft, "slot identity mismatch cannot select a substitute identity");
            host.BeginCapture("steer"); Check(host.CapturingAction == "steer", "one capture active");
            time = 1; host.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample());
            time = 1.01; host.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample(50000));
            Check(host.CapturingAction == null && host.HasDraft, "deliberate movement creates a draft");
            Check(Row(host, "steer").ActiveInverted && Row(host, "steer").DraftInverted == false, "active stays frozen beside draft");
            string message;
            Check(host.Save(out message), message);
            Check(!host.HasDraft && host.SavedPendingRestart && Row(host, "steer").NextStart.HasValue, "saved next-start distinct from unsaved draft");
            Check(Row(host, "steer").ActiveInverted && Row(host, "steer").NextStartInverted == false, "Save does not apply the new axis live");
            Check(File.ReadAllText(path).Contains("confirm = " + button) && File.ReadAllText(path).Contains("Future = keep\r\n[Other]\r\nOwner = untouched"), "one action leaves unrelated bindings and lines unchanged");
            Check(Directory.GetFiles(root, "*.bindings-backup-*").Length == 1, "Save retains original backup");
            host.SetInverted("steer", true);
            Check(host.HasDraft && Row(host, "steer").DraftInverted == true && Row(host, "steer").NextStartInverted == false, "unsaved edit can overlay saved pending state");
            host.Discard(); Check(!host.HasDraft && host.SavedPendingRestart, "Discard does not erase already saved pending state");
            host.Clear("confirm");
            Check(Row(host, "confirm").Draft.HasValue && !Row(host, "confirm").Draft.Value.Bound, "Clear has explicit Unbound draft, never null");
            File.AppendAllText(path, "; external change\r\n");
            Check(!host.Save(out message) && host.HasDraft && message.Contains("changed"), "external mutation refuses Save and retains draft");
            host.Discard();
            host.BeginCapture("confirm"); host.CancelCapture();
            Check(!host.HasDraft && host.CapturingAction == null, "Cancel leaves prior binding intact");
            host.BeginCapture("confirm"); allowed = false;
            Check(host.CapturingAction == null && !host.HasDraft, "focus/menu loss cancels capture"); allowed = true;
            time += .01; host.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample());
            host.BeginCapture("confirm"); host.ObserveAccepted("Fixture wheel", "owned-fixture-path", metadata, Sample(invalid: true));
            Check(host.CapturingAction == null && !host.HasDraft, "invalid sample cannot become a false axis/button movement");

            string smallPath = Path.Combine(root, "small-controls.ini"); File.WriteAllText(smallPath, text, new UTF8Encoding(false));
            var small = new SharedBindingCaptureHost(smallPath, Encoding.UTF8.GetBytes(text), () => time, () => allowed);
            var smallMeta = Metadata(maximum: 1023); time = 2;
            small.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(511));
            small.BeginCapture("steer"); time = 3; small.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(511));
            time = 3.01; small.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(800));
            Check(small.HasDraft, "metadata range scales shared threshold for a 0..1023 axis");
            Check(small.Save(out message), message);
            var savedSmall = new ControlProfileDocument(File.ReadAllText(smallPath)).Bindings["steer"];
            Check(savedSmall.Minimum == 0 && savedSmall.Maximum == 1023 && savedSmall.Rest == 511 && savedSmall.Travel == 1 && savedSmall.Index == 0 &&
                savedSmall.Device == Wheel && savedSmall.Product == Product, "saved capture preserves exact metadata range, rest, direction, object and identities");
            small.Discard(); small.BeginCapture("confirm");
            time += .01; small.ObserveAccepted("Small-range fixture", "owned-small-path", Metadata(2, 1023), Sample(511, generation: 2));
            Check(small.CapturingAction == null && !small.HasDraft, "generation change cannot retarget a capture");

            var bounds = new SharedBindingCaptureHost(path, Encoding.UTF8.GetBytes(text), () => time, () => true);
            bounds.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(511));
            bounds.BeginCapture("steer"); time += 1; bounds.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(511));
            time += .01; bounds.ObserveAccepted("Small-range fixture", "owned-small-path", smallMeta, Sample(50000, button7: true));
            Check(!bounds.HasDraft && bounds.CapturingAction == null && Row(bounds, "steer").ActiveInverted, "out-of-range valid-byte frame binds neither axis nor simultaneous button");
            var masks = new SharedBindingCaptureHost(path, Encoding.UTF8.GetBytes(text), () => time, () => true);
            var maskedMeta = Metadata(); Put(maskedMeta, 48, 0); Put(maskedMeta, 52, 0); Put(maskedMeta, 56, 0); maskedMeta[64] = 0;
            masks.ObserveAccepted("Masked fixture", "owned-mask-path", maskedMeta, Sample()); masks.BeginCapture("steer");
            Check(masks.CapturingAction == null && !masks.HasDraft, "steering refuses absent or unknown range");
            masks.BeginCapture("confirm"); masks.ObserveAccepted("Masked fixture", "owned-mask-path", maskedMeta, Sample());
            var maskedSample = Sample(50000, button7: true); Put(maskedSample, 212, 9000);
            time += .01; masks.ObserveAccepted("Masked fixture", "owned-mask-path", maskedMeta, maskedSample);
            Check(!masks.HasDraft, "axis, range, button and hat masks suppress unavailable objects");
            masks.ReaderStopped(); Check(masks.CapturingAction == null && !masks.HasDraft, "stopped reader cancels capture without a replacement");
            masks.ObserveAccepted("Masked fixture", "owned-mask-path", metadata, Sample()); masks.Clear("confirm");
            masks.BeginCapture("steer"); time += .3;
            Check(masks.CapturingAction == null && masks.HasDraft, "stale cache cancels capture and preserves an earlier draft");
            masks.ObserveAccepted("Masked fixture", "owned-mask-path", metadata, Sample()); masks.BeginCapture("steer");
            for (int i = 0; i < 120; i++) { time += .1; masks.ObserveAccepted("Masked fixture", "owned-mask-path", metadata, Sample()); }
            Check(masks.CapturingAction == null && masks.HasDraft, "timeout with fresh unchanged samples preserves an earlier draft");
            var missing = new SharedBindingCaptureHost(path, null, () => time, () => true);
            Check(missing.Access == SharedBindingAccess.Missing && missing.AccessReason.Contains("Controls Setup"), "missing profile gives a no-Wheelkit standalone route");
            Console.WriteLine("PASS " + checks + " shared binding host checks; no device, game, window or force output.");
            return 0;
        }
        finally
        {
            string expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(root)), expectedParent, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(root).StartsWith("art-shared-bindings-", StringComparison.Ordinal)) Directory.Delete(root, true);
        }
    }
}
