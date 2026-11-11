using System.Buffers.Binary;

namespace Dbce.Wheel.Shim;

// Mirrors native/shim/shim_dll/dbce_shim.h and shim/shim.hpp. These are ABI
// ordinals, not Woden or iRacing action mappings.
public enum ShimStatus : byte
{
    Unbound, Invalid, Unsupported, DeviceMissing, ProductMismatch, ObjectMissing,
    RangeUnqualified, Opening, ReadPending, ReadFailed, Stale, Rearming, Live, TestNeutral
}

public enum ShimRun : byte { None, Latched, Waiting, Running, Completed, Expired, Failed, Inert }

public readonly record struct ShimAction(float Value, ShimStatus Status, bool Injected)
{
    public float Applied => Status == ShimStatus.Live && float.IsFinite(Value) ? Value : 0f;
}

public sealed record ShimFrame(ulong Sequence, long TickMicroseconds, ulong Down, ulong Up,
    ShimRun Run, bool Latched, bool SuppressNative, uint SampleCount, IReadOnlyList<ShimAction> Actions);

public static class ShimAbi2
{
    public const int Version = 2, FrameSize = 296, OpenArgsSize = 128, ActionCount = 31;
    public static readonly IReadOnlyList<string> ActionNames = Array.AsReadOnly(new[] {
        "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
        "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight", "camera", "lookBack",
        "reset", "horn", "modMenu", "muteFfb", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8"
    });

    public static void Validate(int version, int frameSize, int openArgsSize, Func<int, string?> actionName)
    {
        if (version != Version || frameSize != FrameSize || openArgsSize != OpenArgsSize)
            throw new InvalidDataException($"Shim ABI {version}, frame {frameSize}, arguments {openArgsSize}; expected {Version}/{FrameSize}/{OpenArgsSize}.");
        for (int i = 0; i < ActionCount; i++)
            if (!string.Equals(actionName(i), ActionNames[i], StringComparison.Ordinal))
                throw new InvalidDataException($"Shim action {i} differs from '{ActionNames[i]}'.");
        if (actionName(ActionCount) != null) throw new InvalidDataException("Shim has more than 31 actions.");
    }

    public static ShimFrame Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != FrameSize || BinaryPrimitives.ReadUInt32LittleEndian(data) != FrameSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != Version)
            throw new InvalidDataException("Shim frame length, struct size or ABI differs from ABI 2.");
        var run = (ShimRun)data[40];
        if (run > ShimRun.Inert) throw new InvalidDataException("Unknown shim run state.");
        var actions = new ShimAction[ActionCount];
        for (int i = 0; i < actions.Length; i++)
        {
            int at = 48 + i * 8;
            var status = (ShimStatus)data[at + 4];
            if (status > ShimStatus.TestNeutral) throw new InvalidDataException($"Unknown shim status at action {i}.");
            actions[i] = new ShimAction(BitConverter.ToSingle(data.Slice(at, 4)), status, data[at + 5] != 0);
        }
        return new ShimFrame(BinaryPrimitives.ReadUInt64LittleEndian(data[8..]),
            BinaryPrimitives.ReadInt64LittleEndian(data[16..]), BinaryPrimitives.ReadUInt64LittleEndian(data[24..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[32..]), run, data[41] != 0, data[42] != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(data[44..]), actions);
    }
}
