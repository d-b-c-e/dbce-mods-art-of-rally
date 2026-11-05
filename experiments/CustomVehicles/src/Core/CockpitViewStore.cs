using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace RallyCustomVehicles
{
    // Pose offsets per package/set; cockpit FOV offsets shared across packages.
    // Loading never rewrites legacy views. The runtime supplies measured seat FOV.
    public sealed class CockpitViewStore
    {
        public static readonly float[] Default = { 0f, 0f, 0f, 6f, 65f };
        private readonly string path;
        private Document data = new Document();
        private readonly HashSet<string> cars = new HashSet<string>(StringComparer.Ordinal);
        private byte[] diskBytes, legacyBytes;
        public bool HasPendingChanges { get; private set; }
        private sealed class Document
        {
            public int schemaVersion = 2;
            public Dictionary<string,float[]> gamePoseOffsets = new Dictionary<string,float[]>();
            public Dictionary<string,float[]> realisticPoseOffsets = new Dictionary<string,float[]>();
            public float? gameFovOffset, realisticFovOffset, lastRealisticBaseline;
            public Dictionary<string,float[]> legacyViews = new Dictionary<string,float[]>();
        }
        public CockpitViewStore(string path) { this.path = Path.GetFullPath(path); }
        public void ConfigureCars(IEnumerable<string> ids) { foreach (var id in ids) { CheckId(id); cars.Add(id); } }
        private static void CheckId(string id) { if (!VehiclePackage.IdValid(id)) throw new InvalidDataException("Invalid vehicle id."); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Valid(float[] value, int length) => value != null && value.Length == length && value.All(Finite);
        // Compatibility name: validate/copy, with no arbitrary stored-value bounds.
        public static float[] Clamp(float[] value)
        { if (!Valid(value,5)) throw new InvalidDataException("Invalid cockpit view."); return (float[])value.Clone(); }
        public static float LensFov(float value) => Finite(value) ? Math.Max(1f,Math.Min(179f,value)) : 65f;
        private float? Seat(double? value)
        {
            if (value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value>0 && value.Value<180)
            {
                float rounded=(float)Math.Round(value.Value,1);
                if (rounded<=0 || rounded>=180) rounded=(float)value.Value;
                if (Finite(rounded) && rounded>0 && rounded<180) return rounded;
            }
            return data.lastRealisticBaseline;
        }
        public bool HasCalculatedSeat(double? seat) => Seat(seat).HasValue;
        private float[] Baseline(bool realistic,double? seat)
        { var result=(float[])Default.Clone(); if(realistic && Seat(seat) is float measured) result[4]=measured; return result; }
        public float[] Calculated(bool realistic,double? seat) => Baseline(realistic,seat);
        private static Dictionary<string,float[]> Poses(Document doc,bool realistic) => realistic ? doc.realisticPoseOffsets : doc.gamePoseOffsets;
        private static string LegacyKey(string id,bool realistic) => id+(realistic ? "_realistic" : "");
        private float? FovOffset(bool realistic) => realistic ? data.realisticFovOffset : data.gameFovOffset;
        private float[] LegacyFovs(bool realistic) => cars.Select(id=>LegacyKey(id,realistic)).Where(data.legacyViews.ContainsKey)
            .Select(key=>data.legacyViews[key][4]).Distinct().ToArray();
        public bool NeedsFovChoice(bool realistic) => !FovOffset(realistic).HasValue && LegacyFovs(realistic).Length>1;
        public bool Has(string id) => data.gamePoseOffsets.ContainsKey(id) || data.legacyViews.ContainsKey(id);
        public float[] Get(string id) => GetView(id,false,null);
        public void Set(string id,float[] value) => SetView(id,false,null,value,true);
        public float[] GetView(string id,bool realistic,double? seat)
        {
            CheckId(id);cars.Add(id);var result=Baseline(realistic,seat);float[] pose,legacy;
            bool old=data.legacyViews.TryGetValue(LegacyKey(id,realistic),out legacy);
            if(Poses(data,realistic).TryGetValue(id,out pose)) for(int i=0;i<4;i++) result[i]+=pose[i];
            else if(old) Array.Copy(legacy,result,4);
            var offset=FovOffset(realistic);
            if(offset.HasValue) result[4]+=offset.Value;
            else { var fovs=LegacyFovs(realistic);if(fovs.Length==1)result[4]=fovs[0];else if(old)result[4]=legacy[4]; }
            return result;
        }
        public void Reset(string id,bool realistic,double? seat) => SetView(id,realistic,seat,Baseline(realistic,seat),true);
        public void SetView(string id,bool realistic,double? seat,float[] value,bool fovEdited=false)
        {
            CheckId(id);cars.Add(id);value=Clamp(value);
            bool measured=!realistic || Seat(seat).HasValue;
            if(realistic && fovEdited && !measured) throw new InvalidDataException("Seat measurement is needed before choosing or resetting Realistic FOV.");
            var baseline=Baseline(realistic,seat);var next=Copy(data);
            foreach(string car in cars)
            {
                float[] legacy;
                if(!Poses(next,realistic).ContainsKey(car) && next.legacyViews.TryGetValue(LegacyKey(car,realistic),out legacy))
                    Poses(next,realistic)[car]=Enumerable.Range(0,4).Select(i=>legacy[i]-baseline[i]).ToArray();
            }
            // Keep explicit zero entries so a reset never falls back to legacy pose.
            Poses(next,realistic)[id]=Enumerable.Range(0,4).Select(i=>value[i]-baseline[i]).ToArray();
            float? offset=FovOffset(realistic);
            if(fovEdited)offset=value[4]-baseline[4];
            else if(!offset.HasValue && measured)
            {
                var fovs=LegacyFovs(realistic);
                if(fovs.Length<2)offset=fovs.Length==1 ? fovs[0]-baseline[4] : value[4]-baseline[4];
                // Conflicting valid legacy FOVs stay intact until explicit FOV
                // edit/reset chooses the set-wide value; pose edits still save.
            }
            if(realistic){next.realisticFovOffset=offset;if(measured)next.lastRealisticBaseline=baseline[4];}
            else next.gameFovOffset=offset;
            Validate(next);data=next;HasPendingChanges=true;Flush();
        }
        public void Load()
        {
            if(!File.Exists(path)){data=new Document();diskBytes=legacyBytes=null;HasPendingChanges=false;return;}
            byte[] before=File.ReadAllBytes(path);if(before.Length>65536)throw new InvalidDataException("Cockpit view file too large.");
            var json=VehiclePackage.ReadJson<JObject>(path);bool legacy=json["schemaVersion"]==null;
            var loaded=legacy ? new Document{legacyViews=json.ToObject<Dictionary<string,float[]>>()} : VehiclePackage.ReadJson<Document>(path);
            Validate(loaded);if(!File.ReadAllBytes(path).SequenceEqual(before))throw new IOException("Cockpit views changed during load.");
            data=loaded;diskBytes=before;legacyBytes=legacy ? before : null;HasPendingChanges=false;
        }
        public void Flush()
        {
            if(!HasPendingChanges)return;Validate(data);
            byte[] after=new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(data,Formatting.Indented));
            if(after.Length>65536)throw new InvalidDataException("Cockpit view file too large.");
            if(diskBytes!=null && diskBytes.SequenceEqual(after)){HasPendingChanges=false;return;}
            byte[] current=File.Exists(path) ? File.ReadAllBytes(path) : null;
            if((diskBytes==null)!=(current==null) || diskBytes!=null && !diskBytes.SequenceEqual(current))
                throw new IOException("Cockpit views changed externally; pending session edits retained, reload before saving.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(legacyBytes!=null)
            {
                string backup=path+".legacy-v1.json";
                if(File.Exists(backup)){if(!File.ReadAllBytes(backup).SequenceEqual(legacyBytes))throw new IOException("Different legacy backup already exists; preserved.");}
                else File.WriteAllBytes(backup,legacyBytes);
            }
            string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary,after);
                if(File.Exists(path))File.Replace(temporary,path,path+".previous");else File.Move(temporary,path);
                diskBytes=after;legacyBytes=null;HasPendingChanges=false;
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static Dictionary<string,float[]> Clone(Dictionary<string,float[]> source) => source.ToDictionary(p=>p.Key,p=>(float[])p.Value.Clone());
        private static Document Copy(Document d) => new Document{gamePoseOffsets=Clone(d.gamePoseOffsets),realisticPoseOffsets=Clone(d.realisticPoseOffsets),
            legacyViews=Clone(d.legacyViews),gameFovOffset=d.gameFovOffset,realisticFovOffset=d.realisticFovOffset,lastRealisticBaseline=d.lastRealisticBaseline};
        private static void Validate(Document d)
        {
            if(d==null || d.schemaVersion!=2 || d.gamePoseOffsets==null || d.realisticPoseOffsets==null || d.legacyViews==null ||
                d.gamePoseOffsets.Count>64 || d.realisticPoseOffsets.Count>64 || d.legacyViews.Count>64 ||
                d.gamePoseOffsets.Concat(d.realisticPoseOffsets).Any(p=>!VehiclePackage.IdValid(p.Key)||!Valid(p.Value,4)) ||
                d.legacyViews.Any(p=>!VehiclePackage.IdValid(p.Key)||!Valid(p.Value,5)) ||
                new[]{d.gameFovOffset,d.realisticFovOffset,d.lastRealisticBaseline}.Any(v=>v.HasValue&&!Finite(v.Value)) ||
                d.lastRealisticBaseline.HasValue && (d.lastRealisticBaseline.Value<=0 || d.lastRealisticBaseline.Value>=180) ||
                d.realisticFovOffset.HasValue && !d.lastRealisticBaseline.HasValue)
                throw new InvalidDataException("Invalid cockpit views.");
        }
    }
}
