using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Shim;

public sealed record ShimProfileInfo(string Profile, string Revision, string Transmission, string Sha256);

public static class ShimProfile
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    // Pass the exact section bytes to native Core. This locates a section; it
    // deliberately does not reinterpret the controls grammar.
    public static byte[] ControlsSection(byte[] all)
    {
        ArgumentNullException.ThrowIfNull(all);
        if (all.Length > 1024 * 1024) throw new InvalidDataException("Controls file exceeds 1 MiB.");
        int at = all.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        int start = -1, end = all.Length;
        while (at < all.Length)
        {
            int lineStart = at, next = Array.IndexOf(all, (byte)'\n', at);
            at = next < 0 ? all.Length : next + 1;
            string line = Encoding.ASCII.GetString(all, lineStart, at - lineStart).Trim();
            if (line == "[Controls]")
            {
                if (start >= 0) throw new InvalidDataException("Duplicate [Controls] section.");
                start = lineStart;
            }
            else if (start >= 0 && end == all.Length && line.StartsWith('[')) end = lineStart;
        }
        if (start < 0) throw new InvalidDataException("No [Controls] section.");
        return all[start..end];
    }

    // Core's report is authoritative. Refuse if it did not accept these exact
    // bytes, including whitespace, newline style and comments.
    public static ShimProfileInfo Verify(byte[] section, string coreInfo)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(coreInfo);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in coreInfo.Split('\n'))
        {
            int at = line.IndexOf('=');
            if (at < 0) continue;
            if (!fields.TryAdd(line[..at], line[(at + 1)..].TrimEnd('\r')))
                throw new InvalidDataException("Duplicate Core profile field.");
        }
        string hash = Convert.ToHexString(SHA256.HashData(section));
        if (!fields.TryGetValue("ok", out var ok) || ok != "1" ||
            !fields.TryGetValue("sha256", out var accepted) ||
            !hash.Equals(accepted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Core did not accept the exact Controls bytes.");
        return new ShimProfileInfo(fields.GetValueOrDefault("profile", ""), fields.GetValueOrDefault("revision", ""),
            fields.GetValueOrDefault("transmission", "Unavailable"), hash);
    }

    // Consumers can parse this with the shared ControlBinding source when they
    // need a steering device identity for FFB. Core acceptance is checked here
    // before extracting the text; no second Controls grammar is accepted.
    public static string? SteeringBinding(byte[] section, string coreInfo)
    {
        ArgumentNullException.ThrowIfNull(section);
        Verify(section, coreInfo);
        foreach (string line in StrictUtf8.GetString(section).Split('\n'))
        {
            var parts = line.Trim().Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == "steer") return parts[1].Trim();
        }
        return null;
    }
}
