using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
#pragma warning disable 0649 // DataContract fields are populated by the diagnostic request/receipt serializer.

namespace ArtOfSimRally.Mod.Shim
{
    // Diagnostic transport only: the existing Core parses the unchanged profile.
    // No saved backend setting and no controls/profile grammar lives here.
    [DataContract]
    internal sealed class ProbeRequest
    {
        [DataMember(IsRequired = true)] public int Schema;
        [DataMember(IsRequired = true)] public string Mode, Nonce, ExpiresUtc, GameRoot, SourceGameRoot, Result;
        [DataMember(IsRequired = true)] public string ModSha256, NativeSha256, ShimSha256, SettingsSha256, ProfileSha256;
        [DataMember(IsRequired = true)] public string ShimPath, ProfilePath;
        [DataMember] public string ObserveReceipt;
        internal DateTime Deadline => DateTime.Parse(ExpiresUtc, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
        internal static T Read<T>(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > 1024 * 1024) throw new InvalidDataException("probe document size");
            using (var stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        internal static void Write<T>(string path, T value)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }
        internal static string Hash(string path)
        {
            using (var file = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
        internal static bool SamePath(string a, string b) => String.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        internal static void Pin(string path, string expected)
        {
            if (expected == null || expected.Length != 64 || !String.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("probe hash mismatch: " + path);
        }
        internal void Validate(DateTime now, string gameRoot, string modPath, string nativePath, string settingsPath)
        {
            Guid nonce;
            if (Schema != 1 || (Mode != "Observe" && Mode != "Apply") || !Guid.TryParseExact(Nonce, "N", out nonce) || nonce == Guid.Empty)
                throw new InvalidDataException("invalid probe request");
            if (Deadline <= now || Deadline > now.AddMinutes(30)) throw new InvalidDataException("probe request expired or beyond 30 minutes");
            if (SamePath(GameRoot, SourceGameRoot) || !SamePath(GameRoot, gameRoot) || !Path.GetFullPath(modPath).StartsWith(Path.GetFullPath(GameRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("probe requires its exact disposable game root");
            if (!SamePath(Path.GetDirectoryName(nativePath), Path.GetDirectoryName(modPath))) throw new InvalidDataException("probe native DLL must be beside the copied mod");
            if (!Directory.Exists(Result) || File.Exists(Path.Combine(Result, "probe-receipt.json")))
                throw new InvalidDataException("probe result must be prepared and unused");
            Pin(modPath, ModSha256); Pin(nativePath, NativeSha256); Pin(settingsPath, SettingsSha256);
            Pin(ShimPath, ShimSha256); Pin(ProfilePath, ProfileSha256);
        }
        internal void ValidateApply(ProbeReceipt prior, Guid[] instances, OwnedDeviceIdentity[] owned, Guid[] rewired)
        {
            if (Mode != "Apply") return;
            if (prior == null || prior.Mode != "Observe" || !prior.IdentityQualified || !prior.IdentityRestored || !prior.FlagsDisabled || !prior.FlagsRestored ||
                !prior.RawAxesNeutralAfterDisable || prior.ModSha256 != ModSha256 || prior.NativeSha256 != NativeSha256 ||
                prior.ShimSha256 != ShimSha256 || prior.SettingsSha256 != SettingsSha256 || prior.ProfileSha256 != ProfileSha256 ||
                prior.Instances == null || !new System.Collections.Generic.HashSet<Guid>(prior.Instances).SetEquals(instances) ||
                prior.RewiredInstances == null || rewired == null || !new System.Collections.Generic.HashSet<Guid>(prior.RewiredInstances).SetEquals(rewired) ||
                !ProbeIdentityMapper.SameBindings(prior.OwnedDevices, owned))
                throw new InvalidDataException("Apply probe needs matching observed identities, disable/restore and neutral raw-axis receipt");
        }
    }

    [DataContract]
    internal sealed class ProbeReceipt
    {
        [DataMember] public int Schema = 1;
        [DataMember] public string Mode, Nonce, Status, CompletedUtc;
        [DataMember] public string ModSha256, NativeSha256, ShimSha256, SettingsSha256, ProfileSha256;
        [DataMember] public bool NoForceArmed, IdentityQualified, IdentityRestored, FlagsDisabled, FlagsRestored, RawAxesNeutralAfterDisable, NonzeroCacheClearObserved, Applied;
        [DataMember] public long IdentityEpochBefore, IdentityEpochAfter;
        [DataMember] public Guid[] Instances;
        [DataMember] public OwnedDeviceIdentity[] OwnedDevices;
        [DataMember] public Guid[] RewiredInstances;
        [DataMember] public string IdentityQualification;
        [DataMember] public int PublishedFrames;
        [DataMember] public string[] LastAxisStatuses;
        [DataMember] public float[] LastAxisValues;
        [DataMember] public JoystickObservation[] Before, Disabled, Restored;
        [DataMember] public string RawAxisQualification;
        internal ProbeReceipt() { }
        internal ProbeReceipt(ProbeRequest request)
        {
            Mode = request.Mode; Nonce = request.Nonce; ModSha256 = request.ModSha256; NativeSha256 = request.NativeSha256;
            ShimSha256 = request.ShimSha256; SettingsSha256 = request.SettingsSha256; ProfileSha256 = request.ProfileSha256;
        }
    }
}
