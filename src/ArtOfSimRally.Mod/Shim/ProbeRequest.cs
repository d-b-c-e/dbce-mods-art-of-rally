using System;
using System.IO;
using System.Runtime.InteropServices;
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
        // Trial only, diagnostic: may end the title splash once to reach the main menu (romi 29497).
        [DataMember] public bool AdvanceTitle;
        // Trial (Slice B) runs only on the reviewed latched bridge build, art-shim-bridge f38b855 (romi 28531).
        internal const string LatchedShimSha256 = "0D10F913C998C97D7DDF3A8F5F470D010092C3681152059DC88D7E0A7BB0386B";
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
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, int infoClass, byte[] info, int size);
        // Whole-file replacement for Core's session and batch files. Core opens them read-only with read
        // and delete sharing and fails the run on a sharing violation. ReplaceFile opens the target and
        // can collide with that open; a POSIX-semantics rename of a sibling temp over the target does not
        // open it, so a concurrent reader sees the old or the new file and a held reader keeps the old one.
        internal static void Replace(string path, string text)
        {
            string full = Path.GetFullPath(path), temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, text, new System.Text.UTF8Encoding(false));
            try
            {
                const uint Delete = 0x00010000, Synchronize = 0x00100000, ShareAll = 7, OpenExisting = 3;
                using (var handle = CreateFileW(temp, Delete | Synchronize, ShareAll, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero))
                {
                    if (handle.IsInvalid) throw new IOException("replacement temp open failed: " + Marshal.GetLastWin32Error());
                    // FILE_RENAME_INFO (x64): flags at 0, root handle at 8, name length at 16, name at 20.
                    var name = System.Text.Encoding.Unicode.GetBytes(full);
                    var info = new byte[20 + name.Length + 2];
                    BitConverter.GetBytes(3).CopyTo(info, 0); // FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS
                    BitConverter.GetBytes(name.Length).CopyTo(info, 16);
                    name.CopyTo(info, 20);
                    if (!SetFileInformationByHandle(handle, 22 /* FileRenameInfoEx */, info, info.Length))
                        throw new IOException("atomic replacement failed: " + Marshal.GetLastWin32Error());
                }
            }
            catch { try { File.Delete(temp); } catch { } throw; }
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
            if (Schema != 1 || (Mode != "Observe" && Mode != "Apply" && Mode != "Trial") || !Guid.TryParseExact(Nonce, "N", out nonce) || nonce == Guid.Empty)
                throw new InvalidDataException("invalid probe request");
            if (Mode == "Trial" && !String.Equals(ShimSha256, LatchedShimSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Trial needs the reviewed latched bridge build");
            if (AdvanceTitle && Mode != "Trial") throw new InvalidDataException("only a Trial may advance the title");
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
            if (Mode == "Observe") return;
            if (prior == null || prior.Mode != "Observe" || !prior.IdentityQualified || !prior.IdentityRestored || !prior.FlagsDisabled || !prior.FlagsRestored ||
                !prior.RawAxesNeutralAfterDisable || prior.ModSha256 != ModSha256 || prior.NativeSha256 != NativeSha256 ||
                prior.ShimSha256 != ShimSha256 || prior.SettingsSha256 != SettingsSha256 || prior.ProfileSha256 != ProfileSha256 ||
                prior.Instances == null || !new System.Collections.Generic.HashSet<Guid>(prior.Instances).SetEquals(instances) ||
                prior.RewiredInstances == null || rewired == null || !new System.Collections.Generic.HashSet<Guid>(prior.RewiredInstances).SetEquals(rewired) ||
                !ProbeIdentityMapper.SameBindings(prior.OwnedDevices, owned))
                throw new InvalidDataException(Mode + " probe needs matching observed identities, disable/restore and neutral raw-axis receipt");
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
        // Trial: the fence check, Core's last run state and the selection before, after navDown and after navUp.
        [DataMember] public string TrialFences, TrialStatus;
        [DataMember] public string[] TrialSelections;
        [DataMember] public int TrialRun;
        [DataMember] public bool TitleAdvanced;
        internal ProbeReceipt() { }
        internal ProbeReceipt(ProbeRequest request)
        {
            Mode = request.Mode; Nonce = request.Nonce; ModSha256 = request.ModSha256; NativeSha256 = request.NativeSha256;
            ShimSha256 = request.ShimSha256; SettingsSha256 = request.SettingsSha256; ProfileSha256 = request.ProfileSha256;
        }
    }
}
