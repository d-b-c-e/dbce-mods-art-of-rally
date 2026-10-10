// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace Dbce.Wheel.Input
{
    /// <summary>Optional coherent reader for consumers retaining an older managed
    /// wrapper. Does not load a DLL, open a device, acquire it, or own its lifetime.
    /// Bind the export from the same resident module that owns the read slot.
    /// Serialize reads with that owner's lifecycle; never unload it while bound.</summary>
    public sealed class PovSnapshotReader
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ReadCall(int slot, [Out] int[] axes, int axisCount,
            [Out] byte[] buttons, int buttonCount, [Out] int[] pov, int povCount);
        private readonly ReadCall _read;
        public PovSnapshotReader(ReadCall read) { _read = read; }
        public static PovSnapshotReader FromExport(IntPtr export) => new PovSnapshotReader(
            export == IntPtr.Zero ? null : (ReadCall)Marshal.GetDelegateForFunctionPointer(export, typeof(ReadCall)));
        public bool Available => _read != null;
        public bool Read(int slot, int[] axes, byte[] buttons, int[] pov)
        {
            Clear(axes, buttons, pov);
            if (!Available || slot < 0 || axes == null || axes.Length != 8 ||
                pov == null || pov.Length != 4 || (buttons != null && buttons.Length > 128)) return false;
            try
            {
                if (_read(slot, axes, 8, buttons, buttons == null ? 0 : buttons.Length, pov, 4) == 1)
                {
                    for (int i = 0; i < pov.Length; i++)
                        if (pov[i] < 0 || pov[i] >= 36000) pov[i] = -1;
                    return true;
                }
            }
            catch (Exception) { /* A failed snapshot is never logical pedal input. */ }
            Clear(axes, buttons, pov);
            return false;
        }
        private static void Clear(int[] axes, byte[] buttons, int[] pov)
        {
            if (axes != null) Array.Clear(axes, 0, axes.Length);
            if (buttons != null) Array.Clear(buttons, 0, buttons.Length);
            if (pov != null) for (int i = 0; i < pov.Length; i++) pov[i] = -1;
        }
    }
}
