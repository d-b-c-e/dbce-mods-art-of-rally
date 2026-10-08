using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TurtleVan
{
    // Pure managed DTOs: these types are loaded by UMM, not built into a Unity player.
    [Serializable] public class Asset { public int version; public float[] camera; public Origin[] origins; public MaterialInfo[] materials; public Part[] parts; }
    [Serializable] public class Origin { public string name; public float[] position; }
    [Serializable] public class MaterialInfo { public string name; public float[] color; }
    [Serializable] public class Part { public string group, material; public float[] vertices, normals, uv; public int[] triangles; }

    public static class AssetLoader
    {
        public static Asset Load(string path)
        {
            // Use the game's managed JSON library; Unity's native JsonUtility did
            // not populate this dynamically loaded mod's asset on the owner's run.
            Asset a;
            try { a = JsonConvert.DeserializeObject<Asset>(File.ReadAllText(path)); }
            catch (JsonException ex) { throw new InvalidDataException("Invalid Turtle Van JSON: " + path, ex); }
            if (a == null || a.version != 1) throw new InvalidDataException("Unsupported Turtle Van model schema: " + (a == null ? "null" : a.version.ToString()));
            Check(a.camera, 3, "camera");
            if (a.origins == null || a.materials == null || a.parts == null || a.parts.Length == 0)
                throw new InvalidDataException("Model requires origins, materials and meshes.");
            var groups = new HashSet<string>();
            foreach (var o in a.origins)
            {
                if (o == null || string.IsNullOrEmpty(o.name) || !groups.Add(o.name)) throw new InvalidDataException("Invalid or duplicate model group.");
                Check(o.position, 3, "origin " + o.name);
            }
            foreach (var name in new[] { "body", "steering", "wheelFL", "wheelFR", "wheelRL", "wheelRR" })
                if (!groups.Contains(name)) throw new InvalidDataException("Missing model group: " + name);
            var materials = new HashSet<string>();
            foreach (var m in a.materials)
            {
                if (m == null || string.IsNullOrEmpty(m.name) || !materials.Add(m.name)) throw new InvalidDataException("Invalid or duplicate model material.");
                Check(m.color, 4, "material " + m.name);
            }
            foreach (var p in a.parts)
            {
                if (p == null || !groups.Contains(p.group) || !materials.Contains(p.material)) throw new InvalidDataException("Unknown mesh group/material.");
                if (p.vertices == null || p.vertices.Length == 0 || p.vertices.Length % 3 != 0) throw new InvalidDataException("Invalid mesh vertices: " + p.group);
                Check(p.vertices, p.vertices.Length, "vertices");
                Check(p.normals, p.vertices.Length, "normals");
                Check(p.uv, p.vertices.Length / 3 * 2, "UVs");
                if (p.triangles == null || p.triangles.Length == 0 || p.triangles.Length % 3 != 0) throw new InvalidDataException("Invalid triangle array.");
                foreach (int i in p.triangles)
                    if (i < 0 || i >= p.vertices.Length / 3) throw new InvalidDataException("Triangle index outside vertex array.");
            }
            return a;
        }
        private static void Check(float[] values, int length, string field)
        {
            if (values == null || values.Length != length) throw new InvalidDataException("Invalid model " + field + " length.");
            foreach (float v in values)
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Non-finite model " + field + ".");
        }
    }
}
