using System;
using System.IO;
using System.Runtime.InteropServices;
using ArtOfSimRally.Mod.Shim;

// Metadata exports ONLY: never opens a shim/DirectInput handle or worker.
internal static class AbiProbe
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Number();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Name(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Status([Out] byte[] text, int size);
    private static T Export<T>(IntPtr module, string name) where T : class
    {
        var pointer = GetProcAddress(module, name);
        if (pointer == IntPtr.Zero) throw new Exception("missing export " + name);
        return Marshal.GetDelegateForFunctionPointer(pointer, typeof(T)) as T;
    }
    internal static int Check(string path)
    {
        if (IntPtr.Size != 8) throw new Exception("shim ABI requires x64");
        IntPtr module = LoadLibraryExW(Path.GetFullPath(path), IntPtr.Zero, 0x100 | 0x1000);
        if (module == IntPtr.Zero) throw new Exception("shim metadata load failed " + Marshal.GetLastWin32Error());
        try
        {
            if (Export<Number>(module, "dbce_shim_abi_version")() != ShimFrame.Abi ||
                Export<Number>(module, "dbce_shim_frame_size")() != ShimFrame.Size ||
                Export<Number>(module, "dbce_shim_open_args_size")() != ShimFrame.OpenArgsSize) throw new Exception("shim ABI mismatch");
            var actionName = Export<Name>(module, "dbce_shim_action_name");
            for (int i = 0; i < ShimFrame.Count; i++)
                if (Marshal.PtrToStringAnsi(actionName(i)) != ShimFrame.Names[i]) throw new Exception("shim action mismatch at " + i);
            if (actionName(ShimFrame.Count) != IntPtr.Zero) throw new Exception("shim action count mismatch");
            return 35;
        }
        finally { FreeLibrary(module); }
    }
    internal static int NoForce(string path, bool loadMuted = false)
    {
        // No force/device/initialization API called. The latch and status alone
        // prove the production exact-resident binding works on CLR and Mono.
        if (loadMuted) Environment.SetEnvironmentVariable("DBCE_FFB_MUTE", "1");
        IntPtr module = LoadLibraryExW(Path.GetFullPath(path), IntPtr.Zero, 0x100 | 0x1000);
        if (module == IntPtr.Zero) throw new Exception("native latch fixture load failed");
        try
        {
            if (loadMuted)
            {
                var status = Export<Status>(module, "GetTestInjectionStatus");
                if (status(new byte[512], 512) != 1) throw new Exception("load-time mute absent before explicit Enable");
                Environment.SetEnvironmentVariable("DBCE_FFB_MUTE", null);
                if (status(new byte[512], 512) != 1) throw new Exception("load-time mute cleared with environment");
            }
            ProbeNoForce.Arm(path); ProbeNoForce.Arm(path); return loadMuted ? 4 : 2;
        }
        finally { FreeLibrary(module); }
    }
}
