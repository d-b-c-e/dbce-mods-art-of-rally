using System;
using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Triple;

// Presence is conservative: even disabled/stale developer markers suppress writes.
internal static class LandingLaunchPolicy
{
    internal static string Block(string mute, string shim, bool recorder, bool request,
        bool editor, IEnumerable<string> arguments)
    {
        if (!string.IsNullOrEmpty(mute)) return "muted-launch marker";
        if (!string.IsNullOrEmpty(shim)) return "shim-probe marker";
        if (recorder || request) return "developer recorder/session marker";
        if (editor) return "editor launch";
        foreach (var arg in arguments)
            if (string.Equals(arg, "-batchmode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-nographics", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase)) return "batch/test launch";
        return null;
    }
}
