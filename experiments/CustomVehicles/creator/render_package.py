"""Reconstruct the exported package and atlas for offline visual QA.

This verifies exported geometry/UVs, not Unity runtime rendering or camera hooks.
blender --background --python render_package.py -- --package vehicle.json --output preview.png [--cockpit]
"""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

parser=argparse.ArgumentParser()
parser.add_argument('--package',required=True); parser.add_argument('--output',required=True)
parser.add_argument('--cockpit',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
path=Path(args.package).resolve(); manifest=json.loads(path.read_text())
asset=json.loads((path.parent/manifest['model']).read_text())
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def xyz(values): return (values[0],values[2],values[1])
def triples(values): return [xyz(values[i:i+3]) for i in range(0,len(values),3)]
origins={o['name']:xyz(o['position']) for o in asset['origins']}
texture=bpy.data.images.load(str(path.parent/manifest['texture']))
texture.colorspace_settings.name='sRGB'
materials={}
for info in asset['materials']:
    m=bpy.data.materials.new(info['name']); m.use_nodes=True
    tree=m.node_tree; p=tree.nodes.get('Principled BSDF'); p.inputs['Roughness'].default_value=.82
    image=tree.nodes.new('ShaderNodeTexImage'); image.image=texture; image.interpolation='Closest'
    tree.links.new(image.outputs['Color'],p.inputs['Base Color'])
    if info['name']=='glass':
        transparent=tree.nodes.new('ShaderNodeBsdfTransparent'); mix=tree.nodes.new('ShaderNodeMixShader')
        mix.inputs[0].default_value=.12
        tree.links.new(transparent.outputs[0],mix.inputs[1]); tree.links.new(p.outputs[0],mix.inputs[2])
        tree.links.new(mix.outputs[0],tree.nodes.get('Material Output').inputs['Surface'])
    materials[info['name']]=m
for i,part in enumerate(asset['parts']):
    vertices=triples(part['vertices']); indices=part['triangles']
    triangles=[(indices[t],indices[t+2],indices[t+1]) for t in range(0,len(indices),3)]
    mesh=bpy.data.meshes.new(f'{part["group"]}_{i}'); mesh.from_pydata(vertices,[],triangles); mesh.update()
    uv=mesh.uv_layers.new(name='Atlas UV')
    for poly in mesh.polygons:
        poly.use_smooth=True
        for loop in poly.loop_indices:
            idx=mesh.loops[loop].vertex_index; uv.data[loop].uv=part['uv'][idx*2:idx*2+2]
    mesh.normals_split_custom_set_from_vertices(triples(part['normals']))
    obj=bpy.data.objects.new(mesh.name,mesh); bpy.context.collection.objects.link(obj)
    obj.location=origins[part['group']]; mesh.materials.append(materials[part['material']])

bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.04))
floor=bpy.data.materials.new('Studio floor'); floor.diffuse_color=(.24,.27,.29,1); bpy.context.object.data.materials.append(floor)
world=bpy.context.scene.world; world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.20,.26,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.6
def aim(obj,point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
for pos,power,size in [((-4,5,7),1300,5),((4,2,5),850,4),((1,-5,6),1600,4)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    light=bpy.context.object; light.data.energy=power; light.data.size=size; aim(light,(0,0,1.2))
bpy.ops.object.camera_add(location=(-6,8,5))
cam=bpy.context.object; scene=bpy.context.scene; scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
scene.view_settings.view_transform='AgX'; scene.render.image_settings.file_format='PNG'
scene.render.resolution_percentage=100
if args.cockpit:
    cam.data.type='PERSP'; cam.data.lens=20; cam.data.clip_start=.025; cam.location=xyz(asset['camera'])
    aim(cam,cam.location+Vector((0,6,-6*math.tan(math.radians(6)))))
    scene.render.resolution_x=1400; scene.render.resolution_y=788
else:
    cam.data.type='ORTHO'; cam.data.ortho_scale=7.1 if manifest['id']=='dbce.turtle-van' else 5.5
    aim(cam,(0,0,1.65 if manifest['id']=='dbce.turtle-van' else 1.05))
    scene.render.resolution_x=1400; scene.render.resolution_y=1100
out=Path(args.output).resolve(); out.parent.mkdir(parents=True,exist_ok=True)
scene.render.filepath=str(out); bpy.ops.render.render(write_still=True)
print('PACKAGE RECONSTRUCTED: '+str(sum(len(p['triangles'])//3 for p in asset['parts']))+' triangles')
