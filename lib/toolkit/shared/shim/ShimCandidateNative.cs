// Mirrors native/shim/shim_dll/dbce_shim_candidate.h (extension 4, candidate ABI 1): one explicitly selected first-device
// capture candidate on an ordinary shim handle (sana 213587). Portable vendorable source like ShimSnapshot.cs: block
// namespace, classes and #nullable only, so netstandard2.1, net48 and Unity Mono hosts can vendor it byte-exact.
// It binds the six exports through the host's own resolver over the module the host already loaded and hash-checked:
// it loads nothing, reads no device, clock or file, and leaves the report JSON to the host's existing reader. The
// candidate snapshot reuses extension 3's structs: decode it with ShimSnapshot.Take(SnapshotCallFor(handle), seq, ...)
// in its own ShimSnapshotFeed, never in the feed of the ordinary snapshot.
#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dbce.Wheel.Shim
{
    public sealed class ShimCandidateNative
    {
        public const int Version = 1;
        private static readonly string[] Exports = { "dbce_shim_candidate_abi_version", "dbce_shim_candidate_list", "dbce_shim_candidate_select",
            "dbce_shim_candidate_release", "dbce_shim_candidate_report", "dbce_shim_candidate_snapshot" };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int HandleCall(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SelectCall(IntPtr handle, [In] byte[] instance, [In] byte[] product, [Out] byte[] why, int whyLength);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TextCall(IntPtr handle, [Out] byte[] text, int length, out int needed);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SnapshotCall(IntPtr handle, ulong frameSeq, [In, Out] byte[] header, [In, Out] byte[] devices, int deviceCapacity,
            [In, Out] byte[] samples, int sampleCapacity);

        private readonly HandleCall _list, _release;
        private readonly SelectCall _select;
        private readonly TextCall _report;
        private readonly SnapshotCall _snapshot;
        private ShimCandidateNative(HandleCall list, SelectCall select, HandleCall release, TextCall report, SnapshotCall snapshot)
        { _list = list; _select = select; _release = release; _report = report; _snapshot = snapshot; }

        /// <summary>Binds the optional exports of the host's already loaded module. <paramref name="resolve"/> maps an export
        /// name to its address, IntPtr.Zero when absent. Null, with the reason, for a DLL without extension 4 or with another
        /// version: the host then offers no first-device route and changes nothing else.</summary>
        public static ShimCandidateNative? TryBind(Func<string, IntPtr> resolve, out string? unavailable)
        {
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            var addresses = new IntPtr[Exports.Length];
            for (int i = 0; i < Exports.Length; i++)
            {
                addresses[i] = resolve(Exports[i]);
                if (addresses[i] == IntPtr.Zero) { unavailable = "this dbce_shim.dll has no first-device candidate (" + Exports[i] + ")"; return null; }
            }
            int version = Bind<IntCall>(addresses[0])();
            if (version != Version) { unavailable = "shim candidate ABI " + version + "; expected " + Version; return null; }
            unavailable = null;
            return new ShimCandidateNative(Bind<HandleCall>(addresses[1]), Bind<SelectCall>(addresses[2]), Bind<HandleCall>(addresses[3]),
                Bind<TextCall>(addresses[4]), Bind<SnapshotCall>(addresses[5]));
        }

        // Every call below is the handle's owner thread, right after its ordinary tick.

        /// <summary>Queues one listing of attached controllers on the shim's provider worker; a later Report shows it.</summary>
        public int RequestList(IntPtr handle) => _list(handle);

        /// <summary>Selects a listed controller as the one candidate, replacing any other, and queues its open. REFUSED for an
        /// instance the profile binds, BAD_ARGUMENT for malformed identity; <paramref name="why"/> says which.</summary>
        public int Select(IntPtr handle, Guid instance, Guid product, out string why)
        {
            var reason = new byte[512];
            int result = _select(handle, Utf8Z(instance.ToString("B")), Utf8Z(product.ToString("B")), reason, reason.Length);
            why = Text(reason, reason.Length);
            return result;
        }

        /// <summary>Releases the candidate; OK when there is none.</summary>
        public int Release(IntPtr handle) => _release(handle);

        /// <summary>The report JSON as of the last tick. <paramref name="json"/> is null unless the call succeeded with one
        /// complete NUL-terminated text; a malformed text returns BAD_ARGUMENT.</summary>
        public int Report(IntPtr handle, out string? json)
        {
            json = null;
            var buffer = new byte[32768];
            int result = _report(handle, buffer, buffer.Length, out int needed);
            if (result == ShimSnapshotAbi.Ok && needed > buffer.Length && needed <= 1 << 20)
            {
                buffer = new byte[needed];
                result = _report(handle, buffer, buffer.Length, out needed);
            }
            if (result != ShimSnapshotAbi.Ok) return result;
            if (needed <= 0 || needed > buffer.Length || Array.IndexOf(buffer, (byte)0) != needed - 1) return ShimSnapshotAbi.BadArgument;
            json = Text(buffer, needed - 1);
            return result;
        }

        /// <summary>The candidate's own snapshot of frame <paramref name="frameSeq"/>, in extension 3's buffers. A matching
        /// call renews the candidate's lifetime; take it every frame while the editor shows it.</summary>
        public int Snapshot(IntPtr handle, ulong frameSeq, byte[] header, byte[] devices, int deviceCapacity, byte[] samples, int sampleCapacity) =>
            _snapshot(handle, frameSeq, header, devices, deviceCapacity, samples, sampleCapacity);

        /// <summary>The <see cref="ShimSnapshot.NativeCall"/> of the candidate snapshot for one owned handle.</summary>
        public ShimSnapshot.NativeCall SnapshotCallFor(IntPtr handle) =>
            (frameSeq, header, devices, deviceCapacity, samples, sampleCapacity) => Snapshot(handle, frameSeq, header, devices, deviceCapacity, samples, sampleCapacity);

        private static T Bind<T>(IntPtr address) where T : class => (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        private static byte[] Utf8Z(string text) => Encoding.UTF8.GetBytes(text + "\0");
        private static string Text(byte[] bytes, int limit)
        { int end = Array.IndexOf(bytes, (byte)0, 0, limit); return Encoding.UTF8.GetString(bytes, 0, end < 0 ? limit : end); }
    }
}
