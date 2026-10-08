"""Check exported geometry and driver sightline independently of Blender.
This is asset validation, not proof of in-game rendering or driving.
"""
import json, math, hashlib, struct
from pathlib import Path

root=Path(__file__).resolve().parents[1]
a=json.loads((root/'assets/turtle-van.json').read_text())
assert a['version']==1 and a['units']=='metres'
origins={o['name']:o['position'] for o in a['origins']}
assert set(origins)=={'body','steering','wheelFL','wheelFR','wheelRL','wheelRR'}
mats={m['name'] for m in a['materials']}
assert len(mats)==16
sub=lambda a,b: tuple(x-y for x,y in zip(a,b))
add=lambda a,b: tuple(x+y for x,y in zip(a,b))
dot=lambda a,b: sum(x*y for x,y in zip(a,b))
def cross(a,b): return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def hit(o,d,p0,p1,p2):
    e1,e2=sub(p1,p0),sub(p2,p0); h=cross(d,e2); det=dot(e1,h)
    if abs(det)<1e-9: return None
    f=1/det; s=sub(o,p0); u=f*dot(s,h)
    if u<0 or u>1: return None
    q=cross(s,e1); v=f*dot(d,q)
    if v<0 or u+v>1: return None
    t=f*dot(e2,q)
    return t if t>.001 else None

triangles=0; degenerates=0; bad_normals=0; opaque=[]; bounds=[]
for p in a['parts']:
    assert p['group'] in origins and p['material'] in mats
    v,n,uv,ix=p['vertices'],p['normals'],p['uv'],p['triangles']
    assert len(v)%3==0 and len(n)==len(v) and len(uv)*3==len(v)*2 and len(ix)%3==0
    assert all(math.isfinite(x) for x in v+n+uv)
    assert all(0<=x<len(v)//3 for x in ix)
    assert all(0<=x<=1 for x in uv)
    for i in range(0,len(ix),3):
        points=[v[k*3:k*3+3] for k in ix[i:i+3]]
        c=cross(sub(points[1],points[0]),sub(points[2],points[0])); area=math.sqrt(dot(c,c))
        if area<1e-10: degenerates+=1; continue
        if dot(c,n[ix[i]*3:ix[i]*3+3]) <= 0: bad_normals+=1
        world=[add(v,origins[p['group']]) for v in points]; bounds+=world
        if p['material']!='glass': opaque.append((p['group'],p['material'],world))
    triangles+=len(ix)//3
assert bad_normals==0, f'{bad_normals} normals disagree with triangle winding'
assert degenerates == 0, (degenerates,triangles)
assert triangles<100000, triangles
assert len(a['parts'])<=40
eye=a['camera']; sightlines={}
for name, direction in [('horizon',(0,0,1)),('road_center',(0,-math.tan(math.radians(6)),1))]:
    hits=[]
    for group,material,points in opaque:
        t=hit(eye,direction,*points)
        if t is not None and t<20: hits.append((t,group,material))
    assert not hits, f'Driver {name} blocked: {sorted(hits)[:4]}'
    sightlines[name]='clear through windshield'
for group in ('wheelFL','wheelFR','wheelRL','wheelRR'):
    vs=[p['vertices'] for p in a['parts'] if p['group']==group]
    yy=[x for v in vs for x in v[1::3]]; zz=[x for v in vs for x in v[2::3]]
    assert abs(max(yy)-.50)<.025 and abs(min(yy)+.50)<.025
    assert abs(max(zz)+min(zz))<.005, f'{group} is not centered for spinning'
png=(root/'assets/palette.png').read_bytes(); assert png[:8]==b'\x89PNG\r\n\x1a\n'
assert struct.unpack('>II',png[16:24])==(256,256)
glb=(root/'assets/turtle-van.glb').read_bytes(); magic,version,size=struct.unpack('<4sII',glb[:12])
assert magic==b'glTF' and version==2 and size==len(glb)
report=dict(triangles=triangles,material_group_meshes=len(a['parts']),degenerate_triangles=degenerates,
            normal_winding_mismatches=bad_normals,driver_sightlines=sightlines,
            minimum=[min(v[i] for v in bounds) for i in range(3)],maximum=[max(v[i] for v in bounds) for i in range(3)],
            game_runtime_tested=False,
            sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (root/'assets').iterdir() if p.suffix in ('.blend','.glb','.json','.png')})
(root/'previews/validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
