using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RallyCustomVehicles
{
    public sealed class SelectionStore
    {
        private readonly string path;
        private Dictionary<string,string> choices = new Dictionary<string,string>();
        public SelectionStore(string path) { this.path = path; }
        public string Get(string carClass) => choices.TryGetValue(carClass,out var id) ? id : null;
        public void Load()
        {
            if (!File.Exists(path)) return;
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Selection file too large.");
            var data = VehiclePackage.ReadJson<Dictionary<string,string>>(path);
            if (data == null || data.Count > 16) throw new InvalidDataException("Invalid custom selections.");
            foreach (var pair in data) if (!VehiclePackage.IdValid(pair.Value)) throw new InvalidDataException("Invalid saved custom vehicle id.");
            choices = data;
        }
        public void Set(string carClass, string id)
        {
            if (Get(carClass) == id) return;
            if (id == null) choices.Remove(carClass);
            else { if (!VehiclePackage.IdValid(id)) throw new InvalidDataException("Invalid vehicle id."); choices[carClass] = id; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(choices, Formatting.Indented));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous"); else File.Move(temporary,path);
        }
    }
}
