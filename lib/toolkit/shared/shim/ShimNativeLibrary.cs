using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Shim;

// Field offsets mirror native/shim/shim_dll/dbce_shim.h. All pointers are
// caller-owned for the duration of Open2; native copies anything it retains.
[StructLayout(LayoutKind.Explicit, Size = ShimAbi2.OpenArgsSize)]
public struct ShimOpenArgs
{
    [FieldOffset(0)] public uint StructSize;
    [FieldOffset(4)] public uint Flags;
    [FieldOffset(8)] public int ProfileLength;
    [FieldOffset(12)] public byte FencesVerified;
    [FieldOffset(13)] public byte ForceAbsent;
    [FieldOffset(16)] public IntPtr Folder;
    [FieldOffset(24)] public IntPtr Nonce;
    [FieldOffset(32)] public IntPtr Profile;
    [FieldOffset(40)] public IntPtr Unsupported;
    [FieldOffset(48)] public IntPtr Hwnd;
    [FieldOffset(56)] public IntPtr SessionFile;
    [FieldOffset(64)] public IntPtr BatchFile;
    [FieldOffset(72)] public IntPtr CaptureFolder;
    [FieldOffset(80)] public IntPtr GameId;
    [FieldOffset(88)] public IntPtr GameChannels;
    [FieldOffset(96)] public IntPtr Context;
    [FieldOffset(104)] public IntPtr AdapterVersion;
    [FieldOffset(112)] public double Hz;
    [FieldOffset(120)] public double Seconds;
}

/// <summary>Hash-pinned, ABI-checked native shim exports. A consumer owns its
/// game-specific pin, receipt proof, device isolation, and handle-thread rules.</summary>
public sealed class ShimNativeLibrary
{
    public const uint Latched = 1, Capture = 2;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr NameCall(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenCall(IntPtr folder, IntPtr nonce, IntPtr profile, int profileLength,
        IntPtr unsupported, IntPtr hwnd, out IntPtr handle, [Out] byte[] why, int whyLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Open2Call(ref ShimOpenArgs args, out IntPtr handle, [Out] byte[] why, int whyLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TickCall(IntPtr handle, [In, Out] byte[] frame);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RecordCall(IntPtr handle, ulong sequence, [In] double[] values, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TextCall(IntPtr handle, [Out] byte[] text, int length, out int needed);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SnapshotSizesCall(out int header, out int device, out int sample);
    // Extension 3 (dbce_shim_snapshot.h): pinned byte buffers, so the copies the DLL writes are seen here.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SnapshotCall(IntPtr handle, ulong frameSeq, [In, Out] byte[] header, [In, Out] byte[] devices, int deviceCapacity,
        [In, Out] byte[] samples, int sampleCapacity);

    private readonly OpenCall _open;
    private readonly Open2Call _open2;
    private readonly TickCall _tick;
    private readonly RecordCall _record;
    private readonly TextCall _startReport, _devices, _profile, _stop;
    private readonly SnapshotCall? _snapshot;

    /// <summary>Why this DLL offers no accepted-input snapshot (extension 3 is optional), or null when it does. Without it
    /// a consumer's in-game binding capture is unavailable; nothing else changes.</summary>
    public string? SnapshotUnavailable { get; }

    /// <summary>Optional ordinary-only declared-extra transport from this same pinned module.</summary>
    public ShimExtrasNative? Extras { get; private set; }
    public string? ExtrasUnavailable { get; private set; }

    private ShimNativeLibrary(OpenCall open, Open2Call open2, TickCall tick, RecordCall record,
        TextCall startReport, TextCall devices, TextCall profile, TextCall stop, SnapshotCall? snapshot, string? snapshotUnavailable)
    {
        _open = open; _open2 = open2; _tick = tick; _record = record;
        _startReport = startReport; _devices = devices; _profile = profile; _stop = stop;
        _snapshot = snapshot; SnapshotUnavailable = snapshotUnavailable;
    }

    public static ShimNativeLibrary Load(string path, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(expectedSha256);
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("The managed shim ABI 2 layout requires Windows x64.");
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("The shim path must be absolute.", nameof(path));
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("A 64-digit SHA-256 pin is required.", nameof(expectedSha256));
        path = Path.GetFullPath(path);
        using (var stream = File.OpenRead(path))
        {
            if (stream.Length > 64 * 1024 * 1024) throw new InvalidDataException("Shim DLL exceeds 64 MiB.");
            using var sha256 = SHA256.Create();
            string actual = Convert.ToHexString(sha256.ComputeHash(stream));
            if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Shim DLL differs from the consumer's reviewed pin.");
        }
        IntPtr module = NativeLibrary.Load(path);
        try
        {
            T Get<T>(string name) where T : Delegate =>
                Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
            var abi = Get<IntCall>("dbce_shim_abi_version");
            var size = Get<IntCall>("dbce_shim_frame_size");
            var argsSize = Get<IntCall>("dbce_shim_open_args_size");
            var name = Get<NameCall>("dbce_shim_action_name");
            if (Marshal.SizeOf<ShimOpenArgs>() != ShimAbi2.OpenArgsSize)
                throw new InvalidDataException("Managed shim open arguments have the wrong layout.");
            // The returned name pointer is static native storage. Never marshal
            // the export itself as a string, which could free that pointer.
            ShimAbi2.Validate(abi(), size(), argsSize(), i => Marshal.PtrToStringAnsi(name(i)));
            // Optional extension 3: an older DLL simply has no snapshot.
            SnapshotCall? snapshot = null; string? snapshotUnavailable;
            if (!NativeLibrary.TryGetExport(module, "dbce_shim_snapshot_abi_version", out var snapshotAbi) ||
                !NativeLibrary.TryGetExport(module, "dbce_shim_snapshot_sizes", out var snapshotSizes) ||
                !NativeLibrary.TryGetExport(module, "dbce_shim_snapshot", out var snapshotExport))
                snapshotUnavailable = "this dbce_shim.dll has no accepted-input snapshot";
            else if (Marshal.GetDelegateForFunctionPointer<SnapshotSizesCall>(snapshotSizes)(out int header, out int device, out int sample) != 0)
                snapshotUnavailable = "dbce_shim.dll did not report its snapshot sizes";
            else
            {
                try
                {
                    ShimSnapshotAbi.Validate(Marshal.GetDelegateForFunctionPointer<IntCall>(snapshotAbi)(), header, device, sample);
                    snapshot = Marshal.GetDelegateForFunctionPointer<SnapshotCall>(snapshotExport); snapshotUnavailable = null;
                }
                catch (InvalidDataException e) { snapshotUnavailable = e.Message; }
            }
            var library = new ShimNativeLibrary(Get<OpenCall>("dbce_shim_open"), Get<Open2Call>("dbce_shim_open2"),
                Get<TickCall>("dbce_shim_tick"), Get<RecordCall>("dbce_shim_record"),
                Get<TextCall>("dbce_shim_start_report"), Get<TextCall>("dbce_shim_device_reports"),
                Get<TextCall>("dbce_shim_profile_info"), Get<TextCall>("dbce_shim_stop"), snapshot, snapshotUnavailable);
            library.Extras = ShimExtrasNative.TryBind(export => NativeLibrary.TryGetExport(module, export, out var pointer) ? pointer : IntPtr.Zero,
                out string? extrasUnavailable);
            library.ExtrasUnavailable = extrasUnavailable;
            return library;
        }
        catch { NativeLibrary.Free(module); throw; }
        // On success the module remains loaded for the process. A stop that
        // cannot resolve deliberately leaves native workers alive until exit.
    }

    public int Open(string folder, string? nonce, byte[] profile, string? unsupported,
        IntPtr hwnd, out IntPtr handle, out string why)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Length == 0 || profile.Length > 64 * 1024)
            throw new InvalidDataException("Controls section is empty or exceeds native Core's 64 KiB limit.");
        IntPtr folderPtr = Marshal.StringToHGlobalUni(folder);
        IntPtr noncePtr = nonce == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(nonce);
        IntPtr unsupportedPtr = unsupported == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(unsupported);
        IntPtr profilePtr = Marshal.AllocHGlobal(profile.Length);
        try
        {
            Marshal.Copy(profile, 0, profilePtr, profile.Length);
            var buffer = new byte[512];
            int result = _open(folderPtr, noncePtr, profilePtr, profile.Length, unsupportedPtr, hwnd,
                out handle, buffer, buffer.Length);
            why = Text(buffer);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(folderPtr);
            Marshal.FreeCoTaskMem(noncePtr);
            Marshal.FreeCoTaskMem(unsupportedPtr);
            Marshal.FreeHGlobal(profilePtr);
        }
    }

    // Developer capture/latched entry point. The consumer, not this library,
    // must establish every fence and keep all pointers alive through this call.
    public int Open2(ref ShimOpenArgs args, out IntPtr handle, out string why)
    {
        var buffer = new byte[512];
        int result = _open2(ref args, out handle, buffer, buffer.Length);
        why = Text(buffer);
        return result;
    }

    public int Tick(IntPtr handle, byte[] frame)
    {
        if (frame.Length != ShimAbi2.FrameSize) throw new ArgumentException("ABI-2 frame buffer must be 296 bytes.", nameof(frame));
        // A failed native tick must not leave a prior live frame in the buffer.
        Array.Clear(frame);
        BitConverter.GetBytes(ShimAbi2.FrameSize).CopyTo(frame, 0);
        return _tick(handle, frame);
    }

    public int Record(IntPtr handle, ulong frameSequence, double[] values) =>
        _record(handle, frameSequence, values, values.Length);

    /// <summary>dbce_shim_snapshot of <paramref name="handle"/>'s frame <paramref name="frameSeq"/> (extension 3): call it on
    /// the handle's thread right after the tick that returned that frame, through <see cref="ShimSnapshot.Take"/>.
    /// REFUSED when the DLL has no snapshot (<see cref="SnapshotUnavailable"/>).</summary>
    public int Snapshot(IntPtr handle, ulong frameSeq, byte[] header, byte[] devices, int deviceCapacity, byte[] samples, int sampleCapacity) =>
        _snapshot == null ? ShimSnapshotAbi.Refused : _snapshot(handle, frameSeq, header, devices, deviceCapacity, samples, sampleCapacity);

    /// <summary>The <see cref="ShimSnapshot.NativeCall"/> for one owned handle.</summary>
    public ShimSnapshot.NativeCall SnapshotCallFor(IntPtr handle) =>
        (frameSeq, header, devices, deviceCapacity, samples, sampleCapacity) => Snapshot(handle, frameSeq, header, devices, deviceCapacity, samples, sampleCapacity);

    public string StartReport(IntPtr handle) => Read(_startReport, handle);
    public string DeviceReports(IntPtr handle) => Read(_devices, handle);
    public string ProfileInfo(IntPtr handle) => Read(_profile, handle);

    public int Stop(IntPtr handle, out string report)
    {
        var buffer = new byte[128 * 1024];
        int result = _stop(handle, buffer, buffer.Length, out int needed);
        report = needed > buffer.Length ? "(stop report truncated)" : Text(buffer);
        return result;
    }

    private static string Read(TextCall call, IntPtr handle)
    {
        var buffer = new byte[8192];
        int result = call(handle, buffer, buffer.Length, out int needed);
        if (result == 0 && needed > buffer.Length && needed <= 128 * 1024)
        {
            buffer = new byte[needed];
            result = call(handle, buffer, buffer.Length, out needed);
        }
        return result == 0 && needed <= buffer.Length ? Text(buffer) : "(report unavailable or changed size)";
    }

    private static string Text(byte[] buffer)
    {
        int end = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }
}
