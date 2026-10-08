using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RallyCustomVehicles;

internal static class Program
{
    private static int assertions;
    private static string temp;
    private static int Main(string[] args)
    {
        temp=Path.Combine(Path.GetTempPath(),"custom-vehicles-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var packages=VehiclePackage.Discover(args[0],Console.WriteLine);
            Check(packages.Count==2,"two independently authored examples discovered");
            Check(packages.Select(p=>p.TriangleCount).OrderBy(x=>x).SequenceEqual(new long[] {26336,82804}),"complete examples exported");
            TestPackages(packages[0]); TestPolicy(); TestPersistence(); TestJournal();
            foreach(var p in packages)
            {
                var identity=VehicleIdentity.From(p);
                Check(identity.Matches(JsonConvert.DeserializeObject<VehicleIdentity>(JsonConvert.SerializeObject(identity))),"identity survives record roundtrip");
                Check(!identity.Matches(VehicleIdentity.From(p,true)),"physical and cosmetic identities differ");
                Check(!identity.Matches(VehicleIdentity.From(packages.First(x=>x!=p))),"different packages cannot replay as equivalent");
            }
            PatchContracts.Check(args[1],args[2]);
            Console.WriteLine("PASS: "+assertions+" managed assertions. No game, Unity scene, network or hardware execution.");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // Only this freshly created, known absolute scratch directory.
            if(Path.GetFileName(temp).StartsWith("custom-vehicles-tests-",StringComparison.Ordinal)) Directory.Delete(temp,true);
        }
    }
    private static JObject Manifest(string id="test.car") => JObject.FromObject(new VehicleManifest {
        schemaVersion=2,id=id,name="Test Vehicle",author="Test",version="1.0.0",donorPrefab="Car_Mini",model="model.json",texture="palette.png",wheelRadius=.4f
    });
    private static Asset Model()
    {
        var names=new[] {"body","steering","wheelFL","wheelFR","wheelRL","wheelRR"};
        var xyz=new[] {new float[] {0,0,0},new float[] {0,1,0},new float[] {-1,.4f,1},new float[] {1,.4f,1},new float[] {-1,.4f,-1},new float[] {1,.4f,-1}};
        return new Asset {version=1,units="metres",camera=new float[] {-.4f,1.5f,0},
            origins=names.Select((n,i)=>new Origin {name=n,position=xyz[i]}).ToArray(),
            materials=new[] {new MaterialInfo {name="paint",color=new float[] {1,1,0,1}}},
            parts=names.Select(n=>new Part {group=n,material="paint",vertices=new float[] {0,0,0,1,0,0,0,1,0},normals=new float[] {0,0,1,0,0,1,0,0,1},uv=new float[] {.5f,.5f,.5f,.5f,.5f,.5f},triangles=new[] {0,1,2}}).ToArray()};
    }
    private static string Fixture(string folder,VehiclePackage example,string id="test.car")
    {
        var path=Path.Combine(temp,folder); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path,"vehicle.json"),Manifest(id).ToString());
        File.WriteAllText(Path.Combine(path,"model.json"),JsonConvert.SerializeObject(Model()));
        File.Copy(example.TexturePath,Path.Combine(path,"palette.png"));
        return Path.Combine(path,"vehicle.json");
    }
    private static void TestPackages(VehiclePackage example)
    {
        string path=Fixture("single",example),model=Path.Combine(Path.GetDirectoryName(path),"model.json");
        var good=Manifest(); string original=good.ToString();
        var baseline=VehiclePackage.Load(path); Check(baseline.TriangleCount==6,"minimal complete model");
        var bad=new Action<JObject>[] {
            j=>j["schemaVersion"]=1, j=>j["id"]="../bad", j=>j["id"]="UPPER", j=>j["name"]="", j=>j["author"]="\n",
            j=>j["wheelRadius"]=0, j=>j["model"]="../model.json", j=>j["model"]="C:\\model.json", j=>j["texture"]="palette.png:stream",
            j=>j["unknown"]=true, j=>j["physics"]=new JObject(), j=>j["physics"]=JObject.Parse("{massKg:100}"),
            j=>j["physics"]=JObject.Parse("{massKg:'NaN'}"), j=>j["physics"]=JObject.Parse("{frontWeightFraction:0.99}"),
            j=>j["physics"]=JObject.Parse("{centerOfMassM:[0,1]}"), j=>j["physics"]=JObject.Parse("{engine:{powerKw:200}}"),
            j=>j["physics"]=JObject.Parse("{gearbox:{forwardRatios:[1,2],reverseRatio:-3,finalDrive:4}}"),
            j=>j["physics"]=JObject.Parse("{collision:{centerM:[0,0,0],sizeM:[1,-1,1]}}"),
            j=>j["physics"]=JObject.Parse("{dimensions:{frontTrackM:900}}"),
            j=>j["physics"]=JObject.Parse("{frontAxle:{springRateNPerM:0}}")
        };
        foreach(var mutate in bad) { var j=JObject.Parse(original); mutate(j); File.WriteAllText(path,j.ToString()); Reject(()=>VehiclePackage.Load(path),"invalid manifest rejected"); }
        foreach(string json in new[] {"null","{bad",original+" {}",original.Replace("\"schemaVersion\": 2","\"schemaVersion\": 2, \"schemaVersion\": 2")})
        {File.WriteAllText(path,json); Reject(()=>VehiclePackage.Load(path),"ambiguous JSON rejected");}
        File.WriteAllText(path,original);
        var corruptModel=new Action<Asset>[] {
            a=>a.units="centimetres", a=>a.parts[0].triangles[1]=99, a=>a.parts[0].normals[2]=-1,
            a=>a.parts[0].vertices[3]=float.PositiveInfinity, a=>a.parts[0].uv[0]=2,
            a=>a.parts[0].triangles[1]=0, a=>a.origins[2].position[0]=2, a=>a.camera[1]=float.NaN,
            a=>a.parts=a.parts.Take(5).ToArray(), a=>a.parts[0].normals=null
        };
        foreach(var mutate in corruptModel) {var a=Model(); mutate(a); File.WriteAllText(model,JsonConvert.SerializeObject(a)); Reject(()=>VehiclePackage.Load(path),"invalid mesh rejected");}
        File.WriteAllText(model,JsonConvert.SerializeObject(Model()));
        var modified=JObject.Parse(original); modified["name"]="New Name"; File.WriteAllText(path,modified.ToString());
        Check(VehiclePackage.Load(path).ContentHash!=baseline.ContentHash,"package identity includes manifest");
        Fixture("catalog/a",example,"test.a"); string duplicate=Fixture("catalog/b",example,"test.a"); Fixture("catalog/c",example,"test.c");
        File.WriteAllText(Fixture("catalog/d",example,"test.d"),"{corrupt}");
        var errors=new List<string>(); var found=VehiclePackage.Discover(Path.Combine(temp,"catalog"),errors.Add);
        Check(found.Select(p=>p.Manifest.id).SequenceEqual(new[] {"test.a","test.c"}) && errors.Count==2,"duplicate and corrupt neighbor isolation");
        File.WriteAllText(duplicate,Manifest("test.b").ToString()); errors.Clear();
        Check(VehiclePackage.Discover(Path.Combine(temp,"catalog"),errors.Add).Count==3 && errors.Count==1,"fixed package can be added without rebuilding");
        for(int i=0;i<17;i++) Fixture("limit/"+i.ToString("00"),example,"test."+i);
        errors.Clear(); Check(VehiclePackage.Discover(Path.Combine(temp,"limit"),errors.Add).Count==16 && errors.Count==1,"catalog count bounded");
        // Header-only allocation-budget fixtures. Unity decoding is deliberately
        // not invoked by this test or claimed as covered by the header validator.
        for(int i=0;i<5;i++)
        {
            string large=Fixture("texture-budget/"+i,example,"test.big"+i);
            string image=Path.Combine(Path.GetDirectoryName(large),"palette.png"); var bytes=File.ReadAllBytes(image);
            foreach(int offset in new[] {16,20}) {bytes[offset]=0; bytes[offset+1]=0; bytes[offset+2]=16; bytes[offset+3]=0;}
            File.WriteAllBytes(image,bytes);
        }
        errors.Clear(); Check(VehiclePackage.Discover(Path.Combine(temp,"texture-budget"),errors.Add).Count==4 && errors.Count==1,"aggregate decoded texture budget enforced");
        using(baseline.HoldFiles())
        {
            bool refused=false;
            try {using(var writer=File.Open(model,FileMode.Open,FileAccess.Write,FileShare.ReadWrite)) { }} catch(IOException) {refused=true;}
            Check(refused,"validation/packing read lease excludes concurrent rewrites");
        }
        var imagePath=Path.Combine(Path.GetDirectoryName(path),"palette.png"); var png=File.ReadAllBytes(imagePath);
        png[16]=0; png[17]=0; png[18]=32; png[19]=0; File.WriteAllBytes(imagePath,png);
        Reject(()=>VehiclePackage.Load(path),"oversized decoded image rejected before allocation");
    }
    private static void TestPolicy()
    {
        var p=new SessionPolicy(); Check(!p.CanApply(true,true) && p.CanUpload && p.CanUnload,"fresh session defaults to stock physics");
        for(int bits=0;bits<64;bits++)
        {
            p.RuntimeQualified=(bits&1)!=0; p.UploadGuardReady=(bits&2)!=0; p.LocalGuardReady=(bits&4)!=0; p.ExperimentEnabled=(bits&8)!=0;
            Check(p.CanApply((bits&16)!=0,(bits&32)!=0)==(bits==63),"all six activation conditions required");
        }
        p.MarkPhysicsUsed(); p.ExperimentEnabled=false; p.RuntimeQualified=false; p.UploadGuardReady=false; p.LocalGuardReady=false;
        Check(!p.CanUpload && !p.CanUnload && p.PhysicsUsed,"disable/failure cannot clear score taint");
        p.MarkPhysicsUsed(); Check(!p.CanUpload,"repeat stage cannot clear taint");
        Check(new SessionPolicy().CanUpload,"only a new process policy starts clean");
    }
    private static void TestPersistence()
    {
        var path=Path.Combine(temp,"state","selection.json"); var store=new SelectionStore(path);
        store.Set("GROUP_2","test.a"); store.Set("GROUP_3","test.b");
        var loaded=new SelectionStore(path); loaded.Load(); Check(loaded.Get("GROUP_2")=="test.a" && loaded.Get("GROUP_3")=="test.b","stable IDs survive restart");
        loaded.Set("GROUP_2",null); var again=new SelectionStore(path); again.Load(); Check(again.Get("GROUP_2")==null && again.Get("GROUP_3")=="test.b","stock selection clears only its class");
        var selection=new SelectionState(12,3); Check(selection.Choose(12,true)==3 && selection.ToSavedIndex(12)==3,"custom slot never enters native save");
        Check(selection.Choose(12,false)==12 && !selection.Chosen,"class isolation");
        var catalog=new[] {new KeyValuePair<string,SelectionState>("test.a",new SelectionState(12,3)),new KeyValuePair<string,SelectionState>("test.b",new SelectionState(13,3))};
        Check(SelectionResolver.Resolve("test.b",3,catalog)==13,"same-donor packages resolve by stable id");
        var reordered=new[] {new KeyValuePair<string,SelectionState>("test.b",new SelectionState(12,3)),new KeyValuePair<string,SelectionState>("test.a",new SelectionState(13,3))};
        Check(SelectionResolver.Resolve("test.b",3,reordered)==12,"package reorder resolves current slot");
        Check(SelectionResolver.Resolve("test.b",3,catalog.Take(1))==3,"removed or failed preview falls back to donor");
        Check(SelectionResolver.Resolve("test.b",2,catalog)==2,"new native donor selection takes precedence");
        File.WriteAllText(path,"{bad"); Reject(()=>again.Load(),"corrupt selection rejected"); Check(again.Get("GROUP_3")=="test.b","failed read leaves loaded state intact");
    }
    private static void TestJournal()
    {
        for(int fail=0;fail<4;fail++)
        {
            int[] donor={1,2,3}; int[] live=(int[])donor.Clone(); var journal=new MutationJournal(); var order=new List<int>();
            try
            {
                for(int i=0;i<4;i++)
                {
                    int step=i;
                    journal.Change(()=> {live[step%3]=99; if(step==fail) throw new IOException("injected");},()=> {order.Add(step); live[step%3]=donor[step%3];});
                }
            }
            catch(IOException) {journal.Rollback(ex=> {throw ex;});}
            Check(live.SequenceEqual(donor) && donor.SequenceEqual(new[] {1,2,3}),"failed construction restores instance and preserves donor");
            Check(order.SequenceEqual(Enumerable.Range(0,fail+1).Reverse()),"rollback unwinds in ownership order");
            journal.Rollback(ex=> {throw ex;}); Check(order.Count==fail+1,"rollback idempotence");
        }
        int restores=0,errors=0; var j=new MutationJournal(); j.Change(()=>{},()=>restores++); j.Change(()=>{},()=> {throw new Exception("restore fault");});
        j.Rollback(ex=>errors++); Check(restores==1 && errors==1,"one cleanup failure does not leak remaining ownership");
    }
    private static void Check(bool condition,string label) {if(!condition) throw new Exception("FAIL: "+label); assertions++;}
    private static void Reject(Action action,string label)
    {
        try {action();} catch(Exception ex) when(ex is InvalidDataException || ex is JsonException) {assertions++; return;}
        throw new Exception("FAIL: "+label);
    }
}
