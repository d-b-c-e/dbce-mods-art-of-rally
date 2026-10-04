using System.Globalization;
using System.Xml.Linq;
using Dbce.Wheel.Playback;

internal static class RecordedOutputs
{
    public static object Read(string directory, XElement manifest, List<float[]> forces)
    {
        var channels = new Dictionary<string, (int offset, string unit)> {
            ["hudSpeed"] = (256, "HUD-scaled m/s (0.6 x physics speed)"), ["engineRpm"] = (16, "rpm"),
            ["accelerationRight"] = (20, "m/s^2"), ["accelerationUp"] = (24, "m/s^2"), ["accelerationForward"] = (28, "m/s^2"),
            ["velocityRight"] = (32, "m/s"), ["velocityUp"] = (36, "m/s"), ["velocityForward"] = (40, "m/s"),
            ["angularVelocityRight"] = (44, "rad/s"), ["angularVelocityUp"] = (48, "rad/s"), ["angularVelocityForward"] = (52, "rad/s")
        };
        var data = channels.ToDictionary(p => p.Key, _ => new List<double>());
        string packets = Path.Combine(directory, "telemetry.tsv");
        int count = 0, active = 0, effects = 0;
        if (File.Exists(packets))
        {
            ArtifactSeal.Verify(directory, "telemetry.tsv", "effects.tsv", "forces.csv", "manifest.xml");
            double last = -1;
            foreach (string line in File.ReadLines(packets).Skip(1))
            {
                var p = line.Split('\t');
                if (p.Length != 3 || int.Parse(p[0], CultureInfo.InvariantCulture) != count++) throw new InvalidDataException("Telemetry sequence differs");
                double time = double.Parse(p[1], CultureInfo.InvariantCulture);
                if (!double.IsFinite(time) || time < last) throw new InvalidDataException("Telemetry clock differs");
                last = time;
                var bytes = Convert.FromBase64String(p[2]);
                if (bytes.Length != 324 || bytes[323] != 0x52) throw new InvalidDataException("Telemetry layout/sentinel differs");
                int racing = BitConverter.ToInt32(bytes, 0);
                if (racing != 0 && racing != 1) throw new InvalidDataException("Telemetry racing flag differs");
                foreach (var channel in channels)
                {
                    float value = BitConverter.ToSingle(bytes, channel.Value.offset);
                    if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite telemetry " + channel.Key);
                    if (racing == 1) data[channel.Key].Add(value);
                }
                if (racing == 1) active++;
            }
            last = -1;
            foreach (string line in File.ReadLines(Path.Combine(directory, "effects.tsv")).Skip(1))
            {
                var p = line.Split('\t');
                if (p.Length != 4) throw new InvalidDataException("Effect row shape differs");
                double time = double.Parse(p[0], CultureInfo.InvariantCulture);
                if (!double.IsFinite(time) || time < last) throw new InvalidDataException("Effect clock differs");
                last = time; effects++;
            }
        }
        return new {
            physicalOutput = (bool?)manifest.Attribute("physicalOutput"),
            captureSource = (string?)manifest.Attribute("captureSource") ?? (string?)manifest.Attribute("origin"),
            steering = SignalStatistics.Summarize(forces.Select(p => (double)p[10]), "signed-normalized-request", true),
            rawFrontAxleLateralForce = SignalStatistics.Summarize(forces.Select(p => (double)p[1]), "N"),
            telemetry = new { available = count > 0, packets = count, racingPackets = active,
                channels = data.ToDictionary(p => p.Key, p => SignalStatistics.Summarize(p.Value, channels[p.Key].unit)) },
            effectRequests = new { available = File.Exists(packets), count = effects, file = "effects.tsv", scope = "requested native arguments before output muting" },
            scope = "Per-sample statistics from the original physics capture. Race-on telemetry includes the countdown. Compare matching scenarios and speeds; nominal requests are not measured wheel torque."
        };
    }
}
