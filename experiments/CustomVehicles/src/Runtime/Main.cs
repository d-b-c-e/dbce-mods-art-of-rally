using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityModManagerNet;

namespace RallyCustomVehicles
{
    public static class Main
    {
        private const string Id = "ArtOfSimRally.CustomVehicles";
        private static UnityModManager.ModEntry entry;
        private static Harmony harmony;
        private static bool enabled, failed;
        internal static readonly SessionPolicy Policy = new SessionPolicy();
        internal static SelectionStore Selections;
        internal static bool AuditEnabled;
        internal static string StateDirectory => Path.Combine(entry.Path,"State");
        internal static bool Enabled => enabled;
        internal static void Log(string message) => entry?.Logger.Log(message);
        internal static void LogError(Exception error) => entry?.Logger.Error(error.ToString());
        private static Asset asset;
        internal static List<VehiclePackage> Packages { get; private set; }
        private static VehiclePackage activePackage;
        private static GameObject visual;
        private static CarDynamics car;
        private static Transform steering;
        private static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, Wheel> wheels = new Dictionary<string, Wheel>();
        private static readonly Dictionary<string, Vector3> wheelOffsets = new Dictionary<string, Vector3>();
        private static readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        private static readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        private static readonly FieldInfo ManagerField = typeof(GameEntryPoint).GetField("eventManager", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly AccessTools.FieldRef<CarCameras, List<CameraAngle>> Angles = AccessTools.FieldRefAccess<CarCameras, List<CameraAngle>>("CameraAnglesList");
        private static CameraAngle cockpit;
        private static CarCameras rig;
        private static Camera camera;
        private static float savedNear, savedFov;
        private static Vector3 savedPosition;
        private static Quaternion savedRotation;
        private static Vector3 eyeAdjustment;
        private static float pitch = 6f, fov = 65f;
        private static string status = "Select an installed custom vehicle in the car chooser.";

        private static EventManager Manager => ManagerField?.GetValue(null) as EventManager;
        private static bool PlayerView
        {
            get
            {
                var m = Manager;
                return m != null && !m.IsRestartingStage() && (m.status == EventStatusEnums.EventStatus.UNDERWAY ||
                    m.status == EventStatusEnums.EventStatus.WAITING_TO_BEGIN || m.status == EventStatusEnums.EventStatus.PAUSED);
            }
        }

        public static bool Load(UnityModManager.ModEntry mod)
        {
            entry = mod;
            Selections = new SelectionStore(Path.Combine(entry.Path, "State", "selections.json"));
            try { Selections.Load(); } catch (Exception ex) { Log("Custom selection history ignored: " + ex.Message); }
            try
            {
                Packages = VehiclePackage.Discover(Path.Combine(entry.Path,"Vehicles"), message => entry.Logger.Error(message));
                if (Packages.Count > 0) asset = Packages[0].Model;
                Log("Loaded " + Packages.Count + " vehicle package(s).");
                if (UnityModManager.modEntries.Any(m => m.Info.Id == "ArtOfSimRally.TurtleVan" && (m.Active || m.Enabled)))
                    throw new InvalidOperationException("Disable the cosmetic Turtle Van plugin before loading the framework; both manage car previews and cameras.");
                harmony = new Harmony(Id); harmony.PatchAll(Assembly.GetExecutingAssembly());
                Policy.UploadGuardReady = UploadGuard.VerifyInstalled(harmony.Id);
                Policy.LocalGuardReady = LocalResultGuard.VerifyInstalled(harmony.Id);
                // Remains false in this offline candidate. Qualify a no-op donor
                // clone and result guards in an attended session before unlocking
                // physical writes. Successful compilation is not that evidence.
                Policy.RuntimeQualified = false;
                entry.OnToggle = (m, value) =>
                {
                    // Menu buttons cache array lengths. Removing a registered slot mid-menu is unsafe.
                    if (!value && (Catalog.Registered || !Policy.CanUnload)) { Log("Restart the game to unload registered custom vehicles; choose a stock car to stop using custom visuals."); return false; }
                    enabled = value; if (!value) Detach(); return true;
                };
                entry.OnGUI = Draw;
                entry.OnUpdate = Update;
                entry.OnLateUpdate = (m, dt) =>
                {
                    if (visual == null || car == null) return;
                    try { Animate(); } catch (Exception ex) { failed = true; Detach(); LogError(ex); }
                };
                entry.OnUnload = m => { if (Catalog.Registered || !Policy.CanUnload) return false; Detach(); harmony.UnpatchAll(Id); return true; };
                return true;
            }
            catch (Exception ex) { harmony?.UnpatchAll(Id); entry.Logger.Error(ex.ToString()); return false; }
        }

        private static void Draw(UnityModManager.ModEntry mod)
        {
            GUILayout.Label("Custom Vehicles - development preview");
            GUILayout.Label(Packages.Count + " validated packages. Select a vehicle in its donor's class.");
            GUILayout.Label("Install/remove vehicle folders while the game is closed. Appearance uses its package texture.");
            foreach(var package in Packages)
                GUILayout.Label(package.Manifest.name+" "+package.Manifest.version+" by "+package.Manifest.author+" — "+package.Manifest.donorPrefab+" handling"+(package.Manifest.physics==null ? "" : " (custom physical definition staged)"));
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && Policy.RuntimeQualified && Policy.UploadGuardReady && Policy.LocalGuardReady;
            bool physics = GUILayout.Toggle(Policy.ExperimentEnabled, "Experimental physical definitions in Free Roam (next vehicle spawn)");
            GUI.enabled = wasEnabled;
            Policy.ExperimentEnabled = physics && Policy.RuntimeQualified && Policy.UploadGuardReady && Policy.LocalGuardReady;
            if (!Policy.RuntimeQualified) GUILayout.Label("Physics is locked in this candidate pending donor-clone and save-guard runtime qualification.");
            GUILayout.Label(Policy.Status);
            AuditEnabled=GUILayout.Toggle(AuditEnabled,"Record donor initialization diagnostics for the next vehicle spawn");
            if (!Policy.UploadGuardReady || !Policy.LocalGuardReady) GUILayout.Label("Physics unavailable: required result guards are missing.");
            if (GUILayout.Button("Retry visual attachment")) { failed = false; Detach(); }
            GUILayout.Label(status);
            GUILayout.Label("Cycle the game's camera views to reach the cockpit (added after the existing views).");
            GUILayout.Label("Driver eye position (metres):");
            Row("Left / right", ref eyeAdjustment.x, .02f);
            Row("Up / down", ref eyeAdjustment.y, .02f);
            Row("Forward / back", ref eyeAdjustment.z, .02f);
            Row("Look down (degrees)", ref pitch, 1f);
            Row("Vertical field of view", ref fov, 2f);
            pitch = Mathf.Clamp(pitch, -25, 35); fov = Mathf.Clamp(fov, 35, 95);
            eyeAdjustment.x = Mathf.Clamp(eyeAdjustment.x, -.4f, .4f);
            eyeAdjustment.y = Mathf.Clamp(eyeAdjustment.y, -.3f, .3f);
            eyeAdjustment.z = Mathf.Clamp(eyeAdjustment.z, -.4f, .4f);
            if (GUILayout.Button("Reset driver view")) { eyeAdjustment = Vector3.zero; pitch = 6; fov = 65; }
        }
        private static void Row(string label, ref float value, float step)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label + ": " + value.ToString("0.00"), GUILayout.Width(240));
            if (GUILayout.Button("−", GUILayout.Width(42))) value -= step;
            if (GUILayout.Button("+", GUILayout.Width(42))) value += step;
            GUILayout.EndHorizontal();
        }

        private static void Update(UnityModManager.ModEntry mod, float dt)
        {
            if (!enabled || failed) return;
            try
            {
                Catalog.DiscoverExistingChooser();
                var m = Manager;
                var active = m?.playerManager?.carDynamics;
                // Selection belongs to the spawned player instance, not whichever
                // menu choice happens to be highlighted during a later transition.
                var chosen = active?.GetComponent<VehicleInstance>()?.Package;
                if (chosen == null) { if (visual != null) Detach(); return; }
                if (activePackage != chosen) { Detach(); activePackage = chosen; asset = chosen.Model; }
                if (active != car || (visual != null && active == null)) { Detach(); activePackage = chosen; asset = chosen.Model; }
                if (active == null || m.IsRestartingStage()) { ReleaseCamera(false); return; }
                if (visual == null) Attach(active); // Include intro/finish cinematics once native wheels are ready.
                if (!PlayerView || rig == null || !rig.enabled) ReleaseCamera(false);
            }
            catch (Exception ex) { failed = true; Detach(); status = "Stopped after an error; see the UMM log. Use Retry visual attachment to retry."; entry.Logger.Error(ex.ToString()); }
        }

        private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        private static Vector3[] Vectors(float[] a)
        {
            var result = new Vector3[a.Length / 3];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(a[i*3], a[i*3+1], a[i*3+2]);
            return result;
        }
        private static void Attach(CarDynamics target)
        {
            var found = target.GetComponentsInChildren<Wheel>(true);
            foreach (var w in found)
            {
                if (w.modelTransform == null) return; // Native Wheel.Start has not finished yet.
                string pos = w.wheelPos.ToString();
                string key = pos == "FRONT_LEFT" ? "wheelFL" : pos == "FRONT_RIGHT" ? "wheelFR" : pos == "REAR_LEFT" ? "wheelRL" : pos == "REAR_RIGHT" ? "wheelRR" : null;
                if (key != null) wheels[key] = w;
            }
            if (wheels.Count != 4) throw new InvalidDataException("Donor must have four named wheels.");
            car = target;
            var positions = wheels.ToDictionary(p => p.Key, p => car.transform.InverseTransformPoint(p.Value.modelTransform.position));
            float length = Mathf.Abs(positions["wheelFL"].z - positions["wheelRL"].z);
            var authored = asset.origins.ToDictionary(o => o.name, o => V(o.position));
            float sz = length / Mathf.Abs(authored["wheelFL"].z - authored["wheelRL"].z);
            // Preserve the artist's proportions across donors. Scale the entire
            // body uniformly from wheelbase; never squeeze it to the wheel track.
            // Native wheel positions/radii and colliders remain the donor's.
            float sx = sz, sy = sz;
            if (sx < .3f || sx > 3 || sz < .3f || sz > 3) throw new InvalidDataException("Unexpected donor dimensions.");
            var center = (positions["wheelFL"]+positions["wheelFR"]+positions["wheelRL"]+positions["wheelRR"])*.25f;
            visual = new GameObject("CustomVehicle_VisualOnly"); visual.SetActive(false);
            visual.transform.SetParent(car.transform, false);
            var authoredCenter = (authored["wheelFL"] + authored["wheelFR"] + authored["wheelRL"] + authored["wheelRR"]) * .25f;
            visual.transform.localPosition = center - authoredCenter * sz;
            visual.transform.localScale = new Vector3(sx,sy,sz);

            PopulateModel(activePackage, visual, car.gameObject.layer, groups, resources);
            foreach (var pair in wheels)
            {
                float authoredX = car.transform.InverseTransformPoint(groups[pair.Key].position).x;
                wheelOffsets[pair.Key] = target.GetComponent<VehicleInstance>()?.PhysicsApplied == true ? Vector3.zero : new Vector3(authoredX - positions[pair.Key].x, 0, 0);
            }
            steering=groups["steering"];
            // Hide renderers only. Native physics, wheel transforms, collider objects,
            // particles, damage scripts and native meshes remain alive and unchanged.
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(visual.transform) || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) || r.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0) continue;
                hidden[r]=r.forceRenderingOff; r.forceRenderingOff=true;
            }
            Animate();
            visual.SetActive(true);
            status=activePackage.Manifest.name+" attached to "+car.name+". Cycle camera views for the cockpit.";
            entry.Logger.Log(status+" Body scale "+visual.transform.localScale+"; donor colliders unchanged.");
        }

        internal static void PopulateModel(VehiclePackage package, GameObject root, int layer, Dictionary<string, Transform> groups, List<UnityEngine.Object> resources)
        {
            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false); resources.Add(texture);
            if (!ImageConversion.LoadImage(texture,package.TextureData)) throw new InvalidDataException("Palette could not be loaded.");
            texture.filterMode=FilterMode.Point;
            var shader=Shader.Find("Standard");
            if (shader == null) throw new InvalidDataException("Standard shader unavailable in this build.");
            var materials=new Dictionary<string,Material>();
            foreach (var info in package.Model.materials)
            {
                var mat=new Material(shader) { name="CustomVehicle_"+info.name, mainTexture=texture, color=Color.white };
                resources.Add(mat); mat.SetFloat("_Glossiness",.18f);
                if (info.name=="glass")
                {
                    mat.color=new Color(1,1,1,.12f); mat.SetFloat("_Mode",3); mat.SetInt("_SrcBlend",(int)BlendMode.One);
                    mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite",0);
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.renderQueue=3000;
                }
                materials[info.name]=mat;
            }
            foreach (var origin in package.Model.origins)
            {
                var t=new GameObject(origin.name).transform; t.SetParent(root.transform,false); t.localPosition=V(origin.position); groups[origin.name]=t;
            }
            foreach (var part in package.Model.parts)
            {
                var obj=new GameObject(part.group+"_"+part.material); obj.layer=layer; obj.transform.SetParent(groups[part.group],false);
                var mesh=new Mesh { name=obj.name, indexFormat=IndexFormat.UInt32 }; resources.Add(mesh);
                mesh.vertices=Vectors(part.vertices); mesh.normals=Vectors(part.normals);
                var uv=new Vector2[part.uv.Length/2]; for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);
                mesh.uv=uv; mesh.triangles=part.triangles; mesh.RecalculateBounds();
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=materials[part.material];
                if (part.material=="glass") renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        private static void Animate()
        {
            foreach(var pair in wheels)
            {
                var w=pair.Value; if(w==null || w.modelTransform==null) continue;
                var t=groups[pair.Key];
                // Move only the drawn wheel to the authored track. Offset in car
                // space, never spinning wheel space; keep native suspension/steer.
                t.position=w.modelTransform.position + car.transform.TransformVector(wheelOffsets[pair.Key]);
                t.rotation=w.modelTransform.rotation;
                // World size follows native tyre radius; never move the native wheel.
                float r=w.radius/activePackage.Manifest.wheelRadius;
                var scale=visual.transform.lossyScale;
                float worldScale=car.GetComponent<VehicleInstance>()?.PhysicsApplied==true ? 1f : car.transform.lossyScale.y;
                t.localScale=new Vector3(r*worldScale/scale.x,r*worldScale/scale.y,r*worldScale/scale.z);
            }
            if(steering!=null) steering.localRotation=Quaternion.Euler(0,0,-wheels["wheelFL"].steering*360);
        }
        private static void Drive(CarCameras current)
        {
            if(!enabled || visual==null || car==null) return;
            try
            {
                // A rig aimed at a different car is never ours to modify.
                if(current.target==null || (current.target!=car.transform && !current.target.IsChildOf(car.transform))) return;
                if (!activePackage.Manifest.cockpit) return;
                if(rig!=current)
                {
                    RemoveAngle(); rig=current;
                    cockpit=new CameraAngle(0,0,0,CameraAngle.CameraAngles.CAMERA1); Angles(rig).Add(cockpit);
                    entry.Logger.Log("Custom vehicle cockpit added to camera cycle.");
                }
                if(!PlayerView || !ReferenceEquals(current.CurrentCameraAngle,cockpit)) { ReleaseCamera(true); return; }
                var cam=UIManager.Instance?.PanelManager?.mainCamera; if(cam==null) return;
                if(camera!=cam)
                {
                    ReleaseCamera(false); camera=cam; savedNear=cam.nearClipPlane; savedFov=cam.fieldOfView;
                    savedPosition=cam.transform.localPosition; savedRotation=cam.transform.localRotation;
                }
                cam.transform.position=visual.transform.TransformPoint(V(asset.camera)+eyeAdjustment);
                cam.transform.rotation=car.transform.rotation*Quaternion.Euler(pitch,0,0);
                cam.nearClipPlane=.025f; cam.fieldOfView=fov;
            }
            catch(Exception ex) { failed=true; Detach(); status="Camera/model stopped after error; see UMM log."; entry.Logger.Error(ex.ToString()); }
        }
        private static void ReleaseCamera(bool snap)
        {
            if(camera==null) return;
            camera.transform.localPosition=savedPosition; camera.transform.localRotation=savedRotation;
            camera.nearClipPlane=savedNear; camera.fieldOfView=savedFov; camera=null;
            if(snap && rig!=null && rig.enabled && PlayerView && !ReferenceEquals(rig.CurrentCameraAngle,cockpit)) rig.SetToWantedPositionImmediate();
        }
        private static void RemoveAngle()
        {
            ReleaseCamera(false);
            if(rig!=null && cockpit!=null)
            {
                bool active=ReferenceEquals(rig.CurrentCameraAngle,cockpit);
                var list=Angles(rig); list?.Remove(cockpit);
                if(active && list!=null && list.Count>0) rig.RefreshCameraType();
            }
            rig=null; cockpit=null;
        }
        private static void Detach()
        {
            RemoveAngle();
            foreach(var item in hidden) if(item.Key!=null) item.Key.forceRenderingOff=item.Value;
            hidden.Clear();
            if(visual!=null) { visual.SetActive(false); UnityEngine.Object.Destroy(visual); }
            foreach(var resource in resources) if(resource!=null) UnityEngine.Object.Destroy(resource);
            resources.Clear(); groups.Clear(); wheels.Clear(); wheelOffsets.Clear(); visual=null; car=null; steering=null; activePackage=null;
            status="Custom body inactive. Stock visuals restored.";
        }
        [HarmonyPatch(typeof(CarCameras),"LateUpdate")]
        private static class CameraPatch
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyAfter("ArtOfSimRally")]
            private static void After(CarCameras __instance) => Drive(__instance);
        }
        [HarmonyPatch(typeof(CameraManager),"EnableCinemachineCamera")]
        private static class CinematicPatch { [HarmonyPrefix] private static void Before() => ReleaseCamera(false); }
        [HarmonyPatch(typeof(CameraManager),"DisableCameraManagers")]
        private static class IntroPatch { [HarmonyPrefix] private static void Before() => ReleaseCamera(false); }
    }
}
