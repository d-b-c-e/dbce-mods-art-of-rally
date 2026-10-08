"""Blender 5.x: export a data-only custom vehicle, no Unity editor required.

blender car.blend --background --python export_vehicle.py -- --output folder
The destination must contain vehicle.json (see examples). Every exported mesh
has a custom 'group' property. Empty markers: origin_<group> and driver_eye.
Author X right, Y forward, Z up, in metres. Modifiers are baked on export.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

GROUPS = ('body', 'steering', 'wheelFL', 'wheelFR', 'wheelRL', 'wheelRR')

def runtime(v):
    return [round(v[0], 6), round(v[2], 6), round(v[1], 6)]

def linear_to_srgb(value):
    return 12.92*value if value <= .0031308 else 1.055*value**(1/2.4)-.055

def export(output, check=False):
    output = Path(output).resolve()
    if not (output/'vehicle.json').is_file():
        raise ValueError('Missing vehicle.json. Copy an examples/*/vehicle.json into the output folder first.')
    manifest = json.loads((output / 'vehicle.json').read_text(encoding='utf-8-sig'))
    for key in ('model', 'texture', 'preview'):
        value=manifest.get(key)
        if key=='preview' and value is None: continue
        if not isinstance(value,str) or Path(value).name != value or any(c in value for c in '/\\:'):
            raise ValueError(f'{key}: use a simple filename inside the package.')
        if value=='vehicle.json' or Path(value).suffix.lower() != ('.json' if key=='model' else '.png'):
            raise ValueError(f'{key}: use a separate '+('model .json' if key=='model' else '.png')+' file.')
    bpy.context.view_layer.update()
    required=['origin_'+g for g in GROUPS]+['driver_eye']
    missing=[name for name in required if bpy.data.objects.get(name) is None]
    if missing: raise ValueError('Missing empty markers: '+', '.join(missing))
    if any(bpy.data.objects[name].type!='EMPTY' for name in required):
        raise ValueError('All origin_* and driver_eye markers must be Empty objects.')
    origins = {g: bpy.data.objects['origin_' + g].matrix_world.translation.copy() for g in GROUPS}
    eye = bpy.data.objects['driver_eye'].matrix_world.translation
    objects = sorted((o for o in bpy.context.scene.objects if o.type in ('MESH', 'FONT', 'CURVE') and 'group' in o), key=lambda o:o.name)
    if not objects:
        raise ValueError('No meshes with a group property.')
    names = sorted({m.name for o in objects for m in o.data.materials if m is not None})
    if not names or len(names) > 64:
        raise ValueError('Use 1-64 named palette materials.')
    colors = {}
    for name in names:
        mat = bpy.data.materials[name]
        bsdf = mat.node_tree.nodes.get('Principled BSDF') if mat.use_nodes else None
        colors[name] = list(bsdf.inputs['Base Color'].default_value if bsdf else mat.diffuse_color)
        # Glass transparency is implemented by the runtime's named glass material.
        # Atlas remains opaque to avoid premultiplied alpha fringes.
        colors[name][3] = 1.0
    side = math.ceil(math.sqrt(len(names)))
    cell = 32
    size = side * cell
    parts = {}
    indices = {}
    graph = bpy.context.evaluated_depsgraph_get()
    skipped = 0
    for obj in objects:
        group = obj['group']
        if group not in GROUPS:
            raise ValueError(f'{obj.name}: unknown group {group}')
        if obj.matrix_world.to_3x3().determinant() <= 0:
            raise ValueError(f'{obj.name}: apply mirrored/negative scale before export')
        evaluated = obj.evaluated_get(graph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
            for tri in mesh.loop_triangles:
                if tri.material_index >= len(mesh.materials) or mesh.materials[tri.material_index] is None:
                    raise ValueError(f'{obj.name}: every face needs a material')
                mat = mesh.materials[tri.material_index].name
                part = parts.setdefault((group, mat), dict(group=group, material=mat, vertices=[], normals=[], uv=[], triangles=[]))
                corners = [Vector(runtime(obj.matrix_world @ mesh.vertices[i].co - origins[group])) for i in tri.vertices]
                normal = (corners[2]-corners[0]).cross(corners[1]-corners[0])
                if normal.length_squared < 1e-20:
                    skipped += 1
                    continue
                normal.normalize()
                swatch = names.index(mat)
                uv = [(swatch % side + .5) / side, (swatch // side + .5) / side]
                corners_indices=[]
                index=indices.setdefault((group,mat),{})
                for corner, loop in zip(corners, tri.loops):
                    n = Vector(runtime((normal_matrix @ mesh.corner_normals[loop].vector).normalized()))
                    if n.dot(normal) <= .001:
                        n = normal
                    xyz=[round(v,6) for v in corner]; normal_values=[round(v,6) for v in n]
                    key=tuple(xyz+normal_values+uv)
                    if key not in index:
                        index[key]=len(part['vertices'])//3
                        part['vertices'].extend(xyz)
                        part['normals'].extend(normal_values)
                        part['uv'].extend(uv)
                    corners_indices.append(index[key])
                part['triangles'].extend(corners_indices[i] for i in (0,2,1))
        finally:
            evaluated.to_mesh_clear()
    asset = dict(version=1, units='metres', camera=runtime(eye),
                 origins=[dict(name=g, position=runtime(origins[g])) for g in GROUPS],
                 materials=[dict(name=n, color=colors[n]) for n in names],
                 parts=[p for p in parts.values() if p['triangles']])
    counts={g:sum(len(p['triangles'])//3 for p in asset['parts'] if p['group']==g) for g in GROUPS}
    if any(n==0 for n in counts.values()): raise ValueError('Every group needs geometry: '+str(counts))
    report = dict(triangles=sum(counts.values()), parts=len(asset['parts']), vertices=sum(len(p['vertices'])//3 for p in asset['parts']), trianglesByGroup=counts, skippedDegenerateFaces=skipped)
    if report['triangles']>100000 or report['parts']>128 or report['vertices']>300000:
        raise ValueError('Model exceeds runtime resource budget: '+str(report))
    if not check:
        # Blender Image.save writes these buffer values to PNG without a scene
        # display transform. Encode sRGB explicitly; runtime Texture2D is sRGB.
        atlas = bpy.data.images.new('Vehicle palette export', width=size, height=size, alpha=True)
        try:
            pixels=[]
            for y in range(size):
                for x in range(size):
                    i=(y//cell)*side+x//cell
                    linear=colors[names[i]] if i<len(names) else [1,0,1,1]
                    pixels.extend([linear_to_srgb(v) for v in linear[:3]]+[1])
            atlas.pixels=pixels; atlas.filepath_raw=str(output/manifest['texture']); atlas.file_format='PNG'; atlas.save()
        finally: bpy.data.images.remove(atlas)
        (output / manifest['model']).write_text(json.dumps(asset, separators=(',', ':'), allow_nan=False), encoding='utf-8')
    print('VEHICLE EXPORT ' + json.dumps(report))
    return report

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', required=True)
    parser.add_argument('--check', action='store_true', help='validate authoring inputs and print counts without writing files')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    try: export(args.output,args.check)
    except (ValueError,KeyError,RuntimeError) as error:
        print('VEHICLE EXPORT ERROR: '+str(error),file=sys.stderr)
        raise SystemExit(1)
