"""Run in background Blender with Trail Scout loaded; never save the scene."""
import hashlib
from pathlib import Path
import sys
import tempfile
import bpy

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'creator'))
from export_vehicle import export

def hashes():
    return {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'examples'/'trail-scout').iterdir() if p.is_file()}

before=hashes()
report=export(ROOT/'examples'/'trail-scout',True)
assert report['triangles']==26336 and len(report['trianglesByGroup'])==6
assert before==hashes(), 'dry run changed a file'
with tempfile.TemporaryDirectory(prefix='vehicle-export-check-') as scratch:
    try: export(scratch,True)
    except ValueError as ex: assert 'Copy an examples/' in str(ex)
    else: raise AssertionError('missing manifest accepted')
marker=bpy.data.objects['driver_eye']; bpy.data.objects.remove(marker,do_unlink=True)
try: export(ROOT/'examples'/'trail-scout',True)
except ValueError as ex: assert 'driver_eye' in str(ex) and 'Missing empty markers' in str(ex)
else: raise AssertionError('missing marker accepted')
assert before==hashes(), 'failed export changed a file'
print('PASS: Blender dry run, per-group counts, missing-manifest/marker diagnostics and no writes.')
