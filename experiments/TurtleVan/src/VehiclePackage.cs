using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace TurtleVan
{
    public sealed class VehicleManifest
    {
        public int schemaVersion;
        public string id, name, author, donorPrefab, model, texture;
        public float wheelRadius;
        public bool cockpit = true;
    }

    public sealed class VehiclePackage
    {
        public VehicleManifest Manifest { get; private set; }
        public Asset Model { get; private set; }
        public string TexturePath { get; private set; }

        public static VehiclePackage Load(string manifestPath)
        {
            if (new FileInfo(manifestPath).Length > 65536) throw new InvalidDataException("Vehicle manifest exceeds 64 KiB.");
            var m = JsonConvert.DeserializeObject<VehicleManifest>(File.ReadAllText(manifestPath),
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 16 });
            if (m == null || m.schemaVersion != 1) throw new InvalidDataException("Unsupported vehicle manifest schema.");
            if (m.id == null || !Regex.IsMatch(m.id, @"\A[a-z0-9][a-z0-9._-]{0,63}\z")) throw new InvalidDataException("Vehicle id must use lowercase letters, digits, dot, dash or underscore.");
            if (string.IsNullOrWhiteSpace(m.name) || m.name.Length > 80 || m.name.Any(char.IsControl)) throw new InvalidDataException("Invalid vehicle display name.");
            if (m.donorPrefab == null || !Regex.IsMatch(m.donorPrefab, @"\A[A-Za-z0-9_]{1,80}\z")) throw new InvalidDataException("Invalid donor prefab name.");
            if (float.IsNaN(m.wheelRadius) || float.IsInfinity(m.wheelRadius) || m.wheelRadius < .05f || m.wheelRadius > 2f) throw new InvalidDataException("wheelRadius must be between 0.05 and 2 metres.");
            string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string model = LocalFile(root, m.model), texture = LocalFile(root, m.texture);
            if (new FileInfo(model).Length > 64 * 1024 * 1024 || new FileInfo(texture).Length > 16 * 1024 * 1024) throw new InvalidDataException("Vehicle model or texture exceeds package limits.");
            var asset = AssetLoader.Load(model);
            var fl = asset.origins.Single(o => o.name == "wheelFL").position;
            var rl = asset.origins.Single(o => o.name == "wheelRL").position;
            if (fl[2] - rl[2] < .3f) throw new InvalidDataException("Front wheels must be ahead of rear wheels (+Z forward), with a wheelbase of at least 0.3 metres.");
            if (asset.parts.Sum(p => (long)p.triangles.Length / 3) > 100000 || asset.parts.Length > 128) throw new InvalidDataException("Vehicle exceeds 100,000 triangles or 128 mesh parts.");
            return new VehiclePackage { Manifest = m, Model = asset, TexturePath = texture };
        }

        public static List<VehiclePackage> Discover(string modRoot, Action<string> error)
        {
            var paths = new List<string>();
            string builtIn = Path.Combine(modRoot, "vehicle.json"), external = Path.Combine(modRoot, "Vehicles");
            if (File.Exists(builtIn)) paths.Add(builtIn);
            if (Directory.Exists(external))
                paths.AddRange(Directory.GetDirectories(external).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p => Path.Combine(p, "vehicle.json")).Where(File.Exists));
            var result = new List<VehiclePackage>(); var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                try
                {
                    var package = Load(path);
                    if (!ids.Add(package.Manifest.id)) throw new InvalidDataException("Duplicate vehicle id: " + package.Manifest.id);
                    result.Add(package);
                }
                catch (Exception ex) { error(path + ": " + ex.Message); }
            }
            return result;
        }

        private static string LocalFile(string root, string relative)
        {
            // v1 packages deliberately use simple filenames, with no remote paths,
            // scripts or Unity bundles. No third-party executable code is loaded.
            if (string.IsNullOrWhiteSpace(relative) || relative != Path.GetFileName(relative) || relative.IndexOfAny(new[] { ':', '/', '\\' }) >= 0)
                throw new InvalidDataException("Model and texture must be filenames inside the vehicle folder.");
            string path = Path.Combine(root, relative);
            if (!File.Exists(path)) throw new InvalidDataException("Missing vehicle file: " + relative);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked vehicle files are unsupported.");
            return path;
        }
    }
}
