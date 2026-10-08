"""Generate the original Trail Scout editable example and studio previews."""
import math
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'examples' / 'trail-scout'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
colors = {
    'paint': (.045, .36, .32, 1), 'cream': (.84, .78, .60, 1),
    'orange': (.94, .24, .045, 1), 'rubber': (.018, .024, .028, 1),
    'metal': (.13, .17, .19, 1), 'silver': (.58, .64, .64, 1),
    'lamp': (1, .90, .60, 1), 'red': (.64, .025, .02, 1),
    'seat': (.24, .14, .075, 1), 'glass': (.24, .40, .43, 1),
}
materials = {}
for name, color in colors.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = color
    p.inputs['Roughness'].default_value = .45
    if name == 'glass':
        p.inputs['Transmission Weight'].default_value = .85
        p.inputs['Roughness'].default_value = .06
        p.inputs['IOR'].default_value = 1.08
    materials[name] = m
group = 'body'

def finish(obj, name, mat):
    obj.name = name
    obj['group'] = group
    obj.data.materials.append(materials[mat])
    return obj

def box(name, at, size, mat, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft edge', 'BEVEL'); mod.width=bevel; mod.segments=2
        obj.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
    return finish(obj, name, mat)

def rod(name, a, b, radius, mat, sides=16):
    a,b = Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)/2)
    obj=bpy.context.object
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat)

def rail(name, points, radius, mat):
    for a,b in zip(points, points[1:]): rod(name,a,b,radius,mat,12)

def slab(name, polygon, thickness, mat):
    # Closed slab from an ordered polygon and a thickness vector.
    vs=[Vector(p) for p in polygon]
    vs += [v+Vector(thickness) for v in vs]
    n=len(polygon)
    faces=[tuple(reversed(range(n))), tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vs,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    # Recalculate the closed surface so all windows and door panels face outward.
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return finish(obj,name,mat)

def window(name, points, thickness):
    slab(name,points,thickness,'glass')
    rail(name+' rubber seal',points+[points[0]],.014,'rubber')

def marker(name, at):
    obj=bpy.data.objects.new(name,None); obj.location=at; obj.empty_display_size=.13
    bpy.context.collection.objects.link(obj)

# Lower body side sheets have actual wheel-arch openings.
boundary=[(-1.84,.51)]
for wheel_y in (-1.14,1.14):
    boundary.append((wheel_y-.46,.51))
    for i in range(13):
        a=math.pi-i*math.pi/12
        boundary.append((wheel_y+math.cos(a)*.46,.43+math.sin(a)*.46))
boundary += [(1.88,.51),(1.88,1.16),(-1.84,1.16)]
for sign in (-1,1):
    x=sign*.83
    slab('Sculpted side body',[(x,y,z) for y,z in boundary],(sign*.055,0,0),'paint')
    box('Side stripe',(sign*.887,-.02,1.075),(.014,3.65,.06),'orange',.006)
    for axle_y in (-1.14,1.14):
        pts=[(sign*.90,axle_y+math.cos(math.pi-i*math.pi/16)*.465,.43+math.sin(math.pi-i*math.pi/16)*.465) for i in range(17)]
        rail('Wheel arch rolled lip',pts,.034,'paint')
    # Side glazing follows the front pillar rake and the rear body opening.
    side=[(sign*.848,.80,1.24),(sign*.848,.555,1.84),(sign*.848,-.38,1.84),(sign*.848,-.38,1.24)]
    window('Door glass',side,(sign*.009,0,0))
    rear=[(sign*.848,-.49,1.24),(sign*.848,-.49,1.84),(sign*.848,-1.67,1.84),(sign*.848,-1.67,1.24)]
    window('Cargo quarter glass',rear,(sign*.009,0,0))
    for a,b in [((.84,1.18),(.55,1.89)),((-.435,1.18),(-.435,1.88)),((-1.73,1.17),(-1.73,1.88))]:
        rod('Cabin pillar',(sign*.844,*a),(sign*.844,*b),.046,'cream')
    box('Window sill',(sign*.85,-.47,1.205),(.08,2.58,.075),'cream')
    rail('Door seam',[(sign*.892,.81,1.18),(sign*.892,.76,.58),(sign*.892,-.42,.58),(sign*.892,-.42,1.18)],.006,'rubber')
    box('Door handle',(sign*.915,-.24,1.12),(.035,.16,.045),'silver',.01)
    rod('Mirror arm',(sign*.86,.65,1.32),(sign*1.04,.64,1.48),.018,'metal')
    box('Mirror',(sign*1.04,.62,1.49),(.18,.075,.15),'metal')
    box('Mirror face',(sign*1.04,.579,1.49),(.14,.01,.11),'silver',.01)
    box('Side step',(sign*.89,-.04,.47),(.19,1.17,.07),'metal')

box('Cabin floor',(0,-.30,.63),(1.63,2.77,.10),'metal')
box('Hood',(0,1.32,1.16),(1.70,1.12,.12),'paint',.06)
box('Front panel',(0,1.87,.91),(1.69,.08,.53),'paint')
box('Rear tailgate',(0,-1.81,.92),(1.71,.08,.51),'paint')
box('Front bumper',(0,1.98,.61),(1.89,.18,.14),'silver')
box('Rear bumper',(0,-1.94,.61),(1.89,.18,.14),'silver')
box('Skid plate',(0,1.80,.48),(1.14,.27,.09),'metal')
box('Grille',(0,1.922,.96),(.86,.028,.21),'rubber')
for z in (.91,.97,1.03): box('Grille bar',(0,1.944,z),(.82,.023,.02),'silver',.003)
for x in (-.64,.64):
    rod('Headlight bezel',(x,1.90,1.02),(x,1.961,1.02),.132,'silver',24)
    rod('Headlight lens',(x,1.963,1.02),(x,1.975,1.02),.105,'lamp',24)
    box('Front indicator',(x,1.932,.80),(.18,.028,.055),'orange',.008)
    box('Rear lamp',(x,-1.863,.98),(.16,.025,.22),'red',.016)
    box('Rear reverse lamp',(x,-1.879,.92),(.12,.012,.052),'cream',.004)
box('Front plate',(0,2.079,.61),(.38,.012,.10),'cream',.006)
box('Rear plate',(0,-1.87,.82),(.36,.012,.11),'cream',.006)

box('Cream roof',(0,-.59,1.94),(1.81,2.51,.12),'cream',.045)
window('Windshield',[(-.79,.824,1.25),(.79,.824,1.25),(.79,.584,1.84),(-.79,.584,1.84)],(0,.008,.003))
rail('Front windscreen surround',[(-.84,.848,1.20),(.84,.848,1.20),(.84,.566,1.89),(-.84,.566,1.89),(-.84,.848,1.20)],.034,'cream')
window('Rear window',[(-.77,-1.768,1.25),(.77,-1.768,1.25),(.77,-1.768,1.84),(-.77,-1.768,1.84)],(0,-.008,0))
for x in (-.43,.43):
    rod('Wiper',(x-.18,.845,1.25),(x+.12,.782,1.40),.01,'rubber')
    box('Seat base',(x,-.16,.80),(.55,.56,.22),'metal')
    box('Seat cushion',(x,-.15,.965),(.61,.62,.14),'seat',.06)
    seat=box('Seat back',(x,-.46,1.27),(.61,.14,.65),'seat',.05); seat.rotation_euler.x=-.10
    box('Seat headrest',(x,-.49,1.63),(.34,.13,.18),'seat',.05)
    for y in (-.31,-.12,.07): box('Seat stitch',(x,y,1.038),(.49,.007,.003),'cream',0)
box('Dashboard',(0,.71,1.19),(1.55,.28,.20),'metal',.04)
box('Dashboard trim',(0,.56,1.16),(1.52,.025,.034),'paint',.006)
for x,r in ((-.57,.084),(-.37,.084),(-.18,.047)):
    rod('Instrument bezel',(x,.544,1.23),(x,.526,1.23),r,'silver',24)
    rod('Instrument face',(x,.523,1.23),(x,.518,1.23),r*.84,'rubber',24)
    rod('Instrument needle',(x,.512,1.23),(x-.03,.512,1.265),.003,'orange',6)
for x in (.12,.22,.32): box('Dash switch',(x,.535,1.22),(.045,.025,.05),'cream',.004)
box('Console',(0,.34,.89),(.18,.32,.26),'metal')
rod('Gearstick',(0,.32,1),(0,.19,1.23),.014,'silver')
rod('Gear knob',(0,.19,1.22),(0,.19,1.27),.033,'rubber')
for x in (-.60,-.43,-.26): box('Pedal',(x,.71,.72),(.08,.12,.035),'rubber',.006)
box('Rear bench',(0,-1.26,.95),(1.41,.47,.14),'seat',.05)
box('Rear backrest',(0,-1.54,1.24),(1.41,.15,.50),'seat',.05)
rod('Steering column',(-.43,.74,1.09),(-.43,.37,1.24),.025,'metal')
group='steering'
bpy.ops.mesh.primitive_torus_add(major_radius=.17, minor_radius=.017, major_segments=36, minor_segments=8, location=(-.43,.37,1.24), rotation=(math.pi/2,0,0))
finish(bpy.context.object,'Steering rim','rubber')
for angle in (0,120,240):
    t=math.radians(angle)
    rod('Steering spoke',(-.43,.37,1.24),(-.43+math.sin(t)*.16,.37,1.24+math.cos(t)*.16),.011,'silver')
rod('Steering hub',(-.43,.39,1.24),(-.43,.34,1.24),.041,'paint')

origins={'body':(0,0,0),'steering':(-.43,.37,1.24)}
for side,x in (('L',-.85),('R',.85)):
    for axle,y in (('F',1.14),('R',-1.14)):
        group='wheel'+axle+side
        origins[group]=(x,y,.40)
        rod('All terrain tire',(x-.125,y,.4),(x+.125,y,.4),.38,'rubber',32)
        out=x+(-.13 if x<0 else .13)
        rod('Wheel rim',(out-.01,y,.4),(out+.01,y,.4),.25,'cream',24)
        rod('Wheel center',(out-.016,y,.4),(out+.016,y,.4),.115,'silver',16)
        for j in range(24):
            t=j*math.tau/24
            tread=box('Tire tread',(x,y+math.sin(t)*.38,.4+math.cos(t)*.38),(.255,.028,.016),'rubber',.003)
            tread.rotation_euler.x=-t
        for j in range(6):
            t=j*math.tau/6
            rod('Rim aperture',(out-.012,y+math.sin(t)*.185,.4+math.cos(t)*.185),(out+.012,y+math.sin(t)*.185,.4+math.cos(t)*.185),.033,'metal',10)

group='body'
# Roof basket and rear-mounted expedition can, separate from the glass openings.
for x in (-.63,.63):
    for y in (-1.27,.14): rod('Roof rack foot',(x,y,1.99),(x,y,2.08),.026,'metal')
    rod('Roof rail',(x,-1.42,2.13),(x,.32,2.13),.025,'metal')
for y in (-1.42,-1.05,-.68,-.31,.06,.32): rod('Roof crossbar',(-.63,y,2.085),(.63,y,2.085),.021,'metal')
for y in (-1.42,.32): rod('Roof rail end',(-.63,y,2.13),(.63,y,2.13),.025,'metal')
box('Expedition case',(.18,-.67,2.24),(.65,.87,.27),'orange',.05)
for x in (-.02,.38): box('Case strap',(x,-.67,2.387),(.035,.83,.012),'metal',.003)
box('Tailgate can',(.32,-1.98,1.10),(.36,.19,.48),'paint',.04)
rail('Can strap',[(.13,-2.084,.95),(.51,-2.084,.95),(.51,-2.084,1.25),(.13,-2.084,1.25)],.014,'metal')

for name,at in origins.items(): marker('origin_'+name,at)
marker('driver_eye',(-.43,-.23,1.54))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'trail-scout.blend'))
sys.path.insert(0,str(Path(__file__).parent))
from export_vehicle import export
export(OUT)

# Studio items have no group property and never enter a vehicle export.
group='studio'
floor=box('Studio floor',(0,0,-.065),(200,200,.10),'cream',0)
del floor['group']
world=bpy.context.scene.world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.17,.21,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
def aim(obj,point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,at,power,size in [('Key',(-4,5,6),1100,5),('Fill',(4,2,4),800,4),('Rim',(1,-5,5),1300,3)]:
    bpy.ops.object.light_add(type='AREA',location=at)
    obj=bpy.context.object; obj.name=name; obj.data.energy=power; obj.data.size=size; aim(obj,(0,0,1))
bpy.ops.object.camera_add(location=(-5,7,4))
cam=bpy.context.object; scene=bpy.context.scene; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
scene.render.resolution_x=1200; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.view_settings.view_transform='AgX'
cam.data.type='ORTHO'; cam.data.ortho_scale=5.5; aim(cam,(0,0,1.06))
scene.render.filepath=str(OUT/'preview.png'); bpy.ops.render.render(write_still=True)
scene.render.filepath=str(ROOT/'artifacts'/'trail-scout-cockpit.png')
cam.data.type='PERSP'; cam.data.lens=20; cam.data.clip_start=.025; cam.location=(-.43,-.23,1.54)
aim(cam,(-.43,6,.88)); scene.render.resolution_y=675
bpy.ops.render.render(write_still=True)
