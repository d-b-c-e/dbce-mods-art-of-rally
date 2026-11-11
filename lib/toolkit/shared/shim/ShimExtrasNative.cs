using System.Runtime.InteropServices;
using System.Text;

namespace Dbce.Wheel.Shim;

/// <summary>Optional extension 2 of the already hash-pinned module. Never loads a second DLL or opens another reader.</summary>
public sealed class ShimExtrasNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenCall(IntPtr folder, IntPtr nonce, IntPtr profile, int profileLength, IntPtr unsupported,
        IntPtr hwnd, IntPtr declaration, int declarationLength, out IntPtr handle, [Out] byte[] why, int whyLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TickCall(IntPtr handle, uint contexts, [In, Out] byte[] common, [In, Out] byte[] extras);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CachedCall(IntPtr handle, ulong seq, [In, Out] byte[] extras);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InfoCall(IntPtr handle, [Out] byte[] bytes, int length, out int needed);
    private readonly OpenCall _open;
    private readonly TickCall _tick;
    private readonly CachedCall _cached;
    private readonly InfoCall _info;
    private ShimExtrasNative(OpenCall open, TickCall tick, CachedCall cached, InfoCall info)
    { _open = open; _tick = tick; _cached = cached; _info = info; }

    public static ShimExtrasNative? TryBind(Func<string, IntPtr> resolve, out string? unavailable)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        try
        {
            T Get<T>(string name) where T : Delegate
            {
                IntPtr address = resolve(name);
                if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
            ShimExtrasAbi.Validate(Get<IntCall>("dbce_shim_extras_abi_version")(), Get<IntCall>("dbce_shim_extras_frame_size")());
            var result = new ShimExtrasNative(Get<OpenCall>("dbce_shim_open_extras"), Get<TickCall>("dbce_shim_tick_extras"),
                Get<CachedCall>("dbce_shim_extras"), Get<InfoCall>("dbce_shim_extras_info"));
            unavailable = null; return result;
        }
        catch (Exception e) when (e is EntryPointNotFoundException or InvalidDataException or ArgumentException)
        { unavailable = "Declared extras unavailable: " + e.Message; return null; }
    }

    /// <summary>Pass the exact complete existing dbce-controls.ini, including [Controls] and [Extras].</summary>
    public int Open(string folder, string? nonce, byte[] profile, string? unsupported, IntPtr hwnd,
        byte[] declaration, out IntPtr handle, out string why)
    {
        ArgumentNullException.ThrowIfNull(folder); ArgumentNullException.ThrowIfNull(profile);
        ShimExtrasAbi.ValidateDeclaration(declaration);
        if (profile.Length == 0 || profile.Length > 64 * 1024) throw new InvalidDataException("Extras profile must be 1..65536 bytes.");
        // Keep every allocation inside the finally scope, including partial allocation failures.
        IntPtr f = IntPtr.Zero, n = IntPtr.Zero, p = IntPtr.Zero, u = IntPtr.Zero, d = IntPtr.Zero;
        try
        {
            f = Marshal.StringToHGlobalUni(folder); n = nonce == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(nonce);
            u = unsupported == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(unsupported);
            p = Marshal.AllocHGlobal(profile.Length); Marshal.Copy(profile, 0, p, profile.Length);
            d = Marshal.AllocHGlobal(declaration.Length); Marshal.Copy(declaration, 0, d, declaration.Length);
            var buffer = new byte[512];
            int result = _open(f, n, p, profile.Length, u, hwnd, d, declaration.Length, out handle, buffer, buffer.Length);
            why = Text(buffer); return result;
        }
        finally
        {
            Marshal.FreeHGlobal(f); Marshal.FreeCoTaskMem(n); Marshal.FreeHGlobal(p);
            Marshal.FreeCoTaskMem(u); Marshal.FreeHGlobal(d);
        }
    }

    public int Tick(IntPtr handle, uint contexts, byte[] common, byte[] extras)
    {
        Prepare(common, ShimAbi2.FrameSize); Prepare(extras, ShimExtrasAbi.FrameSize);
        return _tick(handle, contexts, common, extras);
    }
    public int Cached(IntPtr handle, ulong sequence, byte[] extras)
    { Prepare(extras, ShimExtrasAbi.FrameSize); return _cached(handle, sequence, extras); }
    public string Info(IntPtr handle)
    {
        var buffer = new byte[8192];
        int result = _info(handle, buffer, buffer.Length, out int needed);
        if (result == 0 && needed > buffer.Length && needed <= 128 * 1024)
        { buffer = new byte[needed]; result = _info(handle, buffer, buffer.Length, out needed); }
        if (result != 0 || needed <= 0 || needed > buffer.Length || Array.IndexOf(buffer, (byte)0) < 0)
            throw new InvalidDataException("Extras report unavailable, truncated or changed size.");
        return Text(buffer);
    }
    private static void Prepare(byte[] buffer, int size)
    {
        if (buffer.Length != size) throw new ArgumentException("Wrong shim frame buffer length.");
        Array.Clear(buffer); BitConverter.GetBytes(size).CopyTo(buffer, 0);
    }
    private static string Text(byte[] bytes)
    { int end = Array.IndexOf(bytes, (byte)0); return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end); }
}
