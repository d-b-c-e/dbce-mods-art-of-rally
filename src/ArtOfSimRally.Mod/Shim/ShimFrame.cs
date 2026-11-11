using System;

namespace ArtOfSimRally.Mod.Shim
{
    // Wire contract only: profile parsing, device identity, calibration, rearm and
    // normalization remain in shared Core. The header is lib/toolkit/shim/dbce_shim.h.
    internal enum ShimStatus : byte
    {
        Unbound, Invalid, Unsupported, DeviceMissing, ProductMismatch, ObjectMissing,
        RangeUnqualified, Opening, ReadPending, ReadFailed, Stale, Rearming, Live, TestNeutral
    }

    internal readonly struct ShimAction
    {
        internal readonly float Value;
        internal readonly ShimStatus Status;
        internal ShimAction(float value, ShimStatus status)
        { Value = value; Status = status; }
        internal bool Owned => Status != ShimStatus.Unbound && Status != ShimStatus.Invalid && Status != ShimStatus.Unsupported;
        internal float Applied => Status == ShimStatus.Live ? Value : 0f;
    }

    internal sealed class ShimFrame
    {
        internal const int Abi = 2, Size = 296, OpenArgsSize = 128, Count = 31;
        internal static readonly string[] Names = {
            "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
            "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight", "camera", "lookBack",
            "reset", "horn", "modMenu", "muteFfb", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8"
        };
        private readonly ShimAction[] actions;
        internal readonly ulong Sequence, Down, Up;
        internal readonly long TickMicroseconds;
        internal readonly byte Run;
        internal readonly bool Latched, SuppressNative;
        internal readonly uint SampleCount;
        internal ShimAction this[int index] => actions[index];
        private ShimFrame(byte[] bytes, ShimAction[] values)
        {
            actions = values; Sequence = BitConverter.ToUInt64(bytes, 8); TickMicroseconds = BitConverter.ToInt64(bytes, 16);
            Down = BitConverter.ToUInt64(bytes, 24); Up = BitConverter.ToUInt64(bytes, 32); Run = bytes[40];
            Latched = bytes[41] != 0; SuppressNative = bytes[42] != 0; SampleCount = BitConverter.ToUInt32(bytes, 44);
        }
        internal static bool TryDecode(byte[] bytes, out ShimFrame frame, out string reason)
        {
            frame = null;
            reason = "shim frame requires little-endian ABI 2 and exactly 296 bytes";
            if (!BitConverter.IsLittleEndian || bytes == null || bytes.Length != Size ||
                BitConverter.ToUInt32(bytes, 0) != Size || BitConverter.ToUInt32(bytes, 4) != Abi) return false;
            reason = "shim frame has invalid flags, run state, time or edge bits";
            if (bytes[40] > 7 || bytes[41] > 1 || bytes[42] > 1 || bytes[43] != 0 || BitConverter.ToInt64(bytes, 16) < 0 ||
                (BitConverter.ToUInt64(bytes, 24) >> Count) != 0 || (BitConverter.ToUInt64(bytes, 32) >> Count) != 0) return false;
            var values = new ShimAction[Count];
            for (int i = 0; i < Count; i++)
            {
                int at = 48 + i * 8;
                float value = BitConverter.ToSingle(bytes, at);
                reason = "invalid shim action " + Names[i];
                if (bytes[at + 4] > (byte)ShimStatus.TestNeutral || bytes[at + 5] > 1 || bytes[at + 6] != 0 || bytes[at + 7] != 0 ||
                    float.IsNaN(value) || float.IsInfinity(value) || value < (i == 0 ? -1f : 0f) || value > 1f) return false;
                // bytes[at + 5] is Core's injected flag, set only in latched test sessions, which Art refuses.
                values[i] = new ShimAction(value, (ShimStatus)bytes[at + 4]);
            }
            frame = new ShimFrame(bytes, values); reason = ""; return true;
        }
    }
}
