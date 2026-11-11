// Mirrors native/shim/shim_dll/dbce_shim_snapshot.h (extension 3, snapshot ABI 1): the accepted raw-input snapshot of an
// ordinary shim handle, for in-game binding capture without a second reader (sana 124000/125280).
// Portable vendorable source: block namespace, classes, BitConverter and #nullable only, so netstandard2.1, net48 and
// Unity Mono hosts can vendor it byte-exact. No native loading, devices, clocks, files or UI here: the host binds
// dbce_shim_snapshot itself and hands this code a delegate for it.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if DBCE_INPUT_NET35
using SnapshotDeviceRows = System.Collections.Generic.IList<Dbce.Wheel.Shim.ShimSnapshotDevice>;
using SnapshotSampleRows = System.Collections.Generic.IList<Dbce.Wheel.Shim.ShimSnapshotSample>;
using InvalidDataException = System.IO.IOException;
#else
using SnapshotDeviceRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Shim.ShimSnapshotDevice>;
using SnapshotSampleRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Shim.ShimSnapshotSample>;
#endif

namespace Dbce.Wheel.Shim
{
    public static class ShimSnapshotAbi
    {
        public const int Version = 1, HeaderSize = 56, DeviceSize = 192, SampleSize = 224, MaxDevices = 64;
        public const uint FlagOpened = 1, FlagPath = 2;
        /// <summary>dbce_shim.h results the snapshot call returns.</summary>
        public const int Ok = 0, BadArgument = -1, WrongThread = -2, Refused = -3, Stale = -5;

        /// <summary>The loaded DLL's dbce_shim_snapshot_abi_version and dbce_shim_snapshot_sizes.</summary>
        public static void Validate(int version, int headerSize, int deviceSize, int sampleSize)
        {
            if (version != Version || headerSize != HeaderSize || deviceSize != DeviceSize || sampleSize != SampleSize)
                throw new InvalidDataException("Shim snapshot ABI " + version + ", sizes " + headerSize + "/" + deviceSize + "/" + sampleSize +
                    "; expected " + Version + ", " + HeaderSize + "/" + DeviceSize + "/" + SampleSize + ".");
        }
    }

    /// <summary>dbce::shim::Call, Core's classification of a poll or state call (not an HRESULT).</summary>
    public enum ShimSnapshotCall : byte { Ok, AcquisitionLost, Gone, NotCalled, Pending }

    /// <summary>One row of Core's device table as the frame left it.</summary>
    public sealed class ShimSnapshotDevice
    {
        private readonly byte[] buttonMask;
        private readonly int[] minimum, maximum;
        internal ShimSnapshotDevice(uint index, uint epoch, uint flags, uint axisMask, uint rangeMask, uint povMask, byte[] buttonMask,
            int[] minimum, int[] maximum, Guid instance, Guid product)
        {
            Index = index; Epoch = epoch; Opened = (flags & ShimSnapshotAbi.FlagOpened) != 0; HasPath = (flags & ShimSnapshotAbi.FlagPath) != 0;
            AxisMask = axisMask; RangeMask = rangeMask; PovMask = povMask; this.buttonMask = buttonMask; this.minimum = minimum; this.maximum = maximum;
            Instance = instance; Product = product;
        }
        public uint Index { get; }
        /// <summary>The open epoch: current when <see cref="Opened"/>, otherwise the last one (0: never opened).</summary>
        public uint Epoch { get; }
        public bool Opened { get; }
        /// <summary>An owned interface path exists; its text is in dbce_shim_device_reports.</summary>
        public bool HasPath { get; }
        public uint AxisMask { get; }
        public uint RangeMask { get; }
        public uint PovMask { get; }
        public Guid Instance { get; }
        public Guid Product { get; }
        public bool HasButton(int index) => index >= 0 && index < 128 && (buttonMask[index / 8] & (1 << (index % 8))) != 0;
        public int Minimum(int axis) => minimum[axis];
        public int Maximum(int axis) => maximum[axis];
    }

    /// <summary>One device read of the frame.</summary>
    public sealed class ShimSnapshotSample
    {
        private readonly int[] axes;
        private readonly byte[] buttons;
        private readonly uint[] pov;
        internal ShimSnapshotSample(uint index, uint epoch, ulong deviceSeq, long readStartUs, long readEndUs, ShimSnapshotCall pollCall,
            ShimSnapshotCall stateCall, bool valid, bool stale, bool ended, int[] axes, byte[] buttons, uint[] pov)
        {
            Index = index; Epoch = epoch; DeviceSeq = deviceSeq; ReadStartUs = readStartUs; ReadEndUs = readEndUs; PollCall = pollCall;
            StateCall = stateCall; Valid = valid; Stale = stale; Ended = ended; this.axes = axes; this.buttons = buttons; this.pov = pov;
        }
        public uint Index { get; }
        public uint Epoch { get; }
        /// <summary>The device's read sequence: one more per read, across epochs. Unchanged means no new read.</summary>
        public ulong DeviceSeq { get; }
        public long ReadStartUs { get; }
        public long ReadEndUs { get; }
        public ShimSnapshotCall PollCall { get; }
        public ShimSnapshotCall StateCall { get; }
        /// <summary>Both calls OK: the values are a complete state read.</summary>
        public bool Valid { get; }
        /// <summary>Valid but older than the header's FreshUs at its BoundaryUs: Core did not use it.</summary>
        public bool Stale { get; }
        /// <summary>This read ended its epoch; the device row is no longer opened.</summary>
        public bool Ended { get; }
        public int Axis(int index) => axes[index];
        /// <summary>The DirectInput byte: down when 0x80 is set.</summary>
        public byte Button(int index) => buttons[index];
        /// <summary>Hundredths of a degree; 0xFFFFFFFF centered.</summary>
        public uint Pov(int index) => pov[index];
    }

    /// <summary>One frame's snapshot, decoded and checked. Raw physical evidence only; normalized values stay in the frame.</summary>
    public sealed class ShimSnapshot
    {
        /// <summary>The host's dbce_shim_snapshot for its handle: (frame_seq, header, devices, device capacity, samples,
        /// sample capacity) to the native result. The buffers are byte arrays of the struct sizes times the capacities.</summary>
        public delegate int NativeCall(ulong frameSeq, byte[] header, byte[] devices, int deviceCapacity, byte[] samples, int sampleCapacity);

        private ShimSnapshot(ulong frameSeq, long tickUs, long boundaryUs, long freshUs, ShimSnapshotDevice[] devices, ShimSnapshotSample[] samples)
        { FrameSeq = frameSeq; TickUs = tickUs; BoundaryUs = boundaryUs; FreshUs = freshUs; Devices = Array.AsReadOnly(devices); Samples = Array.AsReadOnly(samples); }

        public ulong FrameSeq { get; }
        public long TickUs { get; }
        public long BoundaryUs { get; }
        public long FreshUs { get; }
        /// <summary>Core's device table in row order (<see cref="ShimSnapshotDevice.Index"/> equals the position).</summary>
        public SnapshotDeviceRows Devices { get; }
        /// <summary>The frame's reads, at most one per device row.</summary>
        public SnapshotSampleRows Samples { get; }

        /// <summary>
        /// Takes the snapshot of <paramref name="frameSeq"/>: the frame the host's last successful tick returned, taken right
        /// after that tick. A capacity shortfall (BAD_ARGUMENT with the header filled for this seq) is retried once with the
        /// reported counts; the same seq is the same observation. Any other result is returned as is, with no snapshot.
        /// </summary>
        public static int Take(NativeCall call, ulong frameSeq, out ShimSnapshot? snapshot)
        {
            snapshot = null;
            int devices = 4, samples = 4;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var header = NewHeader();
                byte[] deviceBytes = new byte[devices * ShimSnapshotAbi.DeviceSize], sampleBytes = new byte[samples * ShimSnapshotAbi.SampleSize];
                int r = call(frameSeq, header, deviceBytes, devices, sampleBytes, samples);
                if (r == ShimSnapshotAbi.Ok) { snapshot = Decode(header, deviceBytes, sampleBytes); return r; }
                if (r != ShimSnapshotAbi.BadArgument || attempt == 1 || BitConverter.ToUInt64(header, 16) != frameSeq ||
                    BitConverter.ToUInt32(header, 4) != ShimSnapshotAbi.Version) return r;
                uint needDevices = BitConverter.ToUInt32(header, 48), needSamples = BitConverter.ToUInt32(header, 52);
                if (needDevices > ShimSnapshotAbi.MaxDevices || needSamples > ShimSnapshotAbi.MaxDevices ||
                    needDevices <= devices && needSamples <= samples) return r;
                devices = Math.Max(1, (int)needDevices); samples = Math.Max(1, (int)needSamples);
            }
            return ShimSnapshotAbi.BadArgument;
        }

        /// <summary>A header with the three input sizes set.</summary>
        public static byte[] NewHeader()
        {
            var header = new byte[ShimSnapshotAbi.HeaderSize];
            Put(header, 0, ShimSnapshotAbi.HeaderSize); Put(header, 8, ShimSnapshotAbi.DeviceSize); Put(header, 12, ShimSnapshotAbi.SampleSize);
            return header;
        }

        /// <summary>Decodes a successful call's buffers. Any layout, count, index, flag or identity mismatch throws
        /// InvalidDataException: a refused snapshot, never a guess.</summary>
        public static ShimSnapshot Decode(byte[] header, byte[] devices, byte[] samples)
        {
            if (header == null || devices == null || samples == null) throw new InvalidDataException("Snapshot buffers are missing.");
            if (header.Length != ShimSnapshotAbi.HeaderSize || U32(header, 0) != ShimSnapshotAbi.HeaderSize || U32(header, 4) != ShimSnapshotAbi.Version ||
                U32(header, 8) != ShimSnapshotAbi.DeviceSize || U32(header, 12) != ShimSnapshotAbi.SampleSize)
                throw new InvalidDataException("Snapshot header has another size or ABI.");
            uint deviceCount = U32(header, 48), sampleCount = U32(header, 52);
            if (deviceCount > ShimSnapshotAbi.MaxDevices || sampleCount > deviceCount)
                throw new InvalidDataException("Snapshot counts " + deviceCount + "/" + sampleCount + " are out of range.");
            if (devices.Length < deviceCount * ShimSnapshotAbi.DeviceSize || samples.Length < sampleCount * ShimSnapshotAbi.SampleSize)
                throw new InvalidDataException("Snapshot buffers are shorter than their counts.");

            var rows = new ShimSnapshotDevice[deviceCount];
            for (int i = 0; i < rows.Length; i++)
            {
                int at = i * ShimSnapshotAbi.DeviceSize;
                if (U32(devices, at) != ShimSnapshotAbi.DeviceSize || U32(devices, at + 4) != ShimSnapshotAbi.Version || U32(devices, at + 8) != i)
                    throw new InvalidDataException("Snapshot device row " + i + " has another size, ABI or index.");
                uint flags = U32(devices, at + 16);
                if ((flags & ~(ShimSnapshotAbi.FlagOpened | ShimSnapshotAbi.FlagPath)) != 0) throw new InvalidDataException("Snapshot device row " + i + " has unknown flags.");
                var mask = new byte[16]; Buffer.BlockCopy(devices, at + 32, mask, 0, 16);
                int[] minimum = new int[8], maximum = new int[8];
                for (int a = 0; a < 8; a++) { minimum[a] = BitConverter.ToInt32(devices, at + 48 + a * 4); maximum[a] = BitConverter.ToInt32(devices, at + 80 + a * 4); }
                rows[i] = new ShimSnapshotDevice((uint)i, U32(devices, at + 12), flags, U32(devices, at + 20), U32(devices, at + 24), U32(devices, at + 28), mask,
                    minimum, maximum, GuidText(devices, at + 112, "instance", i), GuidText(devices, at + 152, "product", i));
            }

            var reads = new ShimSnapshotSample[sampleCount]; var seen = new HashSet<uint>();
            for (int i = 0; i < reads.Length; i++)
            {
                int at = i * ShimSnapshotAbi.SampleSize;
                uint index = U32(samples, at + 8);
                if (U32(samples, at) != ShimSnapshotAbi.SampleSize || U32(samples, at + 4) != ShimSnapshotAbi.Version || index >= deviceCount || !seen.Add(index))
                    throw new InvalidDataException("Snapshot sample " + i + " has another size or ABI, or a missing or repeated device row.");
                byte poll = samples[at + 40], state = samples[at + 41], valid = samples[at + 42], stale = samples[at + 43], ended = samples[at + 44];
                if (poll > (byte)ShimSnapshotCall.Pending || state > (byte)ShimSnapshotCall.Pending || valid > 1 || stale > 1 || ended > 1 ||
                    valid == 1 && (poll != 0 || state != 0) || stale == 1 && valid == 0)
                    throw new InvalidDataException("Snapshot sample " + i + " has inconsistent call or validity fields.");
                int[] axes = new int[8]; var buttons = new byte[128]; var pov = new uint[4];
                for (int a = 0; a < 8; a++) axes[a] = BitConverter.ToInt32(samples, at + 48 + a * 4);
                Buffer.BlockCopy(samples, at + 80, buttons, 0, 128);
                for (int p = 0; p < 4; p++) pov[p] = U32(samples, at + 208 + p * 4);
                reads[i] = new ShimSnapshotSample(index, U32(samples, at + 12), BitConverter.ToUInt64(samples, at + 16), BitConverter.ToInt64(samples, at + 24),
                    BitConverter.ToInt64(samples, at + 32), (ShimSnapshotCall)poll, (ShimSnapshotCall)state, valid == 1, stale == 1, ended == 1, axes, buttons, pov);
            }
            return new ShimSnapshot(BitConverter.ToUInt64(header, 16), BitConverter.ToInt64(header, 24), BitConverter.ToInt64(header, 32),
                BitConverter.ToInt64(header, 40), rows, reads);
        }

        /// <summary>
        /// A valid read's capture values with the device's caps applied (as Art's accepted-input host does): an axis outside
        /// axis_mask and range_mask, or without a range, is 0 with minimum = maximum = 0; a button is down only when present
        /// and its byte has 0x80; a hat is its angle only when present and neither centered (0xFFFFFFFF) nor 36000 or more,
        /// otherwise -1. Throws for an invalid read or a read of another row or epoch.
        /// </summary>
        public static void CaptureValues(ShimSnapshotDevice device, ShimSnapshotSample sample, out int[] axes, out bool[] buttons, out int[] hats,
            out int[] minimum, out int[] maximum)
        {
            if (!sample.Valid || sample.Index != device.Index || sample.Epoch != device.Epoch)
                throw new InvalidOperationException("Capture values need a valid read of this device row and epoch.");
            axes = new int[8]; minimum = new int[8]; maximum = new int[8]; buttons = new bool[128]; hats = new int[4];
            for (int i = 0; i < 8; i++)
                if ((device.AxisMask & device.RangeMask & (1u << i)) != 0 && device.Maximum(i) > device.Minimum(i))
                { axes[i] = sample.Axis(i); minimum[i] = device.Minimum(i); maximum[i] = device.Maximum(i); }
            for (int i = 0; i < 128; i++) buttons[i] = device.HasButton(i) && (sample.Button(i) & 0x80) != 0;
            for (int i = 0; i < 4; i++)
            {
                uint value = sample.Pov(i);
                hats[i] = (device.PovMask & (1u << i)) != 0 && value != 0xFFFFFFFF && value < 36000 ? (int)value : -1;
            }
        }

        private static uint U32(byte[] b, int at) => BitConverter.ToUInt32(b, at);
        private static void Put(byte[] b, int at, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, b, at, 4);

        private static bool GuidValue(string text, out Guid guid)
        {
#if DBCE_INPUT_NET35
            guid = Guid.Empty;
            try { guid = new Guid(text); return text == guid.ToString("B"); }
            catch (FormatException) { return false; }
#else
            return Guid.TryParseExact(text, "B", out guid);
#endif
        }

        // Lower-case braced GUID text, NUL-terminated within its 40 bytes (the profile's dev=/prod= form).
        private static Guid GuidText(byte[] b, int at, string field, int row)
        {
            int end = Array.IndexOf(b, (byte)0, at, 40);
            string text = end < 0 ? "" : Encoding.ASCII.GetString(b, at, end - at);
            if (end < 0 || text.Length != 38 || text != text.ToLowerInvariant() || !GuidValue(text, out var guid) || guid == Guid.Empty)
                throw new InvalidDataException("Snapshot device row " + row + " has no lower-case braced " + field + " GUID.");
            return guid;
        }
    }
}
