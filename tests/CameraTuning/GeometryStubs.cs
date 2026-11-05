// Managed scene data for the linked production geometry/store/pose consumer.
// No Unity ECall, physics engine, renderer, Harmony patch or device is executed.
// Boxes and transforms are synthetic; passing these tests is not measured-car
// geometry, Unity callback ordering, rendered framing or physical acceptance.
namespace ArtOfSimRally.Mod
{
    internal static class CameraFixture { public static string CustomId; }
    internal struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero => new();
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator -(Vector3 a)=>new(-a.x,-a.y,-a.z);
        internal System.Numerics.Vector3 Native=>new(x,y,z);
        internal static Vector3 From(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);
    }
    internal struct Quaternion
    {
        public System.Numerics.Quaternion Value;
        public static Quaternion identity=>new(){Value=System.Numerics.Quaternion.Identity};
        public static Quaternion Euler(float x,float y,float z)=>new(){Value=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};
        public static Quaternion Inverse(Quaternion q)=>new(){Value=System.Numerics.Quaternion.Inverse(q.Value)};
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(){Value=a.Value*b.Value};
        public static Vector3 operator *(Quaternion q,Vector3 v)=>Vector3.From(System.Numerics.Vector3.Transform(v.Native,q.Value));
    }
    internal sealed class Matrix4x4
    {
        public Func<Vector3,Vector3> Map;
        public Vector3 MultiplyPoint3x4(Vector3 v)=>Map(v);
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>new(){Map=v=>a.Map(b.Map(v))};
    }
    internal struct Bounds
    {
        public Vector3 min,max;
        public Bounds(Vector3 min,Vector3 max){this.min=min;this.max=max;}
    }
    internal class Component
    {
        public Transform transform;
        public string name=>transform.name;
        public T GetComponent<T>() where T:class=>transform.GetComponent<T>();
    }
    internal sealed class Transform
    {
        public string name="fixture";
        public Transform parent;
        public Vector3 position;
        public Quaternion rotation=Quaternion.identity;
        public readonly List<Component> Components=new();
        public readonly List<Transform> Children=new();
        public Vector3 up=>rotation*new Vector3(0,1,0);
        public Matrix4x4 localToWorldMatrix=>new(){Map=TransformPoint};
        public Matrix4x4 worldToLocalMatrix=>new(){Map=InverseTransformPoint};
        public Vector3 TransformPoint(Vector3 v)=>position+rotation*v;
        public Vector3 InverseTransformPoint(Vector3 v)=>Quaternion.Inverse(rotation)*(v-position);
        public void Add(Component value){value.transform=this;Components.Add(value);}
        public void Add(Transform child){child.parent=this;Children.Add(child);}
        public T GetComponent<T>() where T:class=>Components.OfType<T>().FirstOrDefault();
        public T GetComponentInParent<T>() where T:class=>GetComponent<T>()??parent?.GetComponentInParent<T>();
        public T[] GetComponentsInChildren<T>()=>Components.OfType<T>().Concat(Children.SelectMany(c=>c.GetComponentsInChildren<T>())).ToArray();
        public Transform Find(string name)=>Children.FirstOrDefault(c=>c.name==name);
        public bool IsChildOf(Transform root)=>ReferenceEquals(this,root)||parent?.IsChildOf(root)==true;
    }
    internal sealed class CarDynamics:Component { }
    internal class Collider:Component { public bool enabled=true,isTrigger; }
    internal sealed class MeshCollider:Collider { public Mesh sharedMesh; }
    internal class Renderer:Component { public bool enabled=true,forceRenderingOff; }
    internal sealed class MeshRenderer:Renderer { }
    internal sealed class SkinnedMeshRenderer:Renderer { public Bounds localBounds; }
    internal sealed class MeshFilter:Component { public Mesh sharedMesh; }
    internal sealed class Mesh
    {
        public Bounds bounds; public bool isReadable;
        public Vector3[] vertices=Array.Empty<Vector3>(); public int[] triangles=Array.Empty<int>();
    }
    internal enum QueryTriggerInteraction { Ignore }
    internal struct RaycastHit { public Collider collider;public Vector3 point; }
    internal static class Physics
    {
        public static RaycastHit[] RaycastAll(Vector3 start,Vector3 direction,float reach,int mask,QueryTriggerInteraction triggers)=>Array.Empty<RaycastHit>();
    }
}
