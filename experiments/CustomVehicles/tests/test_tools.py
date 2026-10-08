"""Offline creator tool integration: real CLI/archives, palette, lineage reports."""
import copy
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
CLI=ROOT/'tools'/'VehicleTool'/'bin'/'Release'/'net48'/'VehicleTool.exe'
EXAMPLE=ROOT/'examples'/'trail-scout'
spec=importlib.util.spec_from_file_location('compare_instances',ROOT/'tools'/'compare_instances.py')
compare=importlib.util.module_from_spec(spec); spec.loader.exec_module(compare)

class CreatorTools(unittest.TestCase):
    def test_data_only_archive_and_existing_file(self):
        with tempfile.TemporaryDirectory(prefix='vehicle-tool-') as scratch:
            folder=Path(scratch)/'car'; shutil.copytree(EXAMPLE,folder)
            (folder/'untrusted.dll').write_text('not an executable; packing exclusion fixture')
            archive=Path(scratch)/'car.zip'
            result=subprocess.run([str(CLI),'pack',str(folder/'vehicle.json'),str(archive)],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            with zipfile.ZipFile(archive) as z:
                self.assertEqual(set(z.namelist()),{'dbce.trail-scout/'+f for f in ('vehicle.json','model.json','palette.png','preview.png')})
                self.assertEqual(z.read('dbce.trail-scout/model.json'),(folder/'model.json').read_bytes())
            digest=hashlib.sha256(archive.read_bytes()).hexdigest()
            result=subprocess.run([str(CLI),'pack',str(folder/'vehicle.json'),str(archive)],capture_output=True,text=True)
            self.assertNotEqual(result.returncode,0)
            self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(),digest)
            self.assertFalse(archive.with_suffix('.zip.partial').exists())

    def test_invalid_package_produces_no_archive(self):
        with tempfile.TemporaryDirectory(prefix='vehicle-tool-') as scratch:
            folder=Path(scratch); (folder/'vehicle.json').write_text('{broken')
            result=subprocess.run([str(CLI),'pack',str(folder/'vehicle.json'),str(folder/'out.zip')],capture_output=True,text=True)
            self.assertNotEqual(result.returncode,0)
            self.assertFalse((folder/'out.zip').exists())
            self.assertFalse((folder/'out.zip.partial').exists())

    def test_srgb_palette_matches_linear_materials(self):
        for folder in (ROOT/'examples').iterdir():
            model=json.loads((folder/'model.json').read_text())
            materials=model['materials']; side=math.ceil(math.sqrt(len(materials)))
            with Image.open(folder/'palette.png') as im:
                for i,mat in enumerate(materials):
                    # Atlas UV origin is bottom-left; PNG rows are top-down.
                    got=im.getpixel((int((i%side+.5)*im.width/side),im.height-1-int((i//side+.5)*im.height/side)))
                    want=[round(255*(12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055)) for v in mat['color'][:3]]
                    self.assertTrue(all(abs(x-y)<=1 for x,y in zip(got,want)),(folder.name,mat['name'],got,want))

    def test_report_compare_detects_nested_solver_changes(self):
        a={'phases':{'init':{'wheels':[{'radius':.38}], 'mass':1350}}}
        b=copy.deepcopy(a)
        self.assertEqual(list(compare.differences(a,b)),[])
        b['phases']['init']['wheels'][0]['radius']=.49
        self.assertEqual(len(list(compare.differences(a,b))),1)
        del b['phases']['init']['mass']
        self.assertEqual(len(list(compare.differences(a,b))),2)
        self.assertTrue(list(compare.differences({'cache':'NaN'},{'cache':'NaN'})))
        self.assertTrue(list(compare.differences(float('inf'),float('inf'))))

    @unittest.skipUnless(os.name=='nt','Windows package boundary')
    def test_link_boundary(self):
        with tempfile.TemporaryDirectory(prefix='vehicle-link-test-') as scratch:
            root=Path(scratch); actual=root/'library'; actual.mkdir(); shutil.copytree(EXAMPLE,actual/'car')
            link=root/'linked-library'
            def quote(value): return "'"+str(value).replace("'","''")+"'"
            command=f'New-Item -ItemType Junction -Path {quote(link)} -Target {quote(actual)} | Out-Null'
            result=subprocess.run(['powershell','-NoProfile','-Command',command],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            try:
                # A trusted, linked library ancestor is allowed.
                result=subprocess.run([str(CLI),'validate',str(link/'car'/'vehicle.json')],capture_output=True,text=True)
                self.assertEqual(result.returncode,0,result.stderr)
            finally: os.rmdir(link)  # Remove junction itself, never recurse through it.
            link=root/'linked-car'
            command=f'New-Item -ItemType Junction -Path {quote(link)} -Target {quote(actual/"car")} | Out-Null'
            result=subprocess.run(['powershell','-NoProfile','-Command',command],capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            try:
                result=subprocess.run([str(CLI),'validate',str(link/'vehicle.json')],capture_output=True,text=True)
                self.assertNotEqual(result.returncode,0)
                self.assertIn('Linked',result.stderr)
            finally: os.rmdir(link)
            self.assertTrue((actual/'car'/'vehicle.json').exists())

if __name__=='__main__': unittest.main(verbosity=2)
