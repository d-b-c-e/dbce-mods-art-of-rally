using System.Collections.Generic;
using System.IO;

namespace ArtOfSimRally.Testing
{
    /// <summary>One request's opt-in recording diagnostics (sana 47008), parsed afresh for every request so nothing carries
    /// into the next one. rewiredInventory=1 asks for Rewired's joystick rows (rewired-inventory.tsv: names, instance
    /// GUIDs, interface paths) at the first frame Rewired is ready and once 5 s later; identityProbe=1 arms the pinned
    /// DualShock 4 identity probe. Absent means off, and an ordinary recording writes neither; any other value, or either
    /// key on a replay, refuses the request. Unity-free, so tests/Recorder drives this exact object.</summary>
    internal sealed class RecordingDiagnostics
    {
        internal static readonly RecordingDiagnostics Off = new RecordingDiagnostics();
        internal bool RewiredInventory { get; private set; }
        internal bool IdentityProbe { get; private set; }
        private int _passes;
        private float _firstAt = -1;

        internal static RecordingDiagnostics Parse(IDictionary<string, string> request, bool replay) =>
            new RecordingDiagnostics { RewiredInventory = Flag(request, "rewiredInventory", replay), IdentityProbe = Flag(request, "identityProbe", replay) };

        private static bool Flag(IDictionary<string, string> request, string key, bool replay)
        {
            if (!request.TryGetValue(key, out var value)) return false;
            if (value != "1") throw new InvalidDataException(key + " must be 1");
            if (replay) throw new InvalidDataException(key + " is only valid for recording");
            return true;
        }

        /// <summary>True while a requested inventory still has a pass to write; never for an ordinary recording, so the
        /// recorder does not touch Rewired for it at all.</summary>
        internal bool InventoryPending => RewiredInventory && _passes < 2;

        /// <summary>The inventory pass due on this recorded frame: 1 at the first frame Rewired is ready, 2 once 5 s
        /// later, otherwise 0.</summary>
        internal int InventoryPassDue(bool rewiredReady, float now)
        {
            if (!InventoryPending || !rewiredReady) return 0;
            if (_passes == 1 && now - _firstAt < 5f) return 0;
            if (_passes == 0) _firstAt = now;
            return ++_passes;
        }
    }
}
