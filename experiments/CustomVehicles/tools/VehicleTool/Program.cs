using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using RallyCustomVehicles;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length<2 || !new[] {"validate","inspect","pack"}.Contains(args[0])) throw new ArgumentException("Usage: VehicleTool validate|inspect vehicle.json OR pack vehicle.json output.zip");
            var p=VehiclePackage.Load(args[1]);
            if(args[0]=="pack")
            {
                using(p.HoldFiles())
                {
                if(VehiclePackage.Load(args[1]).ContentHash!=p.ContentHash) throw new IOException("Package changed during packing; retry with the editor closed.");
                if(args.Length!=3 || Path.GetExtension(args[2])!=".zip") throw new ArgumentException("Specify a new output.zip file.");
                var files=new[] {"vehicle.json",p.Manifest.model,p.Manifest.texture,p.Manifest.preview}.Where(f=>f!=null).Distinct().ToArray();
                string temporary=Path.GetFullPath(args[2])+".partial";
                if(File.Exists(args[2]) || File.Exists(temporary)) throw new IOException("Output exists; choose a new archive name.");
                try
                {
                    using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))
                        foreach(string file in files)
                        {
                            string path=file=="vehicle.json" ? args[1] : VehiclePackage.LocalFile(p.Folder,file);
                            zip.CreateEntryFromFile(path,p.Manifest.id+"/"+file,CompressionLevel.Optimal);
                        }
                    File.Move(temporary,args[2]);
                }
                catch { if(File.Exists(temporary)) File.Delete(temporary); throw; }
                Console.WriteLine("Packed data-only vehicle: "+Path.GetFullPath(args[2]));
                }
            }
            Console.WriteLine(JsonConvert.SerializeObject(new {identity=VehicleIdentity.From(p), triangles=p.TriangleCount, parts=p.Model.parts.Length, decodedTextureBytes=p.TextureBytes, sourceBytes=p.SourceBytes},Formatting.Indented));
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("INVALID: "+ex.Message); return 1; }
    }
}
