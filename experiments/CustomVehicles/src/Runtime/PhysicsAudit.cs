using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using HarmonyLib;
using UnityEngine;

namespace RallyCustomVehicles
{
    // Opt-in, mod-owned diagnostics. This is evidence collection, not an
    // automatic qualification or a recorder/replay integration claim.
    public sealed class PhysicsAudit : MonoBehaviour
    {
        private readonly SortedDictionary<string,object> phases=new SortedDictionary<string,object>();
        private static readonly string GameHash=AssemblyHash();
        internal string Donor;
        internal void Capture(string phase)
        {
            try
            {
                var body=GetComponent<Rigidbody>(); var d=GetComponent<Drivetrain>(); var c=GetComponent<CarDynamics>(); var a=GetComponent<Axles>();
                if(body==null || d==null || c==null || a==null) return;
                phases[phase]=new {
                    massKg=body.mass, centerOfMass=V(body.centerOfMass), inertiaTensor=V(body.inertiaTensor), c.inertiaFactor, c.frontRearWeightRepartition,
                    powerKw=d.maxPower*d.CV2KW, d.maxPowerRPM, d.maxTorque, d.maxTorqueRPM, d.minRPM, d.maxRPM, d.engineTorqueFromFile, d.torqueRPMValuesLen,
                    gearRatios=d.gearRatios?.ToArray(), d.finalDriveRatio,
                    drivetrainCache=new {powerKw=FieldFloat(d,"maxPowerKW"),powerAngularVelocity=FieldFloat(d,"maxPowerAngVel"),p1=FieldFloat(d,"P1"),p2=FieldFloat(d,"P2"),d.clutchMaxTorque,d.idlethrottle,d.lastGearRatio},
                    front=AxleData(a.frontAxle), rear=AxleData(a.rearAxle),
                    meshColliders=GetComponentsInChildren<MeshCollider>(true).OrderBy(x=>RelativePath(transform,x.transform)).Select(x=>new {
                        path=RelativePath(transform,x.transform), attachedToRoot=x.attachedRigidbody==body, x.convex,x.isTrigger,x.enabled,
                        mesh=x.sharedMesh?.name, vertexCount=x.sharedMesh?.vertexCount,
                        localBounds=x.sharedMesh==null ? null : new {center=V(x.sharedMesh.bounds.center),size=V(x.sharedMesh.bounds.size)}
                    }).ToArray(),
                    wheels=GetComponentsInChildren<Wheel>(true).OrderBy(w=>w.wheelPos.ToString()).Select(w=>new {
                        position=w.wheelPos.ToString(), mount=V(transform.InverseTransformPoint(w.transform.position)),
                        w.radius, w.width, w.mass, rimRadius=FieldFloat(w,"rimRadius"),w.rotationalInertia,w.totalRotationalInertia,
                        w.suspensionTravel, w.suspensionRate, w.bumpRate, w.reboundRate, w.brakeFrictionTorque
                    }).ToArray()
                };
                var instance=GetComponent<VehicleInstance>();
                var data=new { schemaVersion=1, gameAssemblySha256=GameHash, utc=DateTime.UtcNow.ToString("O"), scene=gameObject.scene.name, instance=GetInstanceID(), donorPrefab=Donor,
                    identity=instance?.Package==null ? null : VehicleIdentity.From(instance.Package,instance.PhysicsApplied), phases };
                Directory.CreateDirectory(Main.StateDirectory);
                // Separate stock/custom reports allow a no-op comparison without
                // overwriting one side when switching vehicles in the same session.
                string path=Path.Combine(Main.StateDirectory,instance?.Package==null ? "donor-stock.json" : "donor-custom.json");
                string tmp=path+".tmp"; File.WriteAllText(tmp,JsonConvert.SerializeObject(data,Formatting.Indented));
                if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
            }
            catch(Exception ex) { Main.LogError(ex); }
        }
        private static float[] V(Vector3 v)=>new[] {v.x,v.y,v.z};
        private static float FieldFloat(object target,string name)=>(float)(AccessTools.Field(target.GetType(),name) ?? throw new MissingFieldException(target.GetType().Name,name)).GetValue(target);
        internal static string RelativePath(Transform root,Transform child)
        {
            var names=new Stack<string>();
            for(var current=child; current!=null && current!=root; current=current.parent) names.Push(current.name);
            return string.Join("/",names);
        }
        private static string AssemblyHash()
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
            using(var stream=File.OpenRead(typeof(PlayerManager).Assembly.Location))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        private static object AxleData(Axle a)=>a==null ? null : new {a.suspensionTravel,a.suspensionRate,a.bumpRate,a.reboundRate,a.brakeFrictionTorque};
    }
}
