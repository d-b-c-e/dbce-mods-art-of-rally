import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'artifacts/rig-vehicles/turtle-van-real-scale'
a=json.loads((p/'model.json').read_text()); origins={o['name']:o['position'] for o in a['origins']}
def vertices(parts):
 for part in parts:
  v=part['vertices']; o=origins[part['group']]
  for i in range(0,len(v),3): yield [v[i+j]+o[j] for j in range(3)]
def bounds(parts):
 vs=list(vertices(parts)); lo=[min(v[i] for v in vs) for i in range(3)]; hi=[max(v[i] for v in vs) for i in range(3)]
 return dict(min=lo,max=hi,size=[hi[i]-lo[i] for i in range(3)])
b=bounds(a['parts']); wb=origins['wheelFL'][2]-origins['wheelRL'][2]; track=origins['wheelFR'][0]-origins['wheelFL'][0]
wheel=bounds([p for p in a['parts'] if p['group']=='wheelFL']); eye=a['camera']
report=dict(axes='runtime: X right, Y up, Z forward',bounds=b,length=b['size'][2],width_including_mirrors=b['size'][0],height_including_turret=b['max'][1],paint_bounds=bounds([p for p in a['parts'] if p['material']=='paint']),wheelbase=wb,front_track=track,rear_track=origins['wheelRR'][0]-origins['wheelRL'][0],nominal_tyre_radius=.35,tyre_geometry_radius_y=wheel['size'][1]/2,tyre_geometry_radius_z=wheel['size'][2]/2,driver_eye_runtime=eye,driver_eye_gltf=[-eye[0],eye[1],eye[2]],triangles=sum(len(p['triangles'])//3 for p in a['parts']))
assert abs(wb-2.794)<1e-5 and abs(track-1.72)<1e-5 and abs(eye[1]-1.65)<1e-5
assert abs(wheel['size'][1]-wheel['size'][2])<.004
assert report['triangles']<100000
(p/'measurements.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
