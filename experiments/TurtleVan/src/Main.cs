using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityModManagerNet;

namespace TurtleVan
{
    // Artist mesh data only. No game objects, code or physics are serialized.
    [Serializable] public class Asset { public int version; public float[] camera; public Origin[] origins; public MaterialInfo[] materials; public Part[] parts; }
    [Serializable] public class Origin { public string name; public float[] position; }
    [Serializable] public class MaterialInfo { public string name; public float[] color; }
    [Serializable] public class Part { public string group, material; public float[] vertices, normals, uv; public int[] triangles; }

    public static class Main
    {
        private const string Id = "ArtOfSimRally.TurtleVan";
        private static UnityModManager.ModEntry entry;
        private static Harmony harmony;
        private static bool enabled, armed, failed;
        private static Asset asset;
        private static GameObject visual;
        private static CarDynamics car;
        private static Transform steering;
        private static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        private static readonly Dictionary<string, Wheel> wheels = new Dictionary<string, Wheel>();
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
        private static string status = "Prototype ready. Enable the van, then enter a stage or free roam.";

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
            try
            {
                asset = JsonUtility.FromJson<Asset>(File.ReadAllText(Path.Combine(entry.Path, "turtle-van.json")));
                if (asset == null || asset.version != 1 || asset.parts == null || asset.parts.Length == 0) throw new InvalidDataException("Missing model data.");
                harmony = new Harmony(Id); harmony.PatchAll(Assembly.GetExecutingAssembly());
                entry.OnToggle = (m, value) => { enabled = value; if (!value) Detach(); return true; };
                entry.OnGUI = Draw;
                entry.OnUpdate = Update;
                entry.OnUnload = m => { Detach(); harmony.UnpatchAll(Id); return true; };
                return true;
            }
            catch (Exception ex) { harmony?.UnpatchAll(Id); entry.Logger.Error(ex.ToString()); return false; }
        }

        private static void Draw(UnityModManager.ModEntry mod)
        {
            GUILayout.Label("Turtle Van — experimental visual body and cockpit");
            GUILayout.Label("Uses the selected car's handling and collision shape. Pause before changing the model.");
            bool next = GUILayout.Toggle(armed, "Enable Turtle Van for this session");
            if (next != armed) { armed = next; failed = false; if (!armed) Detach(); }
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
            if (!enabled || !armed || failed) return;
            try
            {
                var m = Manager;
                var active = m?.playerManager?.carDynamics;
                if (active != car || (visual != null && active == null)) Detach();
                if (active == null || m.IsRestartingStage()) { ReleaseCamera(false); return; }
                if (visual == null && PlayerView) Attach(active);
                if (!PlayerView || rig == null || !rig.enabled) ReleaseCamera(false);
            }
            catch (Exception ex) { failed = true; Detach(); status = "Stopped after an error; see the UMM log. Toggle off/on to retry."; entry.Logger.Error(ex.ToString()); }
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
            float width = Mathf.Abs(positions["wheelFL"].x - positions["wheelFR"].x);
            float length = Mathf.Abs(positions["wheelFL"].z - positions["wheelRL"].z);
            float sx = width / 1.94f, sz = length / 2.51f, sy = (sx+sz)*.5f;
            if (sx < .3f || sx > 3 || sz < .3f || sz > 3) throw new InvalidDataException("Unexpected donor dimensions.");
            var center = (positions["wheelFL"]+positions["wheelFR"]+positions["wheelRL"]+positions["wheelRR"])*.25f;
            visual = new GameObject("TurtleVan_VisualOnly"); visual.SetActive(false);
            visual.transform.SetParent(car.transform, false);
            visual.transform.localPosition = center - new Vector3(0,.5f*sy,-.005f*sz);
            visual.transform.localScale = new Vector3(sx,sy,sz);

            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false); resources.Add(texture);
            if (!ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.Combine(entry.Path,"palette.png")))) throw new InvalidDataException("Palette could not be loaded.");
            texture.filterMode=FilterMode.Point;
            var shader=Shader.Find("Standard");
            if (shader == null) throw new InvalidDataException("Standard shader unavailable in this build.");
            var materials=new Dictionary<string,Material>();
            foreach (var info in asset.materials)
            {
                var mat=new Material(shader) { name="TurtleVan_"+info.name, mainTexture=texture, color=Color.white };
                resources.Add(mat); mat.SetFloat("_Glossiness",.18f);
                if (info.name=="glass")
                {
                    mat.color=new Color(1,1,1,.12f); mat.SetFloat("_Mode",3); mat.SetInt("_SrcBlend",(int)BlendMode.One);
                    mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite",0);
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.renderQueue=3000;
                }
                materials[info.name]=mat;
            }
            foreach (var origin in asset.origins)
            {
                var t=new GameObject(origin.name).transform; t.SetParent(visual.transform,false); t.localPosition=V(origin.position); groups[origin.name]=t;
            }
            foreach (var part in asset.parts)
            {
                var obj=new GameObject(part.group+"_"+part.material); obj.layer=car.gameObject.layer; obj.transform.SetParent(groups[part.group],false);
                var mesh=new Mesh { name=obj.name, indexFormat=IndexFormat.UInt32 }; resources.Add(mesh);
                mesh.vertices=Vectors(part.vertices); mesh.normals=Vectors(part.normals);
                var uv=new Vector2[part.uv.Length/2]; for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(part.uv[i*2],part.uv[i*2+1]);
                mesh.uv=uv; mesh.triangles=part.triangles; mesh.RecalculateBounds();
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=materials[part.material];
                if (part.material=="glass") renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            steering=groups["steering"];
            // Hide renderers only. Native physics, wheel transforms, collider objects,
            // particles, damage scripts and native meshes remain alive and unchanged.
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(visual.transform) || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) || r.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0) continue;
                hidden[r]=r.forceRenderingOff; r.forceRenderingOff=true;
            }
            visual.SetActive(true);
            status="Van attached to "+car.name+". Cycle camera views for the cockpit.";
            entry.Logger.Log(status+" Body scale "+visual.transform.localScale+"; donor colliders unchanged.");
        }

        private static void Animate()
        {
            foreach(var pair in wheels)
            {
                var w=pair.Value; if(w==null || w.modelTransform==null) continue;
                var t=groups[pair.Key]; t.position=w.modelTransform.position; t.rotation=w.modelTransform.rotation;
                // World size follows native tyre radius; never move the native wheel.
                float r=w.radius/.49f;
                var scale=visual.transform.lossyScale;
                float worldScale=car.transform.lossyScale.y;
                t.localScale=new Vector3(r*worldScale/scale.x,r*worldScale/scale.y,r*worldScale/scale.z);
            }
            if(steering!=null) steering.localRotation=Quaternion.Euler(0,0,-wheels["wheelFL"].steering*360);
        }
        private static void Drive(CarCameras current)
        {
            if(!enabled || !armed || visual==null || car==null) return;
            try
            {
                // A rig aimed at a different car is never ours to modify.
                if(current.target==null || (current.target!=car.transform && !current.target.IsChildOf(car.transform))) return;
                Animate();
                if(rig!=current)
                {
                    RemoveAngle(); rig=current;
                    cockpit=new CameraAngle(0,0,0,CameraAngle.CameraAngles.CAMERA1); Angles(rig).Add(cockpit);
                    entry.Logger.Log("Turtle Van cockpit added to camera cycle.");
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
            resources.Clear(); groups.Clear(); wheels.Clear(); visual=null; car=null; steering=null;
            status="Van off. Original car visuals restored.";
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
