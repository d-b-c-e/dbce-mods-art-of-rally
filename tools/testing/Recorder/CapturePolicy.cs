using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ArtOfSimRally.Testing
{
    // How a capture-driver recording ends and who drives the car at the stage.
    // synthetic (default): the driver's short synthetic drive, 12 s after UNDERWAY.
    // shared: the menu script still reaches the stage, then the car is left to the game and the mod's
    // shared input; the capture ends when the probe's receipt appears or after captureSeconds.
    // native: at the stage the driver scripts the Rewired values Art reads (NativeEvidence.At) and tapes
    // Art's own result beside the mod's final one; the capture ends after captureSeconds. The route first waits
    // at the title until the mod's shared session has started (it qualifies only in the menus; the 03:23 smoke
    // began qualifying about 29 s after launch, the route reaches the stage about 28 s after recording starts).
    internal sealed class CapturePolicy
    {
        internal bool SharedCarInput { get; private set; }
        internal bool NativeCarInput { get; private set; }
        internal float Seconds { get; private set; } = 12f;
        internal string StopFile { get; private set; }
        internal string Source => SharedCarInput ? "shared-shim-integration" : NativeCarInput ? "native-script-integration" : "synthetic-integration";

        internal static CapturePolicy Parse(IDictionary<string, string> request)
        {
            var policy = new CapturePolicy();
            if (request.TryGetValue("carInput", out var input) && input != "synthetic")
            {
                if (input == "shared") policy.SharedCarInput = true;
                else if (input == "native") policy.NativeCarInput = true;
                else throw new InvalidDataException("carInput must be synthetic, shared or native");
                policy.Seconds = input == "native" ? 12f : 30f;
            }
            if (request.TryGetValue("captureSeconds", out var seconds))
            {
                // NaN fails both comparisons, so finiteness is checked first (romi 31018).
                if (!float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || float.IsNaN(value) || float.IsInfinity(value) || value < 5 || value > 120)
                    throw new InvalidDataException("captureSeconds must be 5..120");
                policy.Seconds = value;
            }
            if (request.TryGetValue("stopWhenFile", out var stop) && stop.Length > 0)
            {
                if (!policy.SharedCarInput) throw new InvalidDataException("stopWhenFile needs carInput=shared");
                if (!Path.IsPathRooted(stop)) throw new InvalidDataException("stopWhenFile must be an absolute path");
                policy.StopFile = stop;
            }
            return policy;
        }

        internal const float HoldSeconds = 120f;
        // Native only: true while the route must wait for the shared session; refuses after HoldSeconds.
        internal bool Holds(bool sharedStarted, float now, ref float heldSince)
        {
            if (!NativeCarInput || sharedStarted) return false;
            if (heldSince < 0) heldSince = now;
            if (now - heldSince > HoldSeconds) throw new InvalidOperationException("the shared session did not start within " + HoldSeconds + " s in the menus");
            return true;
        }

        // The stop file ends the capture in any state (the probe may refuse before the stage);
        // the time bound starts at UNDERWAY.
        internal bool Done(bool underway, float now, ref float underwayAt, Func<string, bool> exists)
        {
            if (StopFile != null && exists(StopFile)) return true;
            if (!underway) return false;
            if (underwayAt < 0) underwayAt = now;
            return now - underwayAt >= Seconds;
        }
    }
}
