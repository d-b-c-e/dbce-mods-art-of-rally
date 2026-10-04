using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArtOfSimRally.Testing
{
    /// <summary>Opt-in integration fixture: an existing menu script plus a short, synthetic physics drive.
    /// It records the live result, never copies source car poses. Physical/platform outputs must be muted.</summary>
    internal sealed class CaptureDriver
    {
        private readonly SessionAligner<Dictionary<string, string>> menus;
        private readonly Dictionary<string, int> markers;
        private float underwayAt = -1;
        private readonly float duration;
        internal Dictionary<string, string> Values { get; private set; } = new Dictionary<string, string>();
        internal CaptureDriver(string source, float duration)
        {
            this.duration = duration;
            var names = File.ReadAllLines(Path.Combine(source, "markers.tsv")).Select(l => l.Split('\t')).ToDictionary(p => int.Parse(p[0], CultureInfo.InvariantCulture), p => p[1]);
            markers = names.ToDictionary(p => p.Value, p => p.Key);
            var rows = new List<KeyValuePair<int, Dictionary<string, string>>>();
            foreach (var line in File.ReadLines(Path.Combine(source, "input.tape")))
            {
                var p = line.Split('\t'); var values = new Dictionary<string, string>();
                foreach (var field in p.Skip(2)) { int eq = field.LastIndexOf('='); if (eq > 0) values[field.Substring(0, eq)] = field.Substring(eq + 1); }
                rows.Add(new KeyValuePair<int, Dictionary<string, string>>(int.Parse(p[1], CultureInfo.InvariantCulture), values));
            }
            menus = new SessionAligner<Dictionary<string, string>>(rows, names, "capture menu driver", () => Time.realtimeSinceStartup);
        }
        internal bool Tick(string marker)
        {
            if (marker.EndsWith("|UNDERWAY"))
            {
                if (underwayAt < 0) underwayAt = Time.realtimeSinceStartup;
                return Time.realtimeSinceStartup - underwayAt >= duration;
            }
            if (menus.Next(markers.TryGetValue(marker, out var id) ? id : -1, out var values)) Values = values;
            else Values = new Dictionary<string, string>();
            if (menus.Failed != null) throw new InvalidOperationException(menus.Failed);
            return false;
        }
        internal void Input(ref float throttle, ref float brake, ref float steer, ref float handbrake, ref float clutch, ref bool start)
        {
            // Stay near the start at modest speed. The captured physics result is the replay oracle.
            throttle = 0.3f; brake = 0; steer = underwayAt < 0 ? 0 : 0.1f * Mathf.Sin((Time.realtimeSinceStartup - underwayAt) * 0.4f);
            handbrake = 0; clutch = 0; start = true;
        }
    }
}
