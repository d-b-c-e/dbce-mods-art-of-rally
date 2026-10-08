"""Adapt the accepted Turtle Van master to the generic authoring contract.

Does not change the original cosmetic experiment or installed mod.
"""
import json
import shutil
import sys
from pathlib import Path
import bpy

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT.parent/'TurtleVan'
OUT=ROOT/'examples'/'turtle-van'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'assets'/'turtle-van.blend'))
asset=json.loads((SOURCE/'assets'/'turtle-van.json').read_text())
for marker in asset['origins']+[dict(name='driver_eye',position=asset['camera'])]:
    name='driver_eye' if marker['name']=='driver_eye' else 'origin_'+marker['name']
    obj=bpy.data.objects.get(name)
    if obj is None:
        obj=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(obj)
    x,y,z=marker['position']; obj.location=(x,z,y); obj.empty_display_size=.12
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'artifacts'/'turtle-van-creator.blend'))
sys.path.insert(0,str(Path(__file__).parent))
from export_vehicle import export
export(OUT)
shutil.copy2(SOURCE/'previews'/'exterior.png',OUT/'preview.png')
