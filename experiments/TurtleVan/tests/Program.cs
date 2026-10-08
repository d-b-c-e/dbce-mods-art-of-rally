using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TurtleVan;

internal static class Program
{
    private static int Main(string[] args)
    {
        var a = AssetLoader.Load(args[0]);
        Require(a.version == 1 && a.parts.Length == 31 && a.origins.Length == 6, "model sections");
        Require(a.parts.Sum(p => p.triangles.Length / 3) == 44812, "complete model triangle count");
        Require(Math.Abs(a.camera[0] + .47f) < .0001f && Math.Abs(a.camera[1] - 1.88f) < .0001f, "driver eye");
        string temp = Path.GetTempFileName();
        try
        {
            foreach (string invalid in new[] { "null", "{}", "{\"version\":2}", "{\"version\":1,\"camera\":[0,0,0]}", "{garbled" })
                Reject(temp, invalid);
            var p = a.parts[0]; int original = p.triangles[0];
            p.triangles[0] = p.vertices.Length;
            Reject(temp, JsonConvert.SerializeObject(a)); p.triangles[0] = original;
            p.normals = null; Reject(temp, JsonConvert.SerializeObject(a));
        }
        finally { File.Delete(temp); }
        Console.WriteLine("PASS: exact production loader parsed 31 meshes / 44,812 triangles; driver eye and 7 corrupt-model cases checked.");
        Console.WriteLine("JSON library: " + typeof(JsonConvert).Assembly.FullName);
        return 0;
    }
    private static void Reject(string path, string json)
    {
        File.WriteAllText(path, json);
        try { AssetLoader.Load(path); }
        catch (InvalidDataException) { return; }
        throw new Exception("Corrupt model was accepted.");
    }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Failed: " + label);
    }
}
