"""Original, editable Turtle Van fan model. Run with Blender --background --python.
Blender axes: X right, Y forward, Z up. Runtime export: X right, Y up, Z forward.
No game geometry is extracted. Rear/cabin are original interpretations of the reference.
"""
import bpy, bmesh, math, json, os
from mathutils import Vector
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'assets'
PREVIEWS = ROOT / 'previews'
ASSETS.mkdir(exist_ok=True); PREVIEWS.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
M = {}
colors = {
 'paint': (0.94, .67, .105, 1), 'highlight': (1, .79, .24, 1),
 'shell': (.045, .21, .065, 1), 'shell_light': (.085, .30, .09, 1),
 'seam': (.015, .035, .02, 1), 'rubber': (.028, .033, .033, 1),
 'metal': (.32, .37, .37, 1), 'silver': (.64, .69, .66, 1),
 'red': (.72, .025, .015, 1), 'cream': (.96, .91, .69, 1),
 'seat': (.12, .20, .13, 1), 'dash': (.055, .09, .064, 1),
 'orange': (1, .25, .02, 1), 'blue': (.025, .37, .55, 1),
 'glass': (.25, .48, .48, .16), 'lamp': (.97, .91, .66, 1),
}
for name, c in colors.items():
    m = bpy.data.materials.new(name); m.diffuse_color = c; m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    # Palette entries are sRGB, Blender shader colors are linear.
    linear = tuple(v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in c[:3])
    bs.inputs['Base Color'].default_value = (*linear,c[3])
    bs.inputs['Roughness'].default_value = .48 if name not in ('rubber','seat') else .85
    if name in ('silver','metal'): bs.inputs['Metallic'].default_value = .55
    if name == 'glass':
        bs.inputs['Alpha'].default_value = c[3]
        m.surface_render_method = 'DITHERED'
    M[name] = m

GROUP = 'body'
def finish(o, name, mat, bevel=0, segments=3):
    o.name = name; o.data.materials.append(M[mat]); o['group'] = GROUP
    if bevel:
        mod = o.modifiers.new('Soft toy edges', 'BEVEL'); mod.width = bevel; mod.segments = segments
        mod = o.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    return o
def box(name, pos, size, mat, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos); o = bpy.context.object
    o.dimensions = size; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, name, mat, bevel)
def sphere(name, pos, scale, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, location=pos)
    o = bpy.context.object; o.scale = scale
    for p in o.data.polygons: p.use_smooth=True
    return finish(o, name, mat)
def rod(name, a, b, r, mat, r2=None, vertices=16):
    a,b = Vector(a),Vector(b); d=b-a
    if r<.025: vertices=min(vertices,8)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r, radius2=r if r2 is None else r2, depth=d.length, location=(a+b)/2)
    o=bpy.context.object; o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    return finish(o,name,mat,min(.008,r*.2),2)
def mesh(name, vs, faces, mat, bevel=0):
    me=bpy.data.meshes.new(name); me.from_pydata(vs,[],faces); me.update()
    o=bpy.data.objects.new(name,me); bpy.context.collection.objects.link(o)
    return finish(o,name,mat,bevel)
def label(name, text, pos, size, mat, rot=(math.pi/2,0,math.pi), extrude=.001):
    cu=bpy.data.curves.new(name,'FONT'); cu.body=text; cu.size=size; cu.align_x='CENTER'; cu.align_y='CENTER'; cu.extrude=extrude
    o=bpy.data.objects.new(name,cu); bpy.context.collection.objects.link(o); o.location=pos; o.rotation_euler=rot
    finish(o,name,mat); return o
def torus(name,pos,major,minor,mat,rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=pos,major_radius=major,minor_radius=minor,rotation=rot)
    return finish(bpy.context.object,name,mat)

def rounded_loop(w,l,r,z):
    points=[]
    for cx,cy,start in ((w-r,l-r,0),(-w+r,l-r,90),(-w+r,-l+r,180),(w-r,-l+r,270)):
        for i in range(9):
            t=math.radians(start+i*90/8)
            points.append((cx+r*math.cos(t),cy+r*math.sin(t),z))
    return points
def hull(name,z0,z1,w,l,r,thickness,mat):
    loops=[rounded_loop(w,l,r,z0),rounded_loop(w,l,r,z1),
           rounded_loop(w-thickness,l-thickness,r-thickness,z0),rounded_loop(w-thickness,l-thickness,r-thickness,z1)]
    n=len(loops[0]); vs=sum(loops,[]); fs=[]
    for i in range(n):
        j=(i+1)%n
        fs.extend([(i,j,n+j,n+i),(2*n+j,2*n+i,3*n+i,3*n+j),
                   (n+i,n+j,3*n+j,3*n+i),(j,i,2*n+i,2*n+j)])
    o=mesh(name,vs,fs,mat)
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free()
    return o
def curve_rail(name,points,r,mat):
    # Continuous swept tube keeps the trim smooth without hundreds of bevels.
    points=[Vector(p) for p in points]; vs=[]; fs=[]; sides=8
    for i,p in enumerate(points):
        d=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
        axis=Vector((0,0,1)) if abs(d.z)<.9 else Vector((0,1,0))
        n=d.cross(axis).normalized(); b=d.cross(n)
        for j in range(sides):
            t=j*math.tau/sides; vs.append(tuple(p+r*(math.cos(t)*n+math.sin(t)*b)))
    for i in range(len(points)-1):
        for j in range(sides):
            k=(j+1)%sides; fs.append((i*sides+j,i*sides+k,(i+1)*sides+k,(i+1)*sides+j))
    fs.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
    o=mesh(name,vs,fs,mat)
    for p in o.data.polygons: p.use_smooth=p.index<len(fs)-2

# Separate wall panels leave a real hollow cabin, including open wheel wells.
box('Floor',(0,0,.65),(1.82,3.94,.14),'dash')
# A hollow rounded shell gives actual quarter-panel curvature; thin cubes could
# not carry a large bevel because their thickness clamped the rounding radius.
lower=hull('Rounded lower body',.68,1.60,.985,2.055,.25,.095,'paint')
for y in (-1.26,1.25):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.575,depth=2.65,location=(0,y,.5),rotation=(0,math.pi/2,0))
    cutter=bpy.context.object
    bo=lower.modifiers.new('Open wheel arch','BOOLEAN'); bo.operation='DIFFERENCE'; bo.object=cutter
    bpy.context.view_layer.objects.active=lower
    bpy.ops.object.modifier_apply(modifier=bo.name); bpy.data.objects.remove(cutter,do_unlink=True)
mod=lower.modifiers.new('Soft body edges','BEVEL'); mod.width=.018; mod.segments=3
lower.modifiers.new('Body normals','WEIGHTED_NORMAL')
box('Rear wall',(0,-1.98,1.46),(1.91,.13,1.74),'paint',.04)
box('Rear inset door',(0,-2.055,1.36),(1.44,.05,1.13),'highlight')
rod('Rear door split',(0,-2.09,.87),(0,-2.09,1.87),.014,'paint')
for x in (-.96,.96):
    for y in (-1.26,1.25):
        # Fuller rounded fender strip follows the open wheel arch.
        vs=[]
        for i in range(21):
            t=math.pi*i/20
            for j in range(5):
                u=j/4; r=.578+.09*u
                vs.append((x*(1.032+.035*math.sin(math.pi*u)),y+math.cos(t)*r,.5+math.sin(t)*r))
        fs=[(i*5+j,i*5+j+1,(i+1)*5+j+1,(i+1)*5+j) for i in range(20) for j in range(4)]
        mesh('Wheel arch trim',vs,[tuple(reversed(f)) for f in fs] if x<0 else fs,'highlight')
    box('Rear upper side',(x,-.95,1.97),(.105,2.02,.87),'paint')
    box('Sliding door seam',(x*1.066,-.23,1.15),(.018,.014,.62),'seam',0)
    box('Cab lower door',(x*1.04,.61,1.25),(.035,.92,.51),'paint',.015)
    box('Door pull',(x*1.057,.31,1.53),(.04,.17,.045),'metal',.015)
    box('Side runner',(x*1.085,0,.56),(.22,1.37,.09),'shell')
    # Side window posts connect windshield to a closed rear cabin.
    rod('B pillar',(x,.05,1.57),(x,.05,2.40),.047,'paint')
    rod('A pillar',(x,1.94,1.57),(x,1.72,2.40),.047,'paint')
    rod('Door belt',(x,.03,1.58),(x,1.96,1.58),.048,'highlight')
    box('Side glazing',(x,.83,2.02),(.012,1.48,.67),'glass',0)
    box('Side mirror',(x*1.23,1.46,1.99),(.13,.075,.29),'metal')
    rod('Mirror bracket',(x,1.4,1.94),(x*1.23,1.46,1.96),.022,'metal')
    # Four larger raised panels echo the toy's armored sliding side door.
    box('Armored door backing',(x*1.07,-.67,1.61),(.055,1.08,1.08),'paint',.02)
    for y in (-.94,-.40):
        for z in (1.35,1.88):
            box('Side armor panel',(x*1.11,y,z),(.09,.40,.39),'highlight',.04)
    box('Rear vent recess',(x*1.065,-1.64,1.96),(.045,.39,.39),'seam',.015)
    for z in (1.82,1.89,1.96,2.03,2.10):
        box('Rear cooling louvre',(x*1.11,-1.64,z),(.095,.32,.035),'paint',.013)
    for y in (-1.03,-.32):
        for z in (1.18,2.07): sphere('Armor fastener',(x*1.17,y,z),(.009,.014,.014),'metal')
    # Door perimeter and trim provide readable surface detail from the chase view.
    curve_rail('Cab door outline',[(x*1.042,.09,.88),(x*1.042,.09,1.48),(x*1.042,1.07,1.48)],.009,'metal')
    box('Rear lamp',(x*.79,-2.075,1.05),(.20,.055,.16),'red')
    rod('Roof beacon post',(x*.85,1.63,2.46),(x*.85,1.63,2.72),.04,'highlight')
    rod('Beacon housing',(x*.85,1.44,2.74),(x*.85,1.78,2.74),.1,'highlight')
    rod('Beacon lens',(x*.85,1.78,2.74),(x*.85,1.80,2.74),.079,'red')
    torus('Beacon lens guard',(x*.85,1.81,2.74),.084,.007,'highlight',(math.pi/2,0,0))

for height,r,mat in ((1.60,.06,'highlight'),(1.48,.012,'paint')):
    pts=rounded_loop(1.014,2.078,.26,height); curve_rail('Wraparound belt moulding',pts+[pts[0]],r,mat)

# Split windscreen, with thin transparent panes and rubber trim.
for x in (-.47,.47):
    vs=[(x-.42,1.945,1.64),(x+.42,1.945,1.64),(x+.42,1.755,2.34),(x-.42,1.755,2.34)]
    mesh('Windshield glass',vs,[(0,1,2,3)],'glass')
rod('Windshield center',(0,1.955,1.58),(0,1.737,2.40),.028,'paint')
rod('Windshield sill',(-.96,1.98,1.58),(.96,1.98,1.58),.055,'highlight')
for x in (-.46,.46): rod('Wiper',(x-.24,1.972,1.64),(x+.23,1.973,1.70),.012,'rubber')
box('Cab roof',(0,.26,2.43),(2.08,3.85,.13),'shell',.045)
box('Front visor',(0,1.95,2.42),(2.18,.52,.085),'shell')
curve_rail('Roof rain gutter',[(-1.043,-1.66,2.44),(-1.043,1.75,2.44),(-.97,1.97,2.44),(.97,1.97,2.44),(1.043,1.75,2.44),(1.043,-1.66,2.44)],.025,'shell_light')
box('Rear bumper',(0,-2.11,.64),(1.99,.22,.24),'shell')
box('Front bumper',(0,2.14,.66),(2.12,.29,.37),'shell',.105)

# Shell roof: separated curved green plates over a dark base.
sphere('Shell dark base',(0,-.70,2.48),(.98,1.27,.47),'seam')
for row in range(3):
    t0=-1.10+row*.71; t1=t0+.66
    for col in range(5):
        a0=col*math.pi/5+.028; a1=(col+1)*math.pi/5-.028
        vs=[]
        for ti in range(5):
            t=t0+(t1-t0)*ti/4
            for ai in range(5):
                a=a0+(a1-a0)*ai/4
                vs.append((.995*math.cos(t)*math.cos(a),-.70+1.29*math.sin(t),2.49+.48*math.cos(t)*math.sin(a)))
        fs=[(i*5+j,i*5+j+1,(i+1)*5+j+1,(i+1)*5+j) for i in range(4) for j in range(4)]
        mesh('Roof shell plate',vs,[tuple(reversed(f)) for f in fs],'shell_light' if (row+col)%3==0 else 'shell')

for x in (-.89,.89):
    box('Spoiler upright',(x,-1.79,2.45),(.13,.17,1.25),'paint')
    box('Spoiler endplate',(x,-1.72,3.13),(.065,.60,.29),'highlight')
box('Big rear wing',(0,-1.75,3.09),(1.92,.57,.12),'highlight')
for x in (-.61,.61):
    a=(x,-.43,2.76); b=(x,.10,3.13); c=(x,1.18,3.85)
    sphere('Cannon socket',a,(.17,.20,.15),'metal')
    rod('Cannon breech',a,b,.11,'metal',.075)
    rod('Cannon barrel',b,c,.034,'metal')
    for t in (.12,.22,.77):
        start=Vector(b).lerp(Vector(c),t); end=Vector(b).lerp(Vector(c),t+.025)
        rod('Cannon barrel collar',start,end,.047,'silver')
    for sign in (-1,1):
        rod('Breech inset',(x+sign*.075,-.26,2.88),(x+sign*.065,-.02,3.06),.012,'seam')
    rod('Cannon muzzle',(x,1.08,3.78),(x,1.31,3.94),.065,'metal',.025)
    sphere('Muzzle dark tip',(x,1.32,3.945),(.025,.025,.025),'rubber')
rod('Radar mast',(0,.72,2.48),(0,.72,3.03),.028,'metal')
box('Red radar flag',(0,.73,2.98),(.33,.06,.25),'red')

# Front emblem and round lamps. Front faces look +Y.
sphere('Front shell surround',(0,2.085,1.13),(.38,.11,.40),'seam')
sphere('Front shell emblem',(0,2.14,1.13),(.33,.07,.35),'shell_light')
for x in (-.12,.12): rod('Emblem seam',(x,2.214,.87),(x,2.214,1.40),.011,'seam')
box('Badge band',(0,2.222,1.16),(.68,.025,.12),'seam',.01)
label('TURTLES badge','TURTLES',(0,2.244,1.16),.105,'highlight')
for x in (-.73,.73):
    rod('Headlight bezel',(x,2.075,1.07),(x,2.15,1.07),.15,'silver')
    rod('Headlight glass',(x,2.15,1.07),(x,2.17,1.07),.113,'lamp')
    torus('Headlamp gasket',(x,2.18,1.07),.118,.009,'rubber',(math.pi/2,0,0))
    for dx in (-.05,0,.05):
        h=math.sqrt(.095**2-dx**2)
        rod('Lens flute',(x+dx,2.175,1.07-h),(x+dx,2.175,1.07+h),.003,'silver',vertices=6)
    rod('Marker light',(x,2.078,1.40),(x,2.12,1.40),.038,'red')
    # Curved individual teeth follow an almond-shaped, black cartoon mouth.
    sign=1 if x>0 else -1
    cx=sign*.53
    sphere('Grin outline',(cx,2.265,.66),(.395,.033,.132),'seam')
    for i in range(6):
        lo=-1+i/3+.015; hi=-1+(i+1)/3-.015
        for row in (-1,1):
            us=[lo+(hi-lo)*j/4 for j in range(5)]
            vs=[]
            for u in us:
                hh=.118*math.sqrt(max(0,1-u*u))
                vs.extend([(cx+u*.377,2.304,.66+row*.007),(cx+u*.377,2.304,.66+row*hh)])
            fs=[(j*2,j*2+1,j*2+3,j*2+2) for j in range(4)]
            if row<0: fs=[tuple(reversed(f)) for f in fs]
            mesh('Grinning teeth',vs,fs,'cream')

# Fully hollow cabin: seats, dashboard, instruments, pedals, cargo bench.
box('Dashboard',(0,1.45,1.49),(1.77,.38,.18),'dash',.06)
box('Dashboard yellow lip',(0,1.235,1.52),(1.79,.07,.12),'paint')
for x in (-.47,.47):
    box('Seat pedestal',(x,.44,.83),(.54,.55,.32),'metal')
    box('Seat cushion',(x,.42,1.02),(.63,.64,.17),'seat',.08)
    back=box('Seat back',(x,.10,1.38),(.63,.17,.64),'seat',.09); back.rotation_euler.x=-.10
    box('Headrest',(x,.06,1.78),(.35,.14,.19),'seat',.06)
    for y in (.22,.39,.56): box('Seat stitching',(x,y,1.11),(.48,.014,.008),'shell_light',.003)
box('Rear bench',(0,-1.33,1.02),(1.63,.52,.18),'seat',.07)
box('Rear bench back',(0,-1.66,1.33),(1.63,.16,.58),'seat',.07)
box('Pizza box',(0,-1.26,1.16),(.49,.49,.07),'cream')
label('Pizza box print','PIZZA',(0,-1.26,1.203),.10,'red',(0,0,0))
box('Center console',(.08,1.04,1.10),(.23,.38,.32),'dash')
rod('Gear lever',(.08,1.03,1.22),(.08,.92,1.46),.016,'metal')
sphere('Gear knob',(.08,.92,1.46),(.039,.039,.039),'red')
for x in (-.63,-.47,-.29): box('Pedal',(x,1.42,.79),(.1,.15,.045),'rubber')
for x in (-.67,-.43,-.20):
    rod('Gauge bezel',(x,1.193,1.52),(x,1.175,1.52),.097 if x<-.3 else .061,'silver')
    rod('Gauge face',(x,1.173,1.52),(x,1.167,1.52),.080 if x<-.3 else .048,'rubber')
    radius=.065 if x<-.3 else .038
    for i in range(9):
        t=math.radians(-135+i*33.75)
        rod('Gauge tick',(x+math.sin(t)*radius,1.164,1.52+math.cos(t)*radius),
            (x+math.sin(t)*radius*.80,1.164,1.52+math.cos(t)*radius*.80),.002,'cream',vertices=6)
    rod('Gauge needle',(x,1.161,1.52),(x-.022,1.161,1.55),.003,'red',vertices=6)
for i, mat in enumerate(('blue','red','orange','shell_light')):
    box('Turtle switch',(.22+i*.145,1.19,1.53),(.085,.035,.063),mat,.009)
label('Dash slogan','SEWER CRUISER',(.44,1.205,1.44),.045,'cream',(math.pi/2,0,0))
rod('Steering column',(-.47,1.40,1.37),(-.47,1.02,1.48),.03,'metal')
GROUP='steering'
torus('Steering wheel',(-.47,1.02,1.48),.205,.023,'rubber',(math.pi/2,0,0))
for angle in (0,120,240):
    t=math.radians(angle)
    rod('Steering spoke',(-.47,1.02,1.48),(-.47+math.sin(t)*.185,1.02,1.48+math.cos(t)*.185),.016,'metal')
rod('Steering hub',(-.47,1.05,1.48),(-.47,.99,1.48),.055,'shell_light')

origins={'body':(0,0,0),'steering':(-.47,1.02,1.48)}
for side,x in (('L',-.97),('R',.97)):
    for axle,y in (('F',1.25),('R',-1.26)):
        GROUP='wheel'+axle+side; origins[GROUP]=(x,y,.50)
        rod('Tire',(x-.14,y,.50),(x+.14,y,.50),.49,'rubber',vertices=32)
        out=x+(-.155 if x<0 else .155)
        rod('Wheel rim',(out-.018,y,.50),(out+.018,y,.50),.32,'silver',vertices=24)
        rod('Hubcap',(out-.023,y,.50),(out+.023,y,.50),.255,'metal',vertices=24)
        for i in range(24):
            t=i*math.tau/24
            tread=box('Tread',(x,y+math.sin(t)*.488,.5+math.cos(t)*.488),(.255,.022,.011),'rubber',.002)
            tread.rotation_euler.x=-t
        for i in range(5):
            t=i*math.tau/5
            sphere('Wheel lug',(out+(-.027 if x<0 else .027),y+math.sin(t)*.17,.50+math.cos(t)*.17),(.012,.018,.018),'silver')

# Width is authored into every part, including cabin and eye marker, so the
# editable model matches the shipped asset. Tyres grow axially, not in radius.
WIDEN=1.10
from mathutils import Matrix
stretch=Matrix.Diagonal((WIDEN,1,1,1))
bpy.context.view_layer.update()
for ob in list(bpy.context.scene.objects):
    if ob.type not in ('MESH','FONT') or 'group' not in ob: continue
    ob.matrix_world=stretch@ob.matrix_world
    if ob['group'].startswith('wheel'):
        pivot=Vector(origins[ob['group']]); pivot.x*=WIDEN
        ob.matrix_world=Matrix.Translation(pivot)@Matrix.Diagonal((1.20,1,1,1))@Matrix.Translation(-pivot)@ob.matrix_world
origins={k:(v[0]*WIDEN,v[1],v[2]) for k,v in origins.items()}

# UV palette atlas. Every exported surface gets a real UV and a palette swatch.
atlas=bpy.data.images.new('TurtleVan palette atlas',width=256,height=256,alpha=True)
pixels=[]
for y in range(256):
    for x in range(256):
        c=list(colors.values())[(y//64)*4+x//64]
        pixels.extend((*c[:3],1))
atlas.pixels=pixels; atlas.filepath_raw=str(ASSETS/'palette.png'); atlas.file_format='PNG'; atlas.save()

# Evaluated export bakes modifiers and text into original, runtime-ready geometry.
bpy.context.view_layer.update(); dg=bpy.context.evaluated_depsgraph_get()
parts={}
def unity(v): return [round(v[0],6),round(v[2],6),round(v[1],6)]
for ob in list(bpy.context.scene.objects):
    if ob.type not in ('MESH','FONT') or 'group' not in ob: continue
    ev=ob.evaluated_get(dg); me=ev.to_mesh(); me.calc_loop_triangles()
    mat=ob.data.materials[0].name; group=ob['group']; key=(group,mat)
    p=parts.setdefault(key,dict(group=group,material=mat,vertices=[],normals=[],uv=[],triangles=[]))
    origin=Vector(origins[group]); normalmat=ob.matrix_world.to_3x3().inverted().transposed()
    idx=list(colors).index(mat); uv=((idx%4+.5)/4,(idx//4+.5)/4)
    for tri in me.loop_triangles:
        corners=[Vector(unity(ob.matrix_world@me.vertices[i].co-origin)) for i in tri.vertices]
        if (corners[1]-corners[0]).cross(corners[2]-corners[0]).length < 1e-10:
            continue # Bevel junctions may contain collapsed triangles; never ship them.
        first=len(p['vertices'])//3
        # At sharp tube elbows/thin bevel junctions, a weighted corner normal
        # can point behind a triangulated face. Keep smooth normals elsewhere,
        # but use that face's normal on these corners to avoid dark seams.
        face=(corners[2]-corners[0]).cross(corners[1]-corners[0]).normalized()
        for loop,vi in zip(tri.loops,tri.vertices):
            v=me.vertices[vi]; p['vertices']+=unity(ob.matrix_world@v.co-origin)
            normal=Vector(unity((normalmat@me.corner_normals[loop].vector).normalized()))
            if normal.dot(face)<=.001: normal=face
            p['normals']+=[round(v,6) for v in normal]; p['uv']+=list(uv)
        p['triangles'] += [first,first+2,first+1] # swap winding for reflected coordinate system
    ev.to_mesh_clear()
asset=dict(version=1,units='metres',camera=unity((-.47*WIDEN,.40,1.88)),
           origins=[dict(name=k,position=unity(v)) for k,v in origins.items()],
           materials=[dict(name=k,color=list(v)) for k,v in colors.items()],parts=list(parts.values()))
(ASSETS/'turtle-van.json').write_text(json.dumps(asset,separators=(',',':')),encoding='utf-8')

# Save an editable master plus portable GLB, before adding studio props.
bpy.ops.wm.save_as_mainfile(filepath=str(ASSETS/'turtle-van.blend'))
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'turtle-van.glb'),export_format='GLB',export_yup=True)

GROUP='studio'
floor=box('Studio ground',(0,0,-.055),(200,200,.1),'cream',0)
world=bpy.context.scene.world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.23,.27,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.5
def aim(o,pt): o.rotation_euler=(Vector(pt)-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power,size in [('Key',(-4,3,7),1600,5),('Fill',(4,2,5),1050,4),('Rim',(1,-5,6),1900,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object; o.name=name; o.data.energy=power; o.data.shape='DISK'; o.data.size=size; aim(o,(0,0,1.2))
bpy.ops.object.camera_add(location=(-6,8,5)); cam=bpy.context.object; bpy.context.scene.camera=cam
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.cycles.use_denoising=True; scene.render.resolution_x=1400; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'
cam.data.type='ORTHO'; cam.data.ortho_scale=7.1; aim(cam,(0,0,1.7))
scene.render.filepath=str(PREVIEWS/'exterior.png'); bpy.ops.render.render(write_still=True)
cam.location=(5,-7,4.5); aim(cam,(0,-.2,1.6)); scene.render.filepath=str(PREVIEWS/'rear.png'); bpy.ops.render.render(write_still=True)
cam.data.type='PERSP'; cam.data.lens=19; cam.data.clip_start=.025
cam.location=(-.47*WIDEN,.40,1.88); aim(cam,(-.47*WIDEN,6,1.88-5.6*math.tan(math.radians(6))))
scene.render.resolution_x=1600; scene.render.resolution_y=900
scene.render.filepath=str(PREVIEWS/'cockpit.png'); bpy.ops.render.render(write_still=True)
print('TURTLE VAN EXPORT: '+str(sum(len(p['triangles'])//3 for p in parts.values()))+' triangles, '+str(len(parts))+' material/group meshes')
