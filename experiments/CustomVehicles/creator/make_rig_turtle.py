"""Offline, reversible real-scale Turtle Van candidate; accepted master is read-only.
Run with Blender --background --python creator/make_rig_turtle.py.
Dimensions and fictional-detail provenance: artifacts/rig-vehicles/REPORT.md.
"""
import bpy, bmesh, json, math, sys, hashlib
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/rig-vehicles/turtle-van-real-scale'
OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT.parent/'TurtleVan/assets/turtle-van.blend'
before=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
source=json.loads((SOURCE.parent/'turtle-van.json').read_text())
origins={o['name']:Vector((o['position'][0],o['position'][2],o['position'][1])) for o in source['origins']}
def interp(v, knots):
 for (a,b),(c,d) in zip(knots,knots[1:]):
  if v<=c: return b+(v-a)*(d-b)/(c-a)
 a,b=knots[-2]; c,d=knots[-1]; return b+(v-a)*(d-b)/(c-a)
# G10 110-inch platform length, width and wheelbase; custom raised roof/seat design.
SX=2.0193/2.4398
Y=[(-2.22,-2.26314),(-1.26,-1.397),(1.25,1.397),(2.304,2.26314)]
Z=[(0,0),(.5,.35),(1.02,.95),(1.88,1.65),(2.495,2.2),(3.97,3.10)]
def body(v): return Vector((v.x*SX,interp(v.y,Y),interp(v.z,Z)))
new_origins={k:body(v) for k,v in origins.items()}
for k in new_origins:
 if k.startswith('wheel'): new_origins[k]=Vector((-.86 if k.endswith('L') else .86,1.397 if k[5]=='F' else -1.397,.35))
# Bake evaluated geometry before deforming; wheel radius and steering remain circular.
graph=bpy.context.evaluated_depsgraph_get()
for obj in list(bpy.context.scene.objects):
 if 'group' not in obj:
  bpy.data.objects.remove(obj,do_unlink=True); continue
 group=obj['group']; ev=obj.evaluated_get(graph)
 mesh=bpy.data.meshes.new_from_object(ev,depsgraph=graph)
 world=obj.matrix_world.copy()
 for v in mesh.vertices:
  p=world@v.co
  if group.startswith('wheel'):
   delta=p-origins[group]; p=new_origins[group]+Vector((delta.x*.235/.3696,delta.y*.35/.49,delta.z*.35/.49))
  elif group=='steering':
   delta=p-origins[group]; p=new_origins[group]+Vector((delta.x*.38/.5016,delta.y*.85,delta.z*.38/.456))
  else: p=body(p)
  v.co=p
 name=obj.name; props=dict(obj.items())
 if name=='Shell dark base':
  bm=bmesh.new(); bm.from_mesh(mesh)
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=(0,0,2.2),plane_no=(0,0,1),clear_inner=True,clear_outer=False,dist=0.00001)
  bm.to_mesh(mesh); bm.free()
 bpy.data.objects.remove(obj,do_unlink=True)
 new=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(new)
 for key,val in props.items(): new[key]=val
 new['provenance']='inferred platform adaptation; decorative fictional body/detail'
# Original materials, simple closed geometry, no external image textures.
def box(name,pos,size,mat,bevel=.01):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos); o=bpy.context.object; o.name=name; o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.data.materials.append(bpy.data.materials[mat]); o['group']='body'; o['provenance']='decorative cabin design'
 if bevel:
  m=o.modifiers.new('Edge softening','BEVEL'); m.width=bevel; m.segments=2
 return o
def rod(name,a,b,r,mat):
 a,b=Vector(a),Vector(b); v=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=r,depth=v.length,location=(a+b)*.5)
 o=bpy.context.object; o.name=name; o.rotation_euler=v.to_track_quat('Z','Y').to_euler(); o.data.materials.append(bpy.data.materials[mat]); o['group']='body'; o['provenance']='decorative cabin design'; return o
# Closed, outlined roof access hatch (not claimed to open at runtime).
box('Roof access hatch gasket',(0,.91,2.207),(.75,.72,.018),'rubber')
box('Roof access hatch lid',(0,.91,2.225),(.71,.68,.026),'shell_light')
for x in (-.24,.24): box('Roof hatch hinge',(x,.59,2.245),(.11,.06,.035),'metal')
rod('Roof hatch handle',(-.12,1.16,2.26),(.12,1.16,2.26),.014,'metal')
# Inferred real-size passenger environment; themed details are not canonical dimensions.
for sign in (-1,1):
 x=sign*.88
 box('Interior door trim',(x,.6,1.12),(.026,.84,.35),'seat')
 box('Door armrest',(sign*.82,.63,1.18),(.11,.36,.065),'dash')
 rod('Interior door pull',(sign*.80,.64,1.28),(sign*.80,.78,1.28),.016,'metal')
 box('Front seat belt upper anchor',(sign*.83,.02,1.78),(.04,.06,.06),'metal')
 rod('Seat belt shoulder strap',(sign*.81,.03,1.75),(sign*.18,.37,1.0),.019,'rubber')
 rod('Seat belt lap strap',(sign*.18,.37,1.0),(sign*.73,.39,.99),.019,'rubber')
 box('Seat belt buckle',(sign*.18,.37,1.0),(.05,.05,.07),'red')
# Doghouse console, radio, switches and vents, situated below the windshield.
box('Engine cover console',(0,.83,.93),(.38,.56,.42),'dash',.045)
box('Radio fascia',(0,1.25,1.35),(.28,.055,.115),'metal')
box('Radio display',(0,1.216,1.36),(.15,.008,.04),'blue',0)
for x in (-.11,.11): rod('Radio knob',(x,1.20,1.33),(x,1.215,1.33),.018,'rubber')
for x in (-.70,.23,.62):
 box('Dash vent recess',(x,1.28,1.43),(.16,.035,.07),'rubber')
 for j in range(4): box('Dash vent slat',(x,1.258,1.407+j*.015),(.14,.008,.005),'metal',0)
for j,mat in enumerate(('red','orange','blue','shell_light')):
 box('Console switch',( -.105+j*.07,.72,1.152),(.035,.04,.012),mat,.003)
# Rear command station echoes the cartoon mobile command centre; original layout.
box('Rear workstation cabinet',(-.64,-.72,.95),(.42,.70,.72),'dash',.025)
box('Workstation counter',(-.61,-.72,1.33),(.49,.76,.045),'metal')
box('Turtle communicator monitor',(-.67,-.77,1.53),(.10,.42,.30),'metal')
box('Communicator screen',(-.612,-.77,1.53),(.012,.35,.23),'blue',.005)
for y in (-.88,-.78,-.68): box('Communicator display line',(-.604,y,1.54),(.006,.065,.015),'shell_light',0)
box('Workstation keyboard',(-.36,-.72,1.36),(.15,.38,.025),'rubber')
for y in (-.84,-.77,-.70,-.63):
 for x in (-.40,-.35,-.30): box('Keyboard key',(x,y,1.378),(.025,.045,.009),'cream',0)
box('Rear storage locker',(.66,-.8,.99),(.38,.72,.80),'shell')
for z in (.72,.96,1.2):
 box('Locker drawer front',(.454,-.8,z),(.018,.64,.18),'seat')
 rod('Drawer pull',(.43,-.87,z),(.43,-.73,z),.012,'silver')
box('Pizza box',(0,-1.38,1.08),(.34,.34,.05),'cream')
# Ceiling ribs, two dome lights and rear door handles complete enclosure cues.
for y in (-1.3,-.6,.1): box('Cab ceiling rib',(0,y,2.075),(1.67,.04,.04),'dash')
for y in (-.9,.3): box('Cab dome light',(0,y,2.047),(.15,.10,.022),'lamp')
for x in (-.12,.12): rod('Rear inside door handle',(x,-2.07,1.22),(x,-2.07,1.37),.015,'metal')
for k,p in new_origins.items():
 o=bpy.data.objects.new('origin_'+k,None); bpy.context.collection.objects.link(o); o.location=p
for name,p in [('driver_eye',body(Vector((-.517,.4,1.88)))),('driver_h_point',body(Vector((-.517,.27,1.04))))]:
 o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o); o.location=p; o['certainty']='inferred'; o['note']='Designed seating reference, not measured SAE occupant data'
manifest=dict(schemaVersion=2,id='dbce.turtle-van-real-scale',name='Turtle Van - real-scale candidate',author='d-b-c-e',version='0.1.0',description='Offline G10-dimensioned fan model; custom raised roof and fictional trim, inferred cabin. No handling override.',donorPrefab='Bonus_LtVanLow',model='model.json',texture='palette.png',wheelRadius=.35,cockpit=True)
(OUT/'vehicle.json').write_text(json.dumps(manifest,indent=2))
sys.path.insert(0,str(Path(__file__).parent)); from export_vehicle import export
report=export(OUT)
assert report['triangles']<100000
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'turtle-van-real-scale.blend'))
# glTF vehicle convention: +Z forward, +X left, +Y up.
rotation=Matrix.Rotation(math.pi,4,'Z')
for o in bpy.context.scene.objects: o.matrix_world=rotation@o.matrix_world
bpy.context.view_layer.update()
bpy.ops.export_scene.gltf(filepath=str(OUT/'turtle-van-real-scale.glb'),export_format='GLB',export_extras=True,export_apply=True)
assert before==hashlib.sha256(SOURCE.read_bytes()).hexdigest()
print('PASS: source unchanged; CV model and named-part GLB exported')
