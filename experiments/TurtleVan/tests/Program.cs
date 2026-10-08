using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TurtleVan;

internal static class Program
{
    private static int Main(string[] args)
    {
        var a = AssetLoader.Load(args[0]);
        Require(a.version == 1 && a.parts.Length == 31 && a.origins.Length == 6, "model sections");
        var report=JObject.Parse(File.ReadAllText(args[1]));
        int triangles=a.parts.Sum(p => p.triangles.Length / 3);
        Require(triangles == (int)report["triangles"], "complete model triangle count from independent geometry validator");
        Require(Math.Abs(a.camera[0] + .517f) < .0001f && Math.Abs(a.camera[1] - 1.88f) < .0001f, "driver eye");
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
        Console.WriteLine($"PASS: exact production loader parsed {a.parts.Length} meshes / {triangles} triangles; driver eye and 7 corrupt-model cases checked.");
        Console.WriteLine("JSON library: " + typeof(JsonConvert).Assembly.FullName);
        CheckSelection();
        CheckPackages(args[0], args[2]);
        PatchContracts.Check(args[3], args[4]);
        return 0;
    }
    private static void CheckSelection()
    {
        var choice = new SelectionState(10, 7);
        Require(choice.Choose(10, true) == 7 && choice.Chosen, "new menu entry maps to native donor");
        Require(choice.ToSavedIndex(10) == 7, "no custom slot persisted");
        for (int i = 0; i < 10; i++) Require(choice.ToSavedIndex(i) == i, "stock save selection unchanged");
        Require(choice.Choose(3, true) == 3 && !choice.Chosen, "selecting stock car clears visual choice");
        Require(choice.Choose(10, false) == 10 && !choice.Chosen, "other class index cannot activate vehicle");
        Require(!new SelectionState(10, 7).Chosen, "next launch has no automatic visual override");
        Console.WriteLine("PASS: menu selection, stock fallback persistence, class isolation and reset.");
    }
    private static void CheckPackages(string model, string manifest)
    {
        string root = Path.Combine(Path.GetTempPath(), "vehicle-package-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = JObject.Parse(File.ReadAllText(manifest));
            File.Copy(model, Path.Combine(root, (string)config["model"]));
            File.Copy(Path.Combine(Path.GetDirectoryName(model), "palette.png"), Path.Combine(root, (string)config["texture"]));
            string path = Path.Combine(root, "vehicle.json");
            File.WriteAllText(path, config.ToString());
            Require(VehiclePackage.Load(path).Manifest.id == "dbce.turtle-van", "built-in vehicle package");
            var errors = new System.Collections.Generic.List<string>();
            // A second independently named vehicle can share the model format.
            string extra = Path.Combine(root, "Vehicles", "second"); Directory.CreateDirectory(extra);
            foreach (var file in Directory.GetFiles(root)) File.Copy(file, Path.Combine(extra, Path.GetFileName(file)));
            config["id"] = "test.second"; config["name"] = "Second Vehicle"; config["donorPrefab"] = "Car_Mini";
            File.WriteAllText(Path.Combine(extra, "vehicle.json"), config.ToString());
            Require(VehiclePackage.Discover(root, errors.Add).Count == 2 && errors.Count == 0, "multiple original model packages");
            config["id"] = "dbce.turtle-van";
            File.WriteAllText(Path.Combine(extra, "vehicle.json"), config.ToString());
            Require(VehiclePackage.Discover(root, errors.Add).Count == 1 && errors.Count == 1, "duplicate isolated");
            config["schemaVersion"] = 99;
            File.WriteAllText(Path.Combine(extra, "vehicle.json"), config.ToString());
            Require(VehiclePackage.Discover(root, errors.Add).Count == 1, "malformed extra does not break good package");
            config["schemaVersion"] = 1; config["model"] = "../turtle-van.json";
            File.WriteAllText(path, config.ToString());
            try { VehiclePackage.Load(path); throw new Exception("Traversal accepted"); } catch (InvalidDataException) { }
            config["model"] = "turtle-van.json"; config["wheelRadius"] = 0;
            File.WriteAllText(path, config.ToString());
            try { VehiclePackage.Load(path); throw new Exception("Invalid radius accepted"); } catch (InvalidDataException) { }
            Console.WriteLine("PASS: package loading, multiple packages, duplicate/malformed isolation, path containment and wheel dimensions.");
        }
        finally { Directory.Delete(root, true); }
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
