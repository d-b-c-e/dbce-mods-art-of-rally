// NET35-safe fixed ABI1/296 scalar frame codec. No native module, provider or game dependency.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
#if DBCE_INPUT_NET35
using InvalidDataException = System.IO.IOException;
#endif
namespace Dbce.Wheel.Input
{
    public struct DeclaredExtraValue
    {
        public readonly float Value;
        public readonly byte Status;
        public bool Live { get { return Status == 12 && !float.IsNaN(Value) && !float.IsInfinity(Value); } }
        public DeclaredExtraValue(float value, byte status) { Value = value; Status = status; }
    }
    public sealed class DeclaredExtrasFrame
    {
        public const int Abi = 1, Size = 296;
        public ulong Sequence { get; private set; }
        public ulong Down { get; private set; }
        public ulong Up { get; private set; }
        public uint Contexts { get; private set; }
        public IList<DeclaredExtraValue> Slots { get; private set; }
        private DeclaredExtrasFrame(ulong sequence, ulong down, ulong up, uint contexts, List<DeclaredExtraValue> values)
        { Sequence = sequence; Down = down; Up = up; Contexts = contexts; Slots = values.AsReadOnly(); }
        public static DeclaredExtrasFrame Decode(byte[] data, ulong commonSequence, bool ordinary,
            uint contexts, DeclaredControlExtras declaration)
        {
            if (data == null || data.Length != Size || BitConverter.ToUInt32(data, 0) != Size || BitConverter.ToUInt32(data, 4) != Abi)
                throw new InvalidDataException("Extras frame length, size or ABI differs from 1/296.");
            ulong sequence = BitConverter.ToUInt64(data, 8), down = BitConverter.ToUInt64(data, 16), up = BitConverter.ToUInt64(data, 24);
            uint count = BitConverter.ToUInt32(data, 32), mask = BitConverter.ToUInt32(data, 36);
            if (!ordinary || sequence == 0 || sequence != commonSequence || count != declaration.Actions.Count || count > 32 ||
                mask != contexts || (mask & ~declaration.AllowedContexts) != 0 || ((down | up) >> (int)count) != 0)
                throw new InvalidDataException("Extras sequence, count, context, edges or ordinary state differs.");
            var values = new List<DeclaredExtraValue>();
            for (int i = 0; i < count; ++i) {
                int at = 40 + i * 8; float value = BitConverter.ToSingle(data, at); byte status = data[at + 4];
                var definition = declaration.Actions[i];
                if (status > 14 || data[at + 5] != 0 || data[at + 6] != 0 || data[at + 7] != 0 ||
                    float.IsNaN(value) || float.IsInfinity(value) || value < -1 || value > 1 ||
                    definition.Shape != ControlShape.Centred && value < 0 || definition.Shape == ControlShape.Digital && value != 0 && value != 1 ||
                    status == 12 && (mask & definition.Contexts) == 0)
                    throw new InvalidDataException("Invalid, injected, unknown or context-inactive extras slot.");
                values.Add(new DeclaredExtraValue(value, status));
            }
            return new DeclaredExtrasFrame(sequence, down, up, mask, values);
        }
    }
}
