using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ArtOfSimRally.Mod;
using ArtOfSimRally.Testing;
using Dbce.Wheel.Playback;

internal static class ForceTrial
{
    // Called only after the original capture passes exact arithmetic/hash replay.
    public static object Run(string directory, string configPath, string output)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var config = new Dictionary<string, float>();
        foreach (var p in json.RootElement.EnumerateObject())
        {
            if (p.Name is not ("gain" or "referenceN" or "smoothing") || !config.TryAdd(p.Name, p.Value.GetSingle()))
                throw new InvalidDataException("Unknown/duplicate tuning key " + p.Name);
            float v = config[p.Name];
            if (!float.IsFinite(v) || (p.Name == "referenceN" ? v <= 0 : v < 0) || (p.Name == "smoothing" && v > 1))
                throw new InvalidDataException("Invalid tuning value " + p.Name);
        }
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Trial output already exists");
        var capture = XDocument.Load(Path.Combine(directory, "manifest.xml")).Root!;
        if ((int?)capture.Attribute("schema") < 2) throw new InvalidDataException("Trials require recorded reset epochs");
        Directory.CreateDirectory(output);
        File.Copy(configPath, Path.Combine(output, "tune.json"));
        var original = new List<double>(); var candidate = new List<double>();
        int epoch = -1, changed = 0, rows = 0; float state = 0, maxDelta = 0;
        using (var writer = new StreamWriter(new FileStream(Path.Combine(output, "forces.csv"), FileMode.CreateNew)))
        {
            writer.WriteLine("sample,time_s,epoch,baseline_normalized,candidate_normalized");
            foreach (string line in File.ReadLines(Path.Combine(directory, "forces.csv")).Skip(1))
            {
                var p = line.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                if (epoch < 0) state = p[9];
                else if ((int)p[12] != epoch) state = 0;
                epoch = (int)p[12];
                float Pick(string key, float value) => config.TryGetValue(key, out var specified) ? specified : value;
                state = ForceCurve.Smooth(state, ForceCurve.Normalised(p[1], p[2], p[3], p[4], Pick("referenceN", p[5]), Pick("gain", p[6]), p[7] == 1), Pick("smoothing", p[8]));
                float delta = Math.Abs(state - p[10]); if (delta > 0.000001f) changed++;
                maxDelta = Math.Max(delta, maxDelta); original.Add(p[10]); candidate.Add(state);
                writer.WriteLine(string.Join(",", rows++, p[0].ToString("R", CultureInfo.InvariantCulture), epoch,
                    p[10].ToString("R", CultureInfo.InvariantCulture), state.ToString("R", CultureInfo.InvariantCulture)));
            }
        }
        var result = new {
            schema = "dbce.force-trial@1", game = "art-of-rally", capability = "signal-reprocess", physicalOutput = false,
            captureManifestSha256 = ArtifactHash.FileHash(Path.Combine(directory, "manifest.xml")),
            configurationSha256 = ArtifactHash.FileHash(configPath), sourceForceSha256 = ArtifactHash.FileHash(Path.Combine(directory, "forces.csv")),
            candidateLibrarySha256 = ArtifactHash.FileHash(typeof(Dbce.Wheel.Ffb.AxleForceCurve).Assembly.Location),
            rows, changedRows = changed, maxDelta,
            baseline = SignalStatistics.Summarize(original, "signed-normalized-request", true),
            candidate = SignalStatistics.Summarize(candidate, "signed-normalized-request", true),
            scope = "Same original physics signals and reset epochs; initial filter history retained. Offline steering request comparison only. Does not change owner settings, physics, telemetry or physical output."
        };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(result));
        ArtifactSeal.Complete(output, new[] { "forces.csv", "tune.json", "report.json" });
        return result;
    }
}
