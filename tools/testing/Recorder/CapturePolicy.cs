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
    internal sealed class CapturePolicy
    {
        internal bool SharedCarInput { get; private set; }
        internal float Seconds { get; private set; } = 12f;
        internal string StopFile { get; private set; }
        internal string Source => SharedCarInput ? "shared-shim-integration" : "synthetic-integration";

        internal static CapturePolicy Parse(IDictionary<string, string> request)
        {
            var policy = new CapturePolicy();
            if (request.TryGetValue("carInput", out var input) && input != "synthetic")
            {
                if (input != "shared") throw new InvalidDataException("carInput must be synthetic or shared");
                policy.SharedCarInput = true; policy.Seconds = 30f;
            }
            if (request.TryGetValue("captureSeconds", out var seconds))
            {
                if (!float.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 5 || value > 120)
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
