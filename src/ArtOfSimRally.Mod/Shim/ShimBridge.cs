using System;
using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Shim
{
    internal interface IPhysicalSnapshotSource
    {
        int Describe(int slot, byte[] metadata);
        int Read(int slot, byte[] sample);
    }
    internal interface IShimBridge
    {
        bool Devices(byte[] devices, int count);
        bool Tick(byte[] samples, int count, byte[] frame);
        void Stop();
    }
    internal interface IPhysicalSnapshotPathSource
    {
        bool TryGetPath(int slot, ulong expectedGeneration, out string path);
    }
    internal interface IShimPathBridge
    {
        bool DevicesWithPaths(byte[] devices, int count, string[] paths);
    }

    // A prepared session borrows Art's existing read slots. Metadata qualification
    // occurs once at the explicit idle handoff; each frame reads each slot once.
    // All physical bytes, including source time and sequence, pass through intact.
    internal sealed class ShimBridge
    {
        internal const int DeviceSize = 144, SampleSize = 232;
        private readonly IPhysicalSnapshotSource source;
        private readonly IShimBridge native;
        private readonly ShimAxisConsumer consumer;
        private readonly int[] slots;
        private readonly ulong[] generations;
        private readonly byte[][] samples;
        private readonly byte[] batch, frame = new byte[ShimFrame.Size];
        private int lastFrame = -1;
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        internal bool Stopped { get; private set; }
        internal string Failure { get; private set; }

        internal ShimBridge(IPhysicalSnapshotSource source, IShimBridge native, ShimAxisConsumer consumer, int[] openSlots)
        {
            if (source == null || native == null || consumer == null || openSlots == null || openSlots.Length == 0 || openSlots.Length > 64)
                throw new ArgumentException("bridge needs qualified, already open read slots");
            this.source = source; this.native = native; this.consumer = consumer;
            slots = (int[])openSlots.Clone(); generations = new ulong[slots.Length]; samples = new byte[slots.Length][];
            batch = new byte[slots.Length * SampleSize];
            var metadata = new byte[slots.Length * DeviceSize]; var unique = new HashSet<int>();
            var pathSource = source as IPhysicalSnapshotPathSource;
            var paths = pathSource == null ? null : new string[slots.Length];
            try
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] < 0 || !unique.Add(slots[i])) throw new ArgumentException("duplicate or invalid read slot");
                    var device = new byte[DeviceSize]; samples[i] = new byte[SampleSize];
                    if (source.Describe(slots[i], device) != 1 || BitConverter.ToUInt32(device, 0) != DeviceSize || BitConverter.ToUInt32(device, 4) != 1 ||
                        (generations[i] = BitConverter.ToUInt64(device, 8)) == 0) throw new InvalidOperationException("unqualified read slot " + slots[i]);
                    Buffer.BlockCopy(device, 0, metadata, i * DeviceSize, DeviceSize);
                    if (pathSource != null && !pathSource.TryGetPath(slots[i], generations[i], out paths[i]))
                        throw new InvalidOperationException("qualified owned interface path unavailable for slot " + slots[i]);
                }
                var pathBridge = native as IShimPathBridge;
                bool accepted = paths == null ? native.Devices(metadata, slots.Length) :
                    pathBridge != null && pathBridge.DevicesWithPaths(metadata, slots.Length, paths);
                if (!accepted) throw new InvalidOperationException("shared provider refused metadata/owned paths");
            }
            catch { native.Stop(); throw; }
        }

        // Copies this frame's physical values into Art's legacy cache, allowing
        // its existing shortcuts/FFB observations to share the SAME read.
        internal bool ReadFrame(int gameFrame, Action<int, byte[]> cache)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("bridge read called on another thread");
            if (Stopped || gameFrame == lastFrame) return !Stopped;
            if (gameFrame < lastFrame) { Stop("game frame regressed"); return false; }
            lastFrame = gameFrame;
            try
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var sample = samples[i]; Array.Clear(sample, 0, sample.Length);
                    int result = source.Read(slots[i], sample);
                    if (result != 1 || BitConverter.ToUInt32(sample, 0) != SampleSize || BitConverter.ToUInt32(sample, 4) != 1 ||
                        BitConverter.ToUInt64(sample, 8) != generations[i]) { Stop("read slot lost its qualified generation"); return false; }
                    Buffer.BlockCopy(sample, 0, batch, i * SampleSize, SampleSize);
                }
                Array.Clear(frame, 0, frame.Length); BitConverter.GetBytes(ShimFrame.Size).CopyTo(frame, 0);
                if (!native.Tick(batch, slots.Length, frame)) { Stop("shared provider refused physical batch"); return false; }
                string why;
                if (!consumer.Publish(frame, gameFrame, out why)) { Stop(why); return false; }
                // Only after the whole batch and resolved frame were accepted.
                for (int i = 0; i < slots.Length; i++) cache(slots[i], samples[i]);
                return true;
            }
            catch (Exception ex) { Stop(ex.Message); return false; }
        }
        internal void Stop(string why)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("bridge stop called on another thread");
            if (Stopped) return;
            Stopped = true; Failure = why; consumer.Stop(); native.Stop();
        }
    }
}
