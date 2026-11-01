using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
namespace RallyCustomVehicles
{
    // Each car's cockpit view (eye left/right, up/down, forward/back in metres, look-down degrees, field of view), saved
    // per package id in the mod's State folder (owner 2026-10-31: the Scout's eye sat mid-car and could not be kept).
    public sealed class CockpitViewStore
    {
        public static readonly float[] Default = { 0f, 0f, 0f, 6f, 65f };
        private readonly string path;
        private Dictionary<string, float[]> views = new Dictionary<string, float[]>();
        public CockpitViewStore(string path) { this.path = path; }

        public bool Has(string id) => id != null && views.ContainsKey(id);
        public float[] Get(string id) => id != null && views.TryGetValue(id, out var v) ? (float[])v.Clone() : (float[])Default.Clone();

        public static float[] Clamp(float[] v) => new[] {
            Math.Max(-.8f, Math.Min(.8f, v[0])), Math.Max(-.6f, Math.Min(.8f, v[1])), Math.Max(-1.5f, Math.Min(1.5f, v[2])),
            Math.Max(-25f, Math.Min(35f, v[3])), Math.Max(20f, Math.Min(120f, v[4])) };   // owner: the eye could not move back far enough

        public void Load()
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Cockpit view file too large.");
            var data = VehiclePackage.ReadJson<Dictionary<string, float[]>>(path);
            if (data == null || data.Count > 64 || data.Any(p => !VehiclePackage.IdValid(p.Key) || p.Value == null || p.Value.Length != 5 ||
                p.Value.Any(x => float.IsNaN(x) || float.IsInfinity(x)))) throw new InvalidDataException("Invalid cockpit views.");
            views = data.ToDictionary(p => p.Key, p => Clamp(p.Value));
        }

        public void Set(string id, float[] value)
        {
            if (!VehiclePackage.IdValid(id)) throw new InvalidDataException("Invalid vehicle id.");
            var clamped = Clamp(value);
            if (views.TryGetValue(id, out var old) && old.SequenceEqual(clamped)) return;
            views[id] = clamped;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(views, Formatting.Indented));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous"); else File.Move(temporary, path);
        }
    }
}
