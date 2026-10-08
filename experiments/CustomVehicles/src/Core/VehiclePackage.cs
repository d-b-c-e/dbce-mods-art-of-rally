using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RallyCustomVehicles
{
    public sealed class VehicleManifest
    {
        public int schemaVersion;
        public string id, name, author, version, description, donorPrefab, model, texture, preview;
        public float wheelRadius;
        public bool cockpit = true;
        public PhysicsDefinition physics;
    }
    public sealed class PhysicsDefinition
    {
        public float? massKg, frontWeightFraction, inertiaScale;
        public float[] centerOfMassM;
        public EngineDefinition engine;
        public GearboxDefinition gearbox;
        public AxleDefinition frontAxle, rearAxle;
        public DimensionsDefinition dimensions;
        public CollisionBox collision;
    }
    public sealed class EngineDefinition
    {
        [JsonProperty(Required=Required.Always)]
        public float powerKw, peakPowerRpm, torqueNm, peakTorqueRpm, idleRpm, limitRpm;
    }
    public sealed class GearboxDefinition { public float[] forwardRatios; public float reverseRatio, finalDrive; }
    public sealed class AxleDefinition { public float? travelM, springRateNPerM, bumpDampingNsPerM, reboundDampingNsPerM, brakeTorqueNm; }
    public sealed class DimensionsDefinition { public float? wheelbaseM, frontTrackM, rearTrackM, tireRadiusM, tireWidthM; }
    public sealed class CollisionBox { public float[] centerM, sizeM; }

    public sealed class VehiclePackage
    {
        public VehicleManifest Manifest { get; private set; }
        public Asset Model { get; private set; }
        public string Folder { get; private set; }
        public string TexturePath { get; private set; }
        internal byte[] TextureData { get; private set; }
        public string ContentHash { get; private set; }
        private string manifestFile;
        public long TextureBytes { get; private set; }
        public long SourceBytes { get; private set; }
        public long TriangleCount => Model.parts.Sum(p => (long)p.triangles.Length / 3);
        public static T ReadJson<T>(string path)
        {
            using (var input = File.OpenText(path))
            using (var reader = new JsonTextReader(input) { MaxDepth = 32 })
            {
                var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("Trailing JSON content.");
                return token.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None }));
            }
        }
        public static VehiclePackage Load(string manifestPath, string packagesRoot=null)
        {
            using(var manifestLease=new ReadLease(new[] {manifestPath})) return LoadLocked(manifestPath,packagesRoot);
        }
        private static VehiclePackage LoadLocked(string manifestPath,string packagesRoot)
        {
            RejectLinks(manifestPath,packagesRoot ?? Path.GetDirectoryName(Path.GetFullPath(manifestPath)));
            if (new FileInfo(manifestPath).Length > 65536) throw new InvalidDataException("Manifest exceeds 64 KiB.");
            var m = ReadJson<VehicleManifest>(manifestPath);
            if (m == null || m.schemaVersion != 2) throw new InvalidDataException("Expected vehicle manifest schemaVersion 2.");
            if (!IdValid(m.id)) throw new InvalidDataException("id must be 1-64 lowercase letters/digits/dots/dashes/underscores.");
            Text(m.name, 80, "name"); Text(m.author, 120, "author"); Text(m.version, 32, "version");
            if (m.description != null && m.description.Length > 2048) throw new InvalidDataException("Description is too long.");
            if (m.donorPrefab == null || !Regex.IsMatch(m.donorPrefab, @"\A[A-Za-z0-9_]{1,80}\z")) throw new InvalidDataException("Invalid donorPrefab.");
            Range(m.wheelRadius, .05f, 2f, "authored wheelRadius");
            PhysicsValidation.Validate(m.physics);
            string root = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string model = LocalFile(root, m.model), texture = LocalFile(root, m.texture);
            var assetFiles=new[] {model,texture}.Concat(m.preview==null ? new string[0] : new[] {LocalFile(root,m.preview)}).ToArray();
            using(var assetLease=new ReadLease(assetFiles))
            {
            if (new FileInfo(model).Length > 64 * 1024 * 1024) throw new InvalidDataException("Model exceeds 64 MiB.");
            long textureBytes = CheckPng(texture);
            if (m.preview != null) textureBytes += CheckPng(LocalFile(root, m.preview));
            var asset = AssetLoader.Load(model);
            ModelValidation.Validate(asset);
            using (var hash = SHA256.Create())
            {
                var files = new[] { manifestPath, model, texture }.Concat(m.preview == null ? new string[0] : new[] { LocalFile(root, m.preview) });
                long sourceBytes=0;
                foreach (var path in files)
                {
                    // Length-prefix each file so boundaries are part of identity.
                    byte[] data = File.ReadAllBytes(path); sourceBytes+=data.Length;
                    byte[] length=BitConverter.GetBytes((long)data.Length);
                    hash.TransformBlock(length,0,length.Length,length,0);
                    hash.TransformBlock(data, 0, data.Length, data, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return new VehiclePackage { Manifest = m, Model = asset, Folder = root, TexturePath = texture,
                    manifestFile=Path.GetFullPath(manifestPath),
                    TextureData=File.ReadAllBytes(texture),
                    TextureBytes=textureBytes, SourceBytes=sourceBytes,
                    ContentHash = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant() };
            }
            }
        }
        internal IDisposable HoldFiles() => new ReadLease(new[] {manifestFile,LocalFile(Folder,Manifest.model),TexturePath}.Concat(Manifest.preview==null ? new string[0] : new[] {LocalFile(Folder,Manifest.preview)}));
        // Hold readers for the whole validation/hash/snapshot transaction. On
        // Windows another process cannot rewrite a file between parsing and hash.
        private sealed class ReadLease : IDisposable
        {
            private readonly List<FileStream> files=new List<FileStream>();
            internal ReadLease(IEnumerable<string> paths)
            {
                try {foreach(string path in paths.Distinct(StringComparer.OrdinalIgnoreCase)) files.Add(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read));}
                catch {Dispose(); throw;}
            }
            public void Dispose() {foreach(var file in files) file.Dispose(); files.Clear();}
        }
        public static List<VehiclePackage> Discover(string packagesRoot, Action<string> error)
        {
            var result = new List<VehiclePackage>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(packagesRoot)) return result;
            foreach (var folder in Directory.GetDirectories(packagesRoot).OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked package directories are unsupported.");
                    string path = Path.Combine(folder, "vehicle.json"); if (!File.Exists(path)) continue;
                    if (result.Count >= 16) throw new InvalidDataException("Maximum 16 loaded vehicle packages.");
                    var package = Load(path,packagesRoot);
                    if (ids.Contains(package.Manifest.id)) throw new InvalidDataException("Duplicate vehicle id: " + package.Manifest.id);
                    if(result.Sum(p=>p.TextureBytes)+package.TextureBytes > 256L*1024*1024 || result.Sum(p=>p.TriangleCount)+package.TriangleCount>500000 || result.Sum(p=>p.SourceBytes)+package.SourceBytes>192L*1024*1024)
                        throw new InvalidDataException("Catalog exceeds aggregate budget (256 MiB decoded textures, 500,000 triangles, 192 MiB source).");
                    ids.Add(package.Manifest.id);
                    result.Add(package);
                }
                catch (Exception ex) { error(Path.GetFileName(folder) + ": " + ex.Message); }
            }
            return result;
        }
        public static string LocalFile(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative != Path.GetFileName(relative) || relative.IndexOfAny(new[] { ':', '/', '\\' }) >= 0)
                throw new InvalidDataException("Assets must be simple filenames inside the vehicle folder.");
            string path = Path.Combine(root, relative);
            if (!File.Exists(path)) throw new InvalidDataException("Missing file: " + relative);
            RejectLinks(path,root);
            return path;
        }
        public static long CheckPng(string path)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("PNG exceeds 16 MiB.");
            byte[] h = new byte[24];
            using (var f = File.OpenRead(path)) if (f.Read(h, 0, h.Length) != h.Length) throw new InvalidDataException("Truncated PNG.");
            if (!h.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || Encoding.ASCII.GetString(h, 12, 4) != "IHDR") throw new InvalidDataException("Expected PNG with IHDR header.");
            long pixels=1;
            for (int i = 16; i <= 20; i += 4)
            {
                long size = ((long)h[i] << 24) | ((long)h[i+1] << 16) | ((long)h[i+2] << 8) | h[i+3];
                if (size < 1 || size > 4096) throw new InvalidDataException("PNG dimensions must be 1-4096 pixels.");
                pixels*=size;
            }
            return pixels*4;
        }
        private static void RejectLinks(string path,string boundary)
        {
            boundary=Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar);
            if(!Path.GetFullPath(path).StartsWith(boundary+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset lies outside package boundary.");
            for(string p=Path.GetFullPath(path); !string.IsNullOrEmpty(p); p=Path.GetDirectoryName(p))
            {
                if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Linked asset or parent directory is unsupported: "+p);
                if(string.Equals(p.TrimEnd(Path.DirectorySeparatorChar),boundary,StringComparison.OrdinalIgnoreCase)) break;
            }
        }
        internal static bool IdValid(string id) => id != null && Regex.IsMatch(id, @"\A[a-z0-9][a-z0-9._-]{0,63}\z");
        internal static void Range(float? value, float min, float max, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value) || value < min || value > max))
                throw new InvalidDataException(name + " must be between " + min + " and " + max + ".");
        }
        private static void Text(string value, int max, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl)) throw new InvalidDataException("Invalid " + name + ".");
        }
    }

    internal static class PhysicsValidation
    {
        internal static void Validate(PhysicsDefinition p)
        {
            if (p == null) return;
            if(p.massKg==null && p.frontWeightFraction==null && p.inertiaScale==null && p.centerOfMassM==null && p.engine==null && p.gearbox==null && p.frontAxle==null && p.rearAxle==null && p.dimensions==null && p.collision==null)
                throw new InvalidDataException("Omit physics for cosmetic packages; an empty physical definition is unsupported.");
            VehiclePackage.Range(p.massKg, 300, 12000, "massKg");
            VehiclePackage.Range(p.frontWeightFraction, .2f, .8f, "frontWeightFraction");
            VehiclePackage.Range(p.inertiaScale, .25f, 4, "inertiaScale");
            Vector(p.centerOfMassM, -3, 3, "centerOfMassM");
            if (p.engine != null)
            {
                var e = p.engine;
                VehiclePackage.Range(e.powerKw, 10, 1500, "powerKw"); VehiclePackage.Range(e.torqueNm, 20, 2500, "torqueNm");
                VehiclePackage.Range(e.idleRpm, 300, 3000, "idleRpm"); VehiclePackage.Range(e.limitRpm, 2500, 15000, "limitRpm");
                VehiclePackage.Range(e.peakPowerRpm, e.idleRpm + 100, e.limitRpm, "peakPowerRpm");
                VehiclePackage.Range(e.peakTorqueRpm, e.idleRpm, e.peakPowerRpm-100, "peakTorqueRpm");
            }
            if (p.gearbox != null)
            {
                var g = p.gearbox;
                if (g.forwardRatios == null || g.forwardRatios.Length < 1 || g.forwardRatios.Length > 8) throw new InvalidDataException("Gearbox needs 1-8 forward ratios.");
                foreach (float v in g.forwardRatios) VehiclePackage.Range(v, .1f, 8, "forward ratio");
                for (int i=1; i<g.forwardRatios.Length; i++) if (g.forwardRatios[i] >= g.forwardRatios[i-1]) throw new InvalidDataException("Forward ratios must strictly decrease.");
                VehiclePackage.Range(g.reverseRatio, -8, -.1f, "reverseRatio"); VehiclePackage.Range(g.finalDrive, 1, 8, "finalDrive");
            }
            foreach (var a in new[] { p.frontAxle, p.rearAxle })
            {
                if (a == null) continue;
                VehiclePackage.Range(a.travelM, .05f, .6f, "travelM"); VehiclePackage.Range(a.springRateNPerM, 5000, 300000, "springRateNPerM");
                VehiclePackage.Range(a.bumpDampingNsPerM, 100, 30000, "bumpDampingNsPerM"); VehiclePackage.Range(a.reboundDampingNsPerM, 100, 30000, "reboundDampingNsPerM");
                VehiclePackage.Range(a.brakeTorqueNm, 100, 12000, "brakeTorqueNm");
            }
            if (p.dimensions != null)
            {
                var d=p.dimensions;
                VehiclePackage.Range(d.wheelbaseM, 1.2f, 6, "wheelbaseM");
                VehiclePackage.Range(d.frontTrackM, .8f, 3, "frontTrackM"); VehiclePackage.Range(d.rearTrackM, .8f, 3, "rearTrackM");
                VehiclePackage.Range(d.tireRadiusM, .15f, .9f, "tireRadiusM"); VehiclePackage.Range(d.tireWidthM, .1f, .6f, "tireWidthM");
            }
            if (p.collision != null)
            {
                Vector(p.collision.centerM, -6, 6, "collision.centerM", true);
                Vector(p.collision.sizeM, .1f, 12, "collision.sizeM", true);
            }
        }
        private static void Vector(float[] v, float min, float max, string name, bool required=false)
        {
            if (v == null && !required) return;
            if (v == null || v.Length != 3) throw new InvalidDataException(name + " must contain X, Y, Z.");
            foreach (float x in v) VehiclePackage.Range(x,min,max,name);
        }
    }
}
