using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Dbce.Wheel.Ffb;
using Dbce.Wheel.Input;

namespace ArtOfSimRally.Mod
{
    internal static class WheelPovInput
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr GetModuleHandleW(string path);
        [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetModuleFileNameW(IntPtr module, StringBuilder path, int capacity);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        private static PovSnapshotReader _reader;
        internal static void Bind(string requestedPath)
        {
            _reader = null;
            try
            {
                var path = Path.GetFullPath(requestedPath);
                var module = GetModuleHandleW(path);
                var actual = new StringBuilder(32768);
                int length = module == IntPtr.Zero ? 0 : GetModuleFileNameW(module, actual, actual.Capacity);
                if (length <= 0 || length >= actual.Capacity || !string.Equals(path, Path.GetFullPath(actual.ToString()), StringComparison.OrdinalIgnoreCase)) return;
                _reader = PovSnapshotReader.FromExport(GetProcAddress(module, "ReadDeviceStateWithPov"));
            }
            catch (Exception e) { ModLog.Warning("POV input unavailable: " + e.Message); }
        }
        internal static bool Read(int slot, int[] axes, byte[] buttons, int[] hats)
        {
            if (_reader?.Available == true) return _reader.Read(slot, axes, buttons, hats);
            // Older optional native: hats unavailable, ordinary controls retained.
            // A FAILED coherent read above never falls back to a second snapshot.
            for (int i = 0; i < hats.Length; i++) hats[i] = -1;
            return WheelFfbNative.Read(slot, axes, buttons);
        }
    }
}
