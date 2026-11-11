using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dbce.Wheel.Shim;

/// <summary>Expected game declaration, not another profile grammar. Native Core still parses and plans bindings.</summary>
public sealed record ShimExtraDefinition(string Id, string Shape, string? Range, IReadOnlyList<string> Contexts);
public sealed record ShimExtraDestination(ShimExtraDefinition Definition, int FirstSlot, int Components, uint ContextMask);
public sealed record ShimExtrasFrame(ulong Sequence, ulong Down, ulong Up, uint Contexts, IReadOnlyList<ShimExtraAction> Slots);
public readonly record struct ShimExtraAction(float Value, byte Status)
{
    public bool Live => Status == (byte)ShimStatus.Live && float.IsFinite(Value);
}

public static class ShimExtrasAbi
{
    public const int Version = 1, FrameSize = 296, MaxSlots = 32, MaxDeclaration = 4096;
    public const byte ContextInactive = 14;

    public static void Validate(int abi, int size)
    {
        if (abi != Version || size != FrameSize) throw new InvalidDataException("Shim extras ABI or frame size differs from 1/296.");
    }

    public static void ValidateDeclaration(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || bytes.Length > MaxDeclaration || bytes.Any(b => b == 0 || b > 127))
            throw new InvalidDataException("Extras declaration must be 1..4096 non-NUL ASCII bytes.");
    }

    public static ShimExtrasFrame Decode(ReadOnlySpan<byte> data, ShimFrame common, ShimExtrasInfo info, uint contexts)
    {
        if (data.Length != FrameSize || BinaryPrimitives.ReadUInt32LittleEndian(data) != FrameSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != Version)
            throw new InvalidDataException("Extras frame length, struct size or ABI differs.");
        ulong seq = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]);
        ulong down = BinaryPrimitives.ReadUInt64LittleEndian(data[16..]), up = BinaryPrimitives.ReadUInt64LittleEndian(data[24..]);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data[32..]), mask = BinaryPrimitives.ReadUInt32LittleEndian(data[36..]);
        info.ValidateContexts(contexts);
        if (seq != common.Sequence || count != info.SlotCount || count > MaxSlots || mask != contexts ||
            ((down | up) >> (int)count) != 0 || common.Latched || common.SuppressNative || common.Run != ShimRun.None)
            throw new InvalidDataException("Extras count, sequence, contexts, edges or ordinary-host state differs.");
        var slots = new ShimExtraAction[(int)count];
        for (int i = 0; i < slots.Length; i++)
        {
            int at = 40 + i * 8;
            float value = BitConverter.ToSingle(data.Slice(at, 4)); byte status = data[at + 4];
            if (status > ContextInactive || data[at + 5] != 0 || !float.IsFinite(value) || value < -1 || value > 1)
                throw new InvalidDataException("Unknown, injected or invalid extras slot.");
            slots[i] = new ShimExtraAction(value, status);
        }
        foreach (var d in info.Destinations.Values)
        {
            if ((d.Definition.Shape is "digital" or "pedal") && slots[d.FirstSlot].Value < 0)
                throw new InvalidDataException("Negative unipolar extras slot.");
            if (d.Definition.Shape == "digital" && slots[d.FirstSlot].Value is not (0 or 1))
                throw new InvalidDataException("Non-digital extras value.");
            if (d.Definition.Range == "circle")
            {
                float x = slots[d.FirstSlot].Value, y = slots[d.FirstSlot + 1].Value;
                if (x * x + y * y > 1.00001f) throw new InvalidDataException("Extras stick exceeds its declared circle.");
            }
        }
        return new ShimExtrasFrame(seq, down, up, mask, Array.AsReadOnly(slots));
    }
}

/// <summary>Verified dbce_shim_extras_info mapping. Consumers use native firstSlot/components, never line counting.</summary>
public sealed class ShimExtrasInfo
{
    public string DeclarationSha256 { get; }
    public string ContextMode { get; }
    public int SlotCount { get; }
    public uint AllowedContexts { get; }
    public IReadOnlyDictionary<string, ShimExtraDestination> Destinations { get; }

    private ShimExtrasInfo(string hash, string mode, int count, uint allowed, Dictionary<string, ShimExtraDestination> destinations)
    {
        DeclarationSha256 = hash; ContextMode = mode; SlotCount = count; AllowedContexts = allowed;
        Destinations = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ShimExtraDestination>(destinations);
    }

    public void ValidateContexts(uint mask)
    {
        if ((mask & ~AllowedContexts) != 0) throw new InvalidDataException("Unknown or advisory extras context mask.");
    }

    public static ShimExtrasInfo Verify(string json, byte[] declaration, string game, string mode,
        IReadOnlyList<string> contexts, IReadOnlyList<ShimExtraDefinition> expected)
    {
        ShimExtrasAbi.ValidateDeclaration(declaration);
        if (json.Length > 128 * 1024 || contexts.Count > 8 || contexts.Distinct(StringComparer.Ordinal).Count() != contexts.Count ||
            mode is not ("none" or "advisory" or "observed") || (mode == "none" && contexts.Count != 0))
            throw new InvalidDataException("Invalid extras declaration expectations or report size.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            var root = document.RootElement; Unique(root);
            string hash = Convert.ToHexString(SHA256.HashData(declaration));
            if (root.GetProperty("abi").GetInt32() != 1 || root.GetProperty("acceptedSchema").GetInt32() != 1 ||
                root.GetProperty("game").GetString() != game || root.GetProperty("contextMode").GetString() != mode ||
                !hash.Equals(root.GetProperty("declaration").GetString(), StringComparison.OrdinalIgnoreCase) ||
                !Names(root.GetProperty("contexts")).SequenceEqual(contexts, StringComparer.Ordinal))
                throw new InvalidDataException("Native extras declaration, schema or contexts differ.");
            var definitions = expected.Select(d => d with { Contexts = Array.AsReadOnly(d.Contexts.ToArray()) })
                .ToDictionary(d => d.Id, StringComparer.Ordinal);
            var destinations = new Dictionary<string, ShimExtraDestination>(StringComparer.Ordinal);
            ulong used = 0; int count = 0;
            foreach (var item in root.GetProperty("extras").EnumerateArray())
            {
                Unique(item);
                string id = item.GetProperty("id").GetString() ?? "";
                if (!definitions.TryGetValue(id, out var d)) throw new InvalidDataException("Unexpected native extra id.");
                int first = item.GetProperty("firstSlot").GetInt32(), components = item.GetProperty("components").GetInt32();
                string? range = item.TryGetProperty("range", out var r) ? r.GetString() : null;
                if (d.Shape is not ("digital" or "pedal" or "centred" or "stick") ||
                    (d.Shape == "stick" ? d.Range is not ("circle" or "square") : d.Range != null) ||
                    item.GetProperty("shape").GetString() != d.Shape || range != d.Range ||
                    !Names(item.GetProperty("contexts")).SequenceEqual(d.Contexts, StringComparer.Ordinal) ||
                    components != (d.Shape == "stick" ? 2 : 1) || first < 0 || first + components > ShimExtrasAbi.MaxSlots)
                    throw new InvalidDataException("Native extra shape, contexts or slot mapping differs.");
                uint mask = 0;
                foreach (string name in d.Contexts)
                {
                    int at = Array.IndexOf(contexts.ToArray(), name);
                    if (at < 0) throw new InvalidDataException("Undeclared destination context.");
                    mask |= 1u << at;
                }
                ulong bits = ((1UL << components) - 1) << first;
                if ((used & bits) != 0 || !destinations.TryAdd(id, new ShimExtraDestination(d, first, components, mask)))
                    throw new InvalidDataException("Overlapping or duplicate native extras slots.");
                used |= bits; count += components;
            }
            if (destinations.Count != definitions.Count || count > 32 || used != (1UL << count) - 1)
                throw new InvalidDataException("Incomplete native extras mapping.");
            return new ShimExtrasInfo(hash, mode, count, mode == "observed" ? (1u << contexts.Count) - 1 : 0, destinations);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { throw new InvalidDataException("Malformed native extras report.", e); }
    }

    private static string[] Names(JsonElement array) => array.EnumerateArray().Select(v => v.GetString() ?? "").ToArray();
    private static void Unique(JsonElement obj)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject()) if (!names.Add(p.Name)) throw new InvalidDataException("Duplicate extras report member.");
    }
}
