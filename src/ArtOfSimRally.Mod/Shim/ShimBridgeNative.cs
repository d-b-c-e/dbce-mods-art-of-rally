using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ArtOfSimRally.Mod.Shim
{
    // Exact-path optional exports, compatible with Unity 2019 Mono. Successful
    // modules remain pinned; stop releases only the copied provider's workers.
    internal sealed class ShimBridgeNative : IShimBridge, IShimPathBridge
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll")] internal static extern bool FreeLibrary(IntPtr module);
        internal static T Export<T>(IntPtr module, string name) where T : class
        {
            var address = GetProcAddress(module, name);
            if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Number();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Name(int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int OpenCall(IntPtr args, out IntPtr handle, [Out] byte[] why, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DevicesCall(IntPtr handle, [In] byte[] devices, int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DevicesWithPathsCall(IntPtr handle, [In] byte[] devices, int count, [In] IntPtr[] paths);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TickCall(IntPtr handle, [In] byte[] samples, int count, [In, Out] byte[] frame);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StopCall(IntPtr handle, [Out] byte[] why, int length, IntPtr needed);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReportsCall(IntPtr handle, [Out] byte[] text, int length, out int needed);
        private IntPtr handle;
        private readonly DevicesCall devices;
        private readonly DevicesWithPathsCall devicesWithPaths;
        private readonly TickCall tick;
        private readonly StopCall stop;
        private readonly ReportsCall reports;
        private readonly int owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal ShimBridgeNative(string dllPath, string runFolder, byte[] exactProfile)
        {
            if (IntPtr.Size != 8 || exactProfile == null) throw new InvalidOperationException("bridge requires x64 and exact profile bytes");
            IntPtr module = LoadLibraryExW(Path.GetFullPath(dllPath), IntPtr.Zero, 0x100 | 0x1000);
            if (module == IntPtr.Zero) throw new InvalidOperationException("bridge DLL load failed: " + Marshal.GetLastWin32Error());
            var memory = new System.Collections.Generic.List<IntPtr>();
            try
            {
                if (Export<Number>(module, "dbce_shim_abi_version")() != 2 || Export<Number>(module, "dbce_shim_frame_size")() != ShimFrame.Size ||
                    Export<Number>(module, "dbce_shim_open_args_size")() != 128 || Export<Number>(module, "dbce_shim_bridge_abi_version")() != 1 ||
                    Export<Number>(module, "dbce_shim_bridge_device_size")() != ShimBridge.DeviceSize || Export<Number>(module, "dbce_shim_bridge_sample_size")() != ShimBridge.SampleSize)
                    throw new InvalidOperationException("bridge ABI mismatch");
                var name = Export<Name>(module, "dbce_shim_action_name");
                for (int i = 0; i < ShimFrame.Count; i++) if (Marshal.PtrToStringAnsi(name(i)) != ShimFrame.Names[i]) throw new InvalidOperationException("bridge action order mismatch");
                if (name(ShimFrame.Count) != IntPtr.Zero) throw new InvalidOperationException("bridge action count mismatch");
                devices = Export<DevicesCall>(module, "dbce_shim_bridge_devices"); tick = Export<TickCall>(module, "dbce_shim_bridge_tick"); stop = Export<StopCall>(module, "dbce_shim_stop");
                try { devicesWithPaths = Export<DevicesWithPathsCall>(module, "dbce_shim_bridge_devices_with_paths"); } catch (EntryPointNotFoundException) { }
                reports = Export<ReportsCall>(module, "dbce_shim_device_reports");
                var args = new byte[128]; BitConverter.GetBytes(128).CopyTo(args, 0); BitConverter.GetBytes(exactProfile.Length).CopyTo(args, 8);
                var folder = Marshal.StringToHGlobalUni(Path.GetFullPath(runFolder)); memory.Add(folder); BitConverter.GetBytes(folder.ToInt64()).CopyTo(args, 16);
                Put(args, 24, Encoding.UTF8.GetBytes("artbridge1\0"), memory);
                Put(args, 32, exactProfile, memory);
                Put(args, 40, Encoding.UTF8.GetBytes(ShimAxisConsumer.UnsupportedActions + "\0"), memory);
                Put(args, 104, Encoding.UTF8.GetBytes("art-axis-bridge-source1\0"), memory);
                var block = Marshal.AllocHGlobal(args.Length); memory.Add(block); Marshal.Copy(args, 0, block, args.Length);
                var why = new byte[1024];
                int result = Export<OpenCall>(module, "dbce_shim_bridge_open")(block, out handle, why, why.Length);
                if (result != 0 || handle == IntPtr.Zero) throw new InvalidOperationException("bridge open " + result + ": " + Encoding.UTF8.GetString(why).TrimEnd('\0'));
            }
            catch { FreeLibrary(module); throw; }
            finally { foreach (var value in memory) Marshal.FreeHGlobal(value); }
        }
        private static void Put(byte[] args, int offset, byte[] bytes, System.Collections.Generic.List<IntPtr> memory)
        {
            var value = Marshal.AllocHGlobal(Math.Max(1, bytes.Length)); memory.Add(value); Marshal.Copy(bytes, 0, value, bytes.Length);
            BitConverter.GetBytes(value.ToInt64()).CopyTo(args, offset);
        }
        private void CheckThread() { if (System.Threading.Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("bridge owner thread required"); }
        [DataContract] private sealed class DeviceReport
        {
            [DataMember(Name = "instance", IsRequired = true)] public string Instance = null;
            [DataMember(Name = "product", IsRequired = true)] public string Product = null;
        }
        internal Guid[] BoundInstances()
        {
            var values = BoundProducts(); var result = new Guid[values.Count]; values.Keys.CopyTo(result, 0); return result;
        }
        internal Dictionary<Guid, Guid> BoundProducts()
        {
            CheckThread(); int needed;
            if (handle == IntPtr.Zero || reports(handle, null, 0, out needed) != 0 || needed < 3 || needed > 65536)
                throw new InvalidOperationException("Core device report unavailable");
            var bytes = new byte[needed]; int written;
            if (reports(handle, bytes, bytes.Length, out written) != 0 || written > bytes.Length || written < 3 || bytes[written - 1] != 0)
                throw new InvalidOperationException("Core device report changed or truncated");
            List<DeviceReport> values;
            using (var stream = new MemoryStream(bytes, 0, written - 1, false))
                values = (List<DeviceReport>)new DataContractJsonSerializer(typeof(List<DeviceReport>)).ReadObject(stream);
            var ids = new Dictionary<Guid, Guid>();
            if (values == null || values.Count == 0 || values.Count > 64) throw new InvalidOperationException("Core profile has no qualified device identities");
            foreach (var value in values)
            {
                Guid id, product;
                if (value == null || !Guid.TryParse(value.Instance, out id) || id == Guid.Empty || ids.ContainsKey(id) ||
                    !Guid.TryParse(value.Product, out product) || product == Guid.Empty)
                    throw new InvalidOperationException("Core returned an invalid or duplicate instance identity");
                ids.Add(id, product);
            }
            return ids;
        }
        public bool Devices(byte[] bytes, int count) { CheckThread(); return handle != IntPtr.Zero && bytes != null && bytes.Length == count * ShimBridge.DeviceSize && devices(handle, bytes, count) == 0; }
        public bool DevicesWithPaths(byte[] bytes, int count, string[] paths)
        {
            CheckThread();
            if (handle == IntPtr.Zero || devicesWithPaths == null || count < 0 || count > 64 || bytes == null || bytes.Length != count * ShimBridge.DeviceSize || paths == null || paths.Length != count) return false;
            var pointers = new IntPtr[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (String.IsNullOrEmpty(paths[i]) || paths[i].Length > 259 || paths[i].IndexOf('\0') >= 0) return false;
                    pointers[i] = Marshal.StringToHGlobalUni(paths[i]);
                }
                return devicesWithPaths(handle, bytes, count, pointers) == 0;
            }
            finally { foreach (var p in pointers) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }
        public bool Tick(byte[] bytes, int count, byte[] frame) { CheckThread(); return handle != IntPtr.Zero && bytes != null && bytes.Length == count * ShimBridge.SampleSize && frame != null && frame.Length == ShimFrame.Size && tick(handle, bytes, count, frame) == 0; }
        public void Stop() { CheckThread(); if (handle == IntPtr.Zero) return; var current = handle; handle = IntPtr.Zero; var why = new byte[1024]; if (stop(current, why, why.Length, IntPtr.Zero) != 0) throw new InvalidOperationException("bridge stop unresolved: " + Encoding.UTF8.GetString(why).TrimEnd('\0')); }
    }

    internal sealed class ExistingWheelSnapshots : IPhysicalSnapshotSource, IPhysicalSnapshotPathSource
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SnapshotCall(int slot, [In, Out] byte[] bytes);
        private readonly SnapshotCall describe, read;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PathCall(int slot, ulong generation, [Out] ushort[] path, int capacity);
        private readonly PathCall path;
        private readonly Dictionary<int, byte[]> expectedInfo = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, string> expectedPaths = new Dictionary<int, string>();
        internal void Pin(int slot, byte[] metadata, string interfacePath)
        {
            if (expectedInfo.Count == 64 || metadata == null || metadata.Length != ShimBridge.DeviceSize || String.IsNullOrEmpty(interfacePath)) throw new ArgumentException("invalid probe identity pin");
            expectedInfo.Add(slot, (byte[])metadata.Clone()); expectedPaths.Add(slot, interfacePath);
        }
        internal void VerifyPinned(int[] slots)
        {
            if (slots == null || slots.Length != expectedInfo.Count || !new HashSet<int>(slots).SetEquals(expectedInfo.Keys))
                throw new InvalidOperationException("owned read-slot inventory changed");
            foreach (int slot in slots)
            {
                var bytes = new byte[ShimBridge.DeviceSize]; string currentPath;
                if (Describe(slot, bytes) != 1 || !TryGetPath(slot, BitConverter.ToUInt64(bytes, 8), out currentPath))
                    throw new InvalidOperationException("owned reader metadata/generation/path changed");
            }
        }
        // Caller must pass WheelFfbNative.LoadedFrom after the existing owner has
        // opened its devices. Loading another path is not a supported handoff.
        internal ExistingWheelSnapshots(string alreadyLoadedPath)
        {
            if (String.IsNullOrEmpty(alreadyLoadedPath)) throw new InvalidOperationException("WheelFfb owner has not loaded a module");
            var module = ShimBridgeNative.LoadLibraryExW(Path.GetFullPath(alreadyLoadedPath), IntPtr.Zero, 0x100 | 0x1000);
            if (module == IntPtr.Zero) throw new InvalidOperationException("existing WheelFfb module unavailable");
            try
            {
                describe = ShimBridgeNative.Export<SnapshotCall>(module, "GetReadDeviceInfoV1"); read = ShimBridgeNative.Export<SnapshotCall>(module, "ReadDeviceSnapshotV1");
                try { path = ShimBridgeNative.Export<PathCall>(module, "GetReadDevicePathV1"); } catch (EntryPointNotFoundException) { }
            }
            catch { ShimBridgeNative.FreeLibrary(module); throw; }
        }
        private static int Call(SnapshotCall call, int slot, byte[] bytes, int size)
        {
            if (bytes == null || bytes.Length != size) return -1;
            Array.Clear(bytes, 0, bytes.Length); BitConverter.GetBytes(size).CopyTo(bytes, 0); bytes[4] = 1;
            return call(slot, bytes);
        }
        public int Describe(int slot, byte[] bytes)
        {
            int result = Call(describe, slot, bytes, ShimBridge.DeviceSize); byte[] expected;
            if (result == 1 && expectedInfo.TryGetValue(slot, out expected))
                for (int i = 0; i < expected.Length; i++) if (bytes[i] != expected[i]) return -2;
            return result;
        }
        public int Read(int slot, byte[] bytes) => Call(read, slot, bytes, ShimBridge.SampleSize);
        public bool TryGetPath(int slot, ulong generation, out string value)
        {
            value = null; if (path == null) return false;
            var bytes = new ushort[260];
            if (path(slot, generation, bytes, bytes.Length) != 1) return false;
            int length = Array.IndexOf(bytes, (ushort)0); if (length <= 0) return false;
            var chars = new char[length]; for (int i = 0; i < length; i++) chars[i] = (char)bytes[i];
            value = new string(chars); string expected;
            if (expectedPaths.TryGetValue(slot, out expected) && value != expected) { value = null; return false; }
            return true;
        }
    }
}
