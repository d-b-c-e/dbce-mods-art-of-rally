using System;
using System.Text;
using System.Security.Cryptography;
using ArtOfSimRally.Mod.Shim;

internal static class ProfileInfoTests
{
    private const string Wheel = "d71b8350-61b7-11f1-8001-444553540000";
    private const string Product = "0006346e-0000-0000-0000-504944564944";
    private static int checks;
    private static void Check(bool condition, string why) { checks++; if (!condition) throw new Exception(why); }
    private static void Refuses(Action action, string why)
    { bool refused = false; try { action(); } catch { refused = true; } Check(refused, why); }
    internal static int Decode()
    {
        checks = 0;
        var profile = Encoding.UTF8.GetBytes("frozen profile bytes"); string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(profile)).Replace("-", "");
        string report = "{\"schema\":1,\"action\":\"steer\",\"status\":\"bound\",\"reason\":\"\",\"sha256\":\"" + hash +
            "\",\"kind\":\"axis\",\"instance\":\"" + Wheel + "\",\"product\":\"" + Product + "\",\"name\":\"Shared wheel\"}";
        Func<string, SharedSteeringTarget> read = json => ShimProfileNative.Decode(Encoding.UTF8.GetBytes(json + "\0"), profile);
        var target = read(report);
        Check(target.Instance == Wheel && target.Name == "Shared wheel" && target.Reason == null, "parsed report lost the steering identity");
        Refuses(() => read(report.Replace(hash, new string('0', 64))), "different startup bytes accepted");
        Refuses(() => read(report.Replace("\"schema\":1", "\"schema\":2")), "unknown report schema accepted");
        Refuses(() => read(report.Replace("\"steer\"", "\"throttle\"")), "another action accepted for FFB");
        Refuses(() => read(report.Replace("\"axis\"", "\"button\"")), "non-axis steering target accepted");
        Refuses(() => read(report.Replace(Wheel, Guid.Empty.ToString("D"))), "zero instance accepted");
        Refuses(() => read(report.Replace(Product, Guid.Empty.ToString("D"))), "zero product accepted");
        Refuses(() => read(report.Replace("\"bound\"", "\"invalid\"")), "invalid report exposed a target");
        foreach (string status in new[] { "unbound", "invalid" })
        {
            var refused = read(report.Replace("\"bound\"", "\"" + status + "\"").Replace("\"axis\"", "\"\"").Replace(Wheel, "").Replace(Product, ""));
            Check(refused.Instance == null && refused.Reason.Contains("shared profile") && refused.Reason.Contains("restart"), "refusal lacks correction path");
        }
        Refuses(() => ShimProfileNative.Decode(Encoding.UTF8.GetBytes(report), profile), "unterminated report accepted");
        return checks;
    }
    // Actual optional export only. No Core host/provider/thread/device/force open occurs.
    internal static int Actual(string dll)
    {
        checks = 0;
        string text = "[Controls]\nSchema = 1\nProfile = Fresh shared only\nsteer = axis 0 dev={" + Wheel + "} prod={" + Product + "} range=0..65535 rest=32768 travel=+1 name=\"Shared wheel\"\n";
        var valid = ShimProfileNative.ReadSteering(dll, Encoding.UTF8.GetBytes(text));
        Check(valid.Instance == Wheel && valid.Name == "Shared wheel", "actual Core profile export target");
        foreach (string invalid in new[] { text + "steer =\n", text + "brake = malformed\n", text.Replace("Schema = 1", "Schema = 2"),
            text + "confirm = button 0 dev={" + Wheel + "} prod={00000001-0000-0000-0000-504944564944}\n" })
            Check(ShimProfileNative.ReadSteering(dll, Encoding.UTF8.GetBytes(invalid)).Instance == null, "actual full profile refusal");
        Check(ShimProfileNative.ReadSteering(dll, Encoding.UTF8.GetBytes("[Controls]\nSchema = 1\nsteer =\n")).Instance == null, "actual unbound steering refused");
        return checks;
    }
}
