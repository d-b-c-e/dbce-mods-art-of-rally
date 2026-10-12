using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ArtOfSimRally.Mod.Shim
{
    internal static class ProbeNoForce
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder path, int size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Enable(int flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Status([Out] byte[] text, int size);
        // Arms the existing irreversible native no-force latch. Never submits an
        // injected sample, creates a device, or loads another native module.
        internal static void Arm(string loadedPath)
        {
            var expected = Path.GetFullPath(loadedPath); var module = GetModuleHandleW(expected);
            var actual = new StringBuilder(32768);
            if (module == IntPtr.Zero || GetModuleFileNameW(module, actual, actual.Capacity) == 0 || !ProbeRequest.SamePath(actual.ToString(), expected))
                throw new InvalidOperationException("exact resident native module unavailable");
            int nativeCopies = 0;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
                foreach (System.Diagnostics.ProcessModule loaded in process.Modules)
                    if (String.Equals(loaded.ModuleName, "UnityForceFeedback.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(loaded.ModuleName, "WheelFfb.dll", StringComparison.OrdinalIgnoreCase)) nativeCopies++;
            if (nativeCopies != 1) throw new InvalidOperationException("probe needs exactly one resident force module; another copy cannot share its no-force latch");
            if (ShimBridgeNative.Export<Enable>(module, "EnableTestInjection")(1) != 1 ||
                ShimBridgeNative.Export<Status>(module, "GetTestInjectionStatus")(new byte[512], 512) != 1)
                throw new InvalidOperationException("cold no-force latch refused; restart required");
        }
    }
}
