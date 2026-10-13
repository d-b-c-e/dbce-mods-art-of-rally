using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace ArtOfSimRally.Mod.Shim
{
    internal sealed class SharedSteeringTarget
    {
        internal readonly string Name, Instance, Reason;
        internal SharedSteeringTarget(string name, string instance, string reason)
        { Name = name; Instance = instance; Reason = reason; }
    }

    // Optional pure extension: Core's parser and binding admission, without opening a host, workers,
    // device readers or force output. ABI 2 alone does not promise this export. No local binding grammar.
    internal static class ShimProfileNative
    {
        internal const string MatchingPackage = "This shared input library cannot resolve the steering FFB target. Install the matching Art shared-input package and restart.";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Inspect([In] byte[] profile, int length, int action, [Out] byte[] text, int capacity, out int needed);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Number();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Name(int index);
        [DataContract] private sealed class Report
        {
            [DataMember(Name = "schema", IsRequired = true)] public int Schema = 0;
            [DataMember(Name = "action", IsRequired = true)] public string Action = null;
            [DataMember(Name = "status", IsRequired = true)] public string Status = null;
            [DataMember(Name = "reason", IsRequired = true)] public string Reason = null;
            [DataMember(Name = "sha256", IsRequired = true)] public string Hash = null;
            [DataMember(Name = "kind", IsRequired = true)] public string Kind = null;
            [DataMember(Name = "instance", IsRequired = true)] public string Instance = null;
            [DataMember(Name = "product", IsRequired = true)] public string Product = null;
            [DataMember(Name = "name", IsRequired = true)] public string Name = null;
        }
        internal static SharedSteeringTarget ReadSteering(string dllPath, byte[] exactProfile)
        {
            if (IntPtr.Size != 8 || exactProfile == null || exactProfile.Length == 0 || exactProfile.Length > 65536)
                throw new InvalidDataException("Shared profile must contain 1–65536 bytes for Core inspection.");
            var module = ShimBridgeNative.LoadLibraryExW(Path.GetFullPath(dllPath), IntPtr.Zero, 0x100 | 0x1000);
            if (module == IntPtr.Zero) throw new InvalidOperationException("Shared profile inspection could not load the qualified library.");
            try
            {
                if (ShimBridgeNative.Export<Number>(module, "dbce_shim_abi_version")() != 2 ||
                    Marshal.PtrToStringAnsi(ShimBridgeNative.Export<Name>(module, "dbce_shim_action_name")(0)) != "steer")
                    throw new InvalidOperationException(MatchingPackage);
                var inspect = ShimBridgeNative.Export<Inspect>(module, "dbce_shim_profile_action_info");
                int needed;
                if (inspect(exactProfile, exactProfile.Length, 0, null, 0, out needed) != 0 || needed < 2 || needed > 512 * 1024)
                    throw new InvalidOperationException("Core profile inspection failed.");
                var bytes = new byte[needed]; int written;
                if (inspect(exactProfile, exactProfile.Length, 0, bytes, bytes.Length, out written) != 0 ||
                    written != bytes.Length || bytes[written - 1] != 0)
                    throw new InvalidOperationException("Core profile inspection changed or was truncated.");
                return Decode(bytes, exactProfile);
            }
            catch (EntryPointNotFoundException) { throw new InvalidOperationException(MatchingPackage); }
            finally { ShimBridgeNative.FreeLibrary(module); }
        }
        internal static SharedSteeringTarget Decode(byte[] bytes, byte[] exactProfile)
        {
            Report report;
            if (bytes == null || bytes.Length < 2 || bytes[bytes.Length - 1] != 0) throw new InvalidDataException("Core profile report is incomplete.");
            using (var stream = new MemoryStream(bytes, 0, bytes.Length - 1, false))
                report = (Report)new DataContractJsonSerializer(typeof(Report)).ReadObject(stream);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(exactProfile)).Replace("-", "");
            if (report == null || report.Schema != 1 || report.Action != "steer" || report.Name == null || report.Reason == null ||
                !String.Equals(report.Hash, hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Core profile report does not describe the startup profile bytes.");
            if (report.Status == "invalid" || report.Status == "unbound")
            {
                if (report.Kind != "" || report.Instance != "" || report.Product != "") throw new InvalidDataException("Refused profile report contains a target.");
                return new SharedSteeringTarget(null, null, "Shared steering FFB target unavailable: " + report.Reason + ". Correct the shared profile and restart.");
            }
            Guid instance, product;
            if (report.Status != "bound" || report.Kind != "axis" || !Guid.TryParse(report.Instance, out instance) || instance == Guid.Empty ||
                !Guid.TryParse(report.Product, out product) || product == Guid.Empty)
                throw new InvalidDataException("Core returned no valid shared steering axis identity.");
            return new SharedSteeringTarget(report.Name, instance.ToString("D"), null);
        }
    }
}
