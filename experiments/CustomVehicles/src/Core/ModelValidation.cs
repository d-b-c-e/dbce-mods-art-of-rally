using System;
using System.IO;
using System.Linq;

namespace RallyCustomVehicles
{
    internal static class ModelValidation
    {
        internal static void Validate(Asset a)
        {
            if (a.parts.Length > 128 || a.parts.Sum(p => (long)p.triangles.Length / 3) > 100000 || a.parts.Sum(p=>(long)p.vertices.Length/3)>300000) throw new InvalidDataException("Model exceeds 128 parts, 100,000 triangles or 300,000 vertices.");
            foreach(float v in a.camera) VehiclePackage.Range(v,-10,10,"camera");
            foreach(var origin in a.origins)
                if(!a.parts.Any(p=>p.group==origin.name)) throw new InvalidDataException("Missing geometry for group: "+origin.name);
            var fl=a.origins.Single(o=>o.name=="wheelFL").position; var fr=a.origins.Single(o=>o.name=="wheelFR").position;
            var rl=a.origins.Single(o=>o.name=="wheelRL").position; var rr=a.origins.Single(o=>o.name=="wheelRR").position;
            if (fl[2]-rl[2]<.3f || fr[2]-rr[2]<.3f || fr[0]-fl[0]<.3f || rr[0]-rl[0]<.3f) throw new InvalidDataException("Wheel markers must use X right, Y up, Z forward and a four-wheel layout.");
            if (Math.Abs(fl[2]-fr[2])>.02f || Math.Abs(rl[2]-rr[2])>.02f) throw new InvalidDataException("Left/right axle markers must share Z within 2 cm.");
            foreach (var o in a.origins) foreach(float v in o.position) VehiclePackage.Range(v,-50,50,"origin");
            foreach (var m in a.materials) foreach(float v in m.color) VehiclePackage.Range(v,0,1,"material color");
            foreach (var p in a.parts)
            {
                foreach (float v in p.vertices) VehiclePackage.Range(v,-50,50,"vertex");
                foreach (float v in p.uv) VehiclePackage.Range(v,0,1,"UV");
                for (int i=0; i<p.normals.Length; i+=3)
                {
                    float n=p.normals[i]*p.normals[i]+p.normals[i+1]*p.normals[i+1]+p.normals[i+2]*p.normals[i+2];
                    if(n<.9f || n>1.1f) throw new InvalidDataException("Normals must be normalized.");
                }
                for(int i=0;i<p.triangles.Length;i+=3)
                {
                    int x=p.triangles[i]*3,y=p.triangles[i+1]*3,z=p.triangles[i+2]*3;
                    double ax=p.vertices[y]-p.vertices[x], ay=p.vertices[y+1]-p.vertices[x+1], az=p.vertices[y+2]-p.vertices[x+2];
                    double bx=p.vertices[z]-p.vertices[x], by=p.vertices[z+1]-p.vertices[x+1], bz=p.vertices[z+2]-p.vertices[x+2];
                    double nx=ay*bz-az*by,ny=az*bx-ax*bz,nz=ax*by-ay*bx;
                    if(nx*nx+ny*ny+nz*nz<1e-20 || nx*p.normals[x]+ny*p.normals[x+1]+nz*p.normals[x+2]<=0)
                        throw new InvalidDataException("Degenerate triangle or normal/winding mismatch.");
                }
            }
        }
    }
}
