using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RallyCustomVehicles
{
    // Only attached to the newly instantiated player car; never to a Resources prefab.
    public sealed class VehicleInstance : MonoBehaviour
    {
        internal VehiclePackage Package;
        internal bool PhysicsApplied;
        private float rootScale;
        private Mesh ownedCollision;
        private MeshCollider bodyCollider;
        private CarDynamics dynamics;
        private Drivetrain drivetrain;
        private Rigidbody body;
        private Axles axles;
        private readonly MutationJournal journal=new MutationJournal();
        private bool faulted;
        private Transform ownedCenter;
        private static readonly FieldInfo RimRadius=AccessTools.Field(typeof(Wheel),"rimRadius");

        internal void Initialize(VehiclePackage package, bool apply)
        {
            Package=package;
            if (!apply) return;
            var scale=transform.lossyScale;
            if (scale.x<=0 || Mathf.Abs(scale.x-scale.y)>.001f || Mathf.Abs(scale.x-scale.z)>.001f)
                throw new InvalidOperationException("Custom physics requires a positive uniform donor root scale.");
            rootScale=scale.x;
            dynamics=GetComponent<CarDynamics>(); drivetrain=GetComponent<Drivetrain>(); body=GetComponent<Rigidbody>(); axles=GetComponent<Axles>();
            if (dynamics==null || drivetrain==null || body==null || axles==null || axles.frontAxle?.leftWheel==null || axles.frontAxle.rightWheel==null || axles.rearAxle?.leftWheel==null || axles.rearAxle.rightWheel==null || axles.otherAxles==null || axles.otherAxles.Length!=0 || drivetrain.CV2KW<=0)
                throw new InvalidOperationException("A complete stock four-wheel rig is required.");
            if(RimRadius==null || RimRadius.FieldType!=typeof(float)) throw new MissingFieldException("Required Wheel.rimRadius cache field is unavailable.");
            if (package.Manifest.physics.collision!=null)
            {
                var wheelRoots=GetComponentsInChildren<Wheel>(true).Select(w=>w.transform).ToArray();
                var candidates=GetComponentsInChildren<MeshCollider>(true).Where(c=>!c.isTrigger && c.attachedRigidbody==body && c.sharedMesh!=null && !wheelRoots.Any(w=>c.transform.IsChildOf(w))).ToArray();
                if(candidates.Length!=1) throw new InvalidOperationException("Collision override requires exactly one non-trigger body mesh attached to this rigidbody outside wheel branches; found "+candidates.Length+".");
                bodyCollider=candidates[0];
                Main.Log("Audited body collision target: "+PhysicsAudit.RelativePath(transform,bodyCollider.transform));
            }
            // The latch is set before the first mutation, including any failure path.
            Main.Policy.MarkPhysicsUsed(); PhysicsApplied=true;
            // Native Axle is mutable managed data. Make its arrays private before
            // changing settings; wheel component references still name this rig.
            var oldFront=axles.frontAxle; var oldRear=axles.rearAxle;
            var newFront=CloneAxle(oldFront); var newRear=CloneAxle(oldRear);
            journal.Change(()=> {axles.frontAxle=newFront; axles.rearAxle=newRear;},()=> {axles.frontAxle=oldFront; axles.rearAxle=oldRear;});
            ApplyDimensions(); ApplyCollision();
            Main.Log("Physical definition activated on new player instance: " + package.Manifest.id + ". " + Main.Policy.Status);
        }

        private void ApplyDimensions()
        {
            var d=Package.Manifest.physics.dimensions; if(d==null) return;
            var wheels=new[] {axles.frontAxle.leftWheel,axles.frontAxle.rightWheel,axles.rearAxle.leftWheel,axles.rearAxle.rightWheel};
            var pos=wheels.Select(w=>transform.InverseTransformPoint(w.transform.position)).ToArray();
            float centerZ=pos.Average(v=>v.z);
            for(int i=0;i<4;i++)
            {
                bool front=i<2; float? track=front?d.frontTrackM:d.rearTrackM;
                var p=pos[i];
                if(track.HasValue) p.x=(pos[front?0:2].x+pos[front?1:3].x)*.5f+(i%2==0?-1:1)*track.Value/(2*rootScale);
                if(d.wheelbaseM.HasValue) p.z=centerZ+(front?1:-1)*d.wheelbaseM.Value/(2*rootScale);
                var wheel=wheels[i]; var position=wheel.transform.localPosition;
                float radius=wheel.radius,width=wheel.width;
                float rim=(float)RimRadius.GetValue(wheel),inertia=wheel.rotationalInertia;
                journal.Change(()=> {
                    wheel.transform.position=transform.TransformPoint(p);
                    if(d.tireRadiusM.HasValue) {
                        wheel.radius=d.tireRadiusM.Value;
                        RimRadius.SetValue(wheel,radius>0 ? rim*wheel.radius/radius : 0f);
                        // Native Start derives the new inertia from final radius/mass.
                        wheel.rotationalInertia=0;
                    }
                    if(d.tireWidthM.HasValue) wheel.width=d.tireWidthM.Value;
                },()=> {if(wheel!=null) {wheel.transform.localPosition=position; wheel.radius=radius; wheel.width=width; wheel.rotationalInertia=inertia; RimRadius.SetValue(wheel,rim);}});
            }
        }
        private void ApplyCollision()
        {
            var c=Package.Manifest.physics.collision; if(c==null) return;
            var collider=bodyCollider;
            var center=V(c.centerM)/rootScale; var half=V(c.sizeM)/(2*rootScale);
            var vertices=new Vector3[8];
            for(int i=0;i<8;i++)
                vertices[i]=collider.transform.InverseTransformPoint(transform.TransformPoint(center+Vector3.Scale(half,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            ownedCollision=new Mesh { name="CustomVehicle_CollisionBox" };
            ownedCollision.vertices=vertices;
            ownedCollision.triangles=new[] {0,2,3,0,3,1,4,5,7,4,7,6,0,1,5,0,5,4,2,6,7,2,7,3,0,4,6,0,6,2,1,3,7,1,7,5};
            ownedCollision.RecalculateBounds(); ownedCollision.RecalculateNormals();
            var oldMesh=collider.sharedMesh; bool oldConvex=collider.convex;
            journal.Change(()=> {collider.sharedMesh=ownedCollision; collider.convex=true;},()=> {if(collider!=null) {collider.sharedMesh=oldMesh; collider.convex=oldConvex;}});
        }
        internal void ApplySetup()
        {
            if(!PhysicsApplied || faulted) return;
            try { ApplySetupCore(); }
            catch(Exception ex) { Abort(ex); }
        }
        private void ApplySetupCore()
        {
            var p=Package.Manifest.physics;
            if(p.massKg.HasValue) { float old=body.mass; journal.Change(()=>body.mass=p.massKg.Value,()=> {if(body!=null) body.mass=old;}); }
            if(p.frontWeightFraction.HasValue) Set(dynamics,"frontRearWeightRepartition",p.frontWeightFraction.Value);
            if(p.inertiaScale.HasValue) Set(dynamics,"inertiaFactor",p.inertiaScale.Value);
            if(p.centerOfMassM!=null)
            {
                // Do not call the game's SetCenterOfMass helper here: it accesses
                // the lazy event manager. Supply the instance marker for native Init.
                if(ownedCenter==null)
                {
                    var marker=new GameObject("CustomVehicle_COG"); marker.transform.SetParent(transform,false);
                    ownedCenter=marker.transform;
                }
                Set(dynamics,"centerOfMass",ownedCenter);
                ownedCenter.position=transform.TransformPoint(V(p.centerOfMassM)/rootScale);
                var old=body.centerOfMass;
                journal.Change(()=>body.centerOfMass=V(p.centerOfMassM)/rootScale,()=> {if(body!=null) body.centerOfMass=old;});
            }
            if(p.engine!=null)
            {
                var e=p.engine;
                // Native maxPower is metric horsepower (CV), not kW; verified
                // Drivetrain.CalcValues multiplies by its own CV2KW conversion.
                Set(drivetrain,"maxPower",e.powerKw/drivetrain.CV2KW);
                Set(drivetrain,"maxPowerRPM",e.peakPowerRpm); Set(drivetrain,"maxTorque",e.torqueNm); Set(drivetrain,"maxTorqueRPM",e.peakTorqueRpm);
                Set(drivetrain,"minRPM",e.idleRpm); Set(drivetrain,"maxRPM",e.limitRpm);
                Set(drivetrain,"engineTorqueFromFile",false); Set(drivetrain,"torqueRPMValuesLen",0);
            }
            if(p.gearbox!=null)
            {
                Set(drivetrain,"gearRatios",new[] {p.gearbox.reverseRatio,0f}.Concat(p.gearbox.forwardRatios).ToArray());
                Set(drivetrain,"finalDriveRatio",p.gearbox.finalDrive);
            }
            ApplyAxle(axles.frontAxle,p.frontAxle); ApplyAxle(axles.rearAxle,p.rearAxle);
        }
        private static void ApplyAxle(Axle target, AxleDefinition p)
        {
            if(p==null) return;
            if(p.travelM.HasValue) target.suspensionTravel=p.travelM.Value;
            if(p.springRateNPerM.HasValue) target.suspensionRate=p.springRateNPerM.Value;
            if(p.bumpDampingNsPerM.HasValue) target.bumpRate=p.bumpDampingNsPerM.Value;
            if(p.reboundDampingNsPerM.HasValue) target.reboundRate=p.reboundDampingNsPerM.Value;
            if(p.brakeTorqueNm.HasValue) target.brakeFrictionTorque=p.brakeTorqueNm.Value;
        }
        private static Vector3 V(float[] p)=>new Vector3(p[0],p[1],p[2]);
        private static Axle CloneAxle(Axle source)
        {
            var result=new Axle();
            foreach(var field in typeof(Axle).GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                var value=field.GetValue(source);
                field.SetValue(result,value is Array array ? array.Clone() : value);
            }
            return result;
        }
        private void Set(object target,string name,object value)
        {
            var field=target.GetType().GetField(name) ?? throw new MissingFieldException(target.GetType().Name,name);
            object old=field.GetValue(target);
            journal.Change(()=>field.SetValue(target,value),()=>field.SetValue(target,old));
        }
        internal void Abort(Exception error)
        {
            faulted=true; Main.LogError(error);
            journal.Rollback(Main.LogError); PhysicsApplied=false;
            if(ownedCenter!=null) UnityEngine.Object.Destroy(ownedCenter.gameObject);
            if(ownedCollision!=null) UnityEngine.Object.Destroy(ownedCollision);
            // Derived native caches cannot be certified after a partial failure.
            // Remove this instance from simulation; require recreation, never
            // continue driving a mix of stock and overridden solver state.
            if(body!=null) body.isKinematic=true;
            gameObject.SetActive(false);
            Main.Log("Custom vehicle construction failed; instance disabled. Restart the event. Result guard remains latched.");
        }
        private void OnDestroy() { if(ownedCollision!=null) UnityEngine.Object.Destroy(ownedCollision); }

        [HarmonyPatch(typeof(PlayerManager),"CreateCar")]
        private static class SpawnPatch
        {
            [HarmonyPostfix] private static void After(string carPrefabName, GameObject __result)
            {
                var package=Catalog.Selected;
                if(!Main.Enabled || __result==null) return;
                PhysicsAudit audit=null;
                if(Main.AuditEnabled) {audit=__result.AddComponent<PhysicsAudit>(); audit.Donor=carPrefabName;}
                if(package==null || package.Manifest.donorPrefab!=carPrefabName) {audit?.Capture("00-spawn"); return;}
                var instance=__result.AddComponent<VehicleInstance>();
                Main.Policy.UploadGuardReady=UploadGuard.VerifyInstalled("ArtOfSimRally.CustomVehicles");
                Main.Policy.LocalGuardReady=LocalResultGuard.VerifyInstalled("ArtOfSimRally.CustomVehicles");
                bool apply=Main.Policy.CanApply(GameModeManager.GameMode==GameModeManager.GAME_MODES.FREEROAM,package.Manifest.physics!=null);
                try { instance.Initialize(package,apply); audit?.Capture("00-spawn"); }
                catch(Exception ex)
                {
                    // A failed early modification is not allowed to start driving.
                    // Retain the score latch and freeze this instance for diagnosis.
                    instance.Abort(ex);
                }
            }
        }
        [HarmonyPatch(typeof(Setup),"LoadSetup")]
        private static class SetupPatch
        {
            [HarmonyPostfix] private static void After(Setup __instance) { __instance.GetComponent<VehicleInstance>()?.ApplySetup(); __instance.GetComponent<PhysicsAudit>()?.Capture("10-setup"); }
        }
        [HarmonyPatch(typeof(CarDynamics),"Init")]
        private static class DynamicsPatch
        {
            [HarmonyPrefix] private static void Before(CarDynamics __instance)=>__instance.GetComponent<VehicleInstance>()?.ApplySetup();
            [HarmonyPostfix] private static void After(CarDynamics __instance)=>__instance.GetComponent<PhysicsAudit>()?.Capture("30-dynamics");
        }
        [HarmonyPatch(typeof(Drivetrain),"Init")]
        private static class EnginePatch
        {
            [HarmonyPrefix] private static void Before(Drivetrain __instance)=>__instance.GetComponent<VehicleInstance>()?.ApplySetup();
            [HarmonyPostfix] private static void After(Drivetrain __instance)=>__instance.GetComponent<PhysicsAudit>()?.Capture("40-drivetrain");
        }
        [HarmonyPatch(typeof(Wheel),"Start")]
        private static class WheelAuditPatch
        {
            [HarmonyPostfix] private static void After(Wheel __instance)=>__instance.GetComponentInParent<PhysicsAudit>()?.Capture("20-wheel-"+__instance.wheelPos);
        }
    }
}
