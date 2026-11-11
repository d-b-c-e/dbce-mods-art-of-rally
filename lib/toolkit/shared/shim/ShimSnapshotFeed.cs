// The shared rules that turn one frame's accepted-input snapshot (ShimSnapshot.cs, extension 3) into capture
// observations and removals, so every in-game binding host applies them once and the same way (sana 125194/126392,
// lola 126314/126356). Portable vendorable source like ShimSnapshot.cs: no Input dependency, no clocks, devices or files.
// The host turns each item into its capture flow's Remove or Observe call and keeps its own dispatch guard.
#nullable enable
using System;
using System.Collections.Generic;

#if DBCE_INPUT_NET35
using SnapshotFeedRows = System.Collections.Generic.IList<Dbce.Wheel.Shim.ShimSnapshotFeedItem>;
using IdentityLookup = Dbce.Wheel.Shim.ShimSnapshotIdentityLookup;
#else
using SnapshotFeedRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Shim.ShimSnapshotFeedItem>;
using IdentityLookup = System.Func<Dbce.Wheel.Shim.ShimSnapshotDevice, (string Name, string IdentityKey)?>;
#endif

namespace Dbce.Wheel.Shim
{
#if DBCE_INPUT_NET35
    public struct ShimSnapshotIdentity
    {
        public readonly string Name, IdentityKey;
        public ShimSnapshotIdentity(string name, string identityKey) { Name = name; IdentityKey = identityKey; }
    }
    public delegate ShimSnapshotIdentity? ShimSnapshotIdentityLookup(ShimSnapshotDevice row);
#endif
    /// <summary>One device row's result for a frame: a removal with its reason, or a new accepted read.</summary>
    public sealed class ShimSnapshotFeedItem
    {
        private ShimSnapshotFeedItem(Guid instance, Guid product, string? reason, string name, string identityKey, ulong generation,
            ulong readSequence, int[] axes, bool[] buttons, int[] hats, int[] minimum, int[] maximum)
        {
            Instance = instance; Product = product; Reason = reason; Name = name; IdentityKey = identityKey; Generation = generation;
            ReadSequence = readSequence; Axes = axes; Buttons = buttons; Hats = hats; Minimum = minimum; Maximum = maximum;
        }
        internal static ShimSnapshotFeedItem Removal(ShimSnapshotDevice row, string reason) =>
            new ShimSnapshotFeedItem(row.Instance, row.Product, reason, "", "", row.Epoch, 0, new int[8], new bool[128], new int[4], new int[8], new int[8]);
        internal static ShimSnapshotFeedItem Observation(ShimSnapshotDevice row, ShimSnapshotSample read, string name, string identityKey)
        {
            ShimSnapshot.CaptureValues(row, read, out var axes, out var buttons, out var hats, out var minimum, out var maximum);
            return new ShimSnapshotFeedItem(row.Instance, row.Product, null, name, identityKey, row.Epoch, read.DeviceSeq, axes, buttons, hats, minimum, maximum);
        }

        public Guid Instance { get; }
        public Guid Product { get; }
        /// <summary>Non-null: remove this device (and cancel a capture on it) for this reason. Null: an observation.</summary>
        public string? Reason { get; }
        public bool IsRemoval => Reason != null;
        public string Name { get; }
        /// <summary>Opaque per-epoch identity: the owned interface path, or "row:&lt;index&gt;".</summary>
        public string IdentityKey { get; }
        /// <summary>The device row's open epoch: compare it per instance only.</summary>
        public ulong Generation { get; }
        /// <summary>The device's read sequence (device_seq).</summary>
        public ulong ReadSequence { get; }
        /// <summary>Capture values with the device caps applied (<see cref="ShimSnapshot.CaptureValues"/>).</summary>
        public int[] Axes { get; }
        public bool[] Buttons { get; }
        public int[] Hats { get; }
        public int[] Minimum { get; }
        public int[] Maximum { get; }
    }

    /// <summary>
    /// One capture host's view of its handle's snapshots (one feed per handle lifetime). Per frame, each device row is:
    /// <list type="bullet">
    /// <item>removed when it is not opened, or its read ended its epoch or belongs to another epoch;</item>
    /// <item>removed at once when its read is explicitly invalid or stale: a fresh earlier sample never bridges a known
    /// bad read;</item>
    /// <item>removed when the host has no identity report for its row, epoch and instance;</item>
    /// <item>observed when its read is valid and fresh (masks applied);</item>
    /// <item>left out when it has no read in this frame, so it ages out in the host's flow.</item>
    /// </list>
    /// A frame seq at or below one already read returns null: the same seq is the same observation, never a newer one.
    /// </summary>
    public sealed class ShimSnapshotFeed
    {
        public const string Disconnected = "Capture device disconnected or reopened. The prior binding was kept.";
        public const string ReadFailed = "Capture device read failed. The prior binding was kept.";
        public const string ReadStale = "Capture device input is stale. The prior binding was kept.";
        public const string NoIdentity = "Capture refused a device with no matching identity report. The prior binding was kept.";

        private ulong lastFrame;

        /// <param name="identity">The host's device report for a row: its name and identity key, or null when no report
        /// matches the row's index, epoch and instance.</param>
        public SnapshotFeedRows? Read(ShimSnapshot snapshot, IdentityLookup identity)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (lastFrame != 0 && snapshot.FrameSeq <= lastFrame) return null;
            lastFrame = snapshot.FrameSeq;
            var reads = new ShimSnapshotSample?[snapshot.Devices.Count];
            foreach (var read in snapshot.Samples) reads[(int)read.Index] = read;
            var items = new List<ShimSnapshotFeedItem>();
            foreach (var row in snapshot.Devices)
            {
                var read = reads[(int)row.Index];
                if (!row.Opened || read != null && (read.Ended || read.Epoch != row.Epoch)) { items.Add(ShimSnapshotFeedItem.Removal(row, Disconnected)); continue; }
                if (read == null) continue;
                if (!read.Valid) { items.Add(ShimSnapshotFeedItem.Removal(row, ReadFailed)); continue; }
                if (read.Stale) { items.Add(ShimSnapshotFeedItem.Removal(row, ReadStale)); continue; }
                var id = identity(row);
                if (id == null) { items.Add(ShimSnapshotFeedItem.Removal(row, NoIdentity)); continue; }
                items.Add(ShimSnapshotFeedItem.Observation(row, read, id.Value.Name, id.Value.IdentityKey));
            }
            return items.AsReadOnly();
        }
    }

    /// <summary>
    /// The frames in which a binding capture owns the wheel's menu and mod actions (sana 126063, lola 126356). A host notes
    /// the frame BEFORE it takes or refuses a snapshot whenever a capture is listening, and again when an observation
    /// starts, continues, completes or refuses one, because its tick (and so the capture's end) can run inside the same
    /// frame's menu read before the mod actions dispatch. One press is never both the binding and a toggle. Keyboard
    /// paths are not involved.
    /// </summary>
    public sealed class SharedBindingFrameGuard
    {
        private int frame = int.MinValue;
        public void Note(int unityFrame) => frame = unityFrame;
        /// <summary>The frame's wheel menu and mod actions belong to a capture: one is listening now, or one listened earlier
        /// in this same frame.</summary>
        public bool Owns(int unityFrame, bool listening) => listening || unityFrame == frame;
    }
}
