"""Construct FJ62 cabin experiment; upstream exterior retained, cabin inferred."""
import copy,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
UP=ROOT.parents[1]/'results/3d-pipeline/construct'
OUT=ROOT/'artifacts/rig-vehicles/fj62-cabin'
OUT.mkdir(parents=True,exist_ok=True)
s=json.loads((UP/'examples/toyota-land-cruiser-fj62/scene.json').read_text())
(OUT/'upstream.scene.json').write_text(json.dumps(s,indent=2))
s['id']='fj62-cabin-study'; s['title']='FJ62 cabin study - inferred interior'
s['subject']['summary']+=' Cabin study adds inferred panels, seating and controls; no factory cabin measurements.'
s['sources'] += [dict(id='interior-photo',kind='reference_image',title='Curated OVR LND 1989 FJ62 restored interior photo 7734 (layout only; restored car)',ref='https://curatedovrlnd.com/products/1989-toyota-land-cruiser-restored',rights='reference_only')]
s['facts'].append(dict(key='driver-eye',value='x 0.38, y 1.57, z -0.50 m; glTF +X left, +Y up, +Z forward',certainty='inferred',source='interior-photo',note='Designed seated eye, not an SAE measurement'))
s['authoring']=dict(tier='standard',research='done',sources=['interior-photo','specs-oulu'],review_rounds=1,outcome='self-reviewed',next=['Validate seating on owner rig; cabin and eye are estimates'])
p={p['id']:p for p in s['parts']}
# Thin side shells keep the blueprint wheel wells without sealing the cabin with a roof-height loft.
sections=p['body']['shape']['sections']
p['body']['shape']={'type':'loft','sections':[[r[0],.018,r[2],r[3],.018] for r in sections], 'round':0}
p['body']['at']=[.88,0,0]; p['body']['mirror']='x'
# Keep id for upstream annotation compatibility but turn the glasshouse into an upper rear rail.
p['glasshouse']['shape']={'type':'box','size':[1.62,.045,.035]}; p['glasshouse']['at']=[0,1.75,-2.47]
p['glasshouse']['look']={'fill':'dark','weight':.7}
p['glasshouse']['stage']=4
p['windscreen-frame']['stage']=4
def add(id,shape,at=(0,0,0),rotate=None,mirror=None,role='detail'):
 q=dict(id=id,role=role,shape=shape,at=list(at),certainty='inferred',source='interior-photo',stage=4,look={'fill':'dark','weight':.7})
 if rotate: q['rotate']=rotate
 if mirror: q['mirror']=mirror
 s['parts'].append(q); return q
def box(id,size,at,**kw):return add(id,{'type':'box','size':size},at,**kw)
def tube(id,points,r=.015,**kw):return add(id,{'type':'tube','path':points,'radius':r,'sides':8,'samples':8,'caps':True},**kw)
box('cabin-floor',[1.73,.055,3.0],[0,.68,-.94],role='structure')
box('cabin-front-bulkhead',[1.72,.55,.035],[0,.96,.575],role='structure')
box('cabin-rear-wall',[1.73,.56,.03],[0,.965,-2.49],role='structure')
box('cabin-door-trim',[.025,.45,.94],[.847,1.0,-.01],mirror='x')
box('cabin-armrest',[.085,.055,.33],[.79,1.12,-.05],mirror='x')
for z in (-.47,-1.33,-2.46):box('cabin-window-post-'+str(z),[.045,.53,.055],[.83,1.51,z],mirror='x',role='structure')
tube('cabin-a-pillar',[[.86,1.256,.585],[.81,1.78,.12]],.025,mirror='x',role='structure')
box('cabin-windshield-glass',[1.51,.66,.008],[0,1.515,.355],rotate=[-42,0,0])
box('cabin-side-glass',[.008,.43,2.34],[.835,1.49,-1.22],mirror='x')
box('seat-cushion-front',[.50,.13,.52],[.38,.87,-.39],mirror='x')
box('seat-base-front',[.43,.13,.43],[.38,.74,-.39],mirror='x')
box('seat-headrest-front',[.29,.18,.13],[.38,1.52,-.66],mirror='x')
box('seat-cushion-rear',[1.38,.13,.52],[0,.87,-1.29])
box('cabin-console',[.28,.23,.78],[0,.82,-.24])
box('cabin-radio',[.26,.12,.04],[0,1.205,.175])
box('cabin-radio-face',[.19,.06,.008],[0,1.21,.149])
box('cabin-heater-controls',[.25,.07,.035],[0,1.09,.19])
box('cabin-instrument-binnacle',[.49,.19,.09],[.4,1.26,.19])
for i,x in enumerate((.27,.46)):
 add('cabin-gauge-'+str(i),{'type':'cylinder','radius':.065,'height':.012,'segments':24,'caps':True},[x,1.27,.14],rotate=[-90,0,0])
 tube('cabin-needle-'+str(i),[[x,1.27,.124],[x+.025,1.31,.124]],.003)
for i,x in enumerate((-.71,-.21,.72)):
 add('cabin-vent-'+str(i),{'type':'cylinder','radius':.044,'height':.018,'segments':20,'caps':True},[x,1.24,.18],rotate=[-90,0,0])
tube('cabin-passenger-grab',[[-.65,1.17,.215],[-.65,1.17,.10],[-.29,1.17,.10],[-.29,1.17,.215]],.014)
tube('cabin-steering-column',[[.38,1.18,.27],[.38,1.36,.12]],.025)
for i in range(3):
 angle=i*math.tau/3
 tube('cabin-wheel-spoke-'+str(i),[[.38,1.36,.12],[.38+.18*math.cos(angle),1.36+.09*math.sin(angle),.12+.156*math.sin(angle)]],.013)
box('cabin-wheel-hub',[.12,.07,.08],[.38,1.36,.12])
for name,x,z,h in [('shift',.04,-.13,1.03),('transfer',.12,.02,.97)]:
 tube('cabin-'+name+'-lever',[[x,.87,z],[x,h,z+.06]],.012)
 add('cabin-'+name+'-knob',{'type':'sphere','radius':.025,'segments':12},[x,h,z+.06])
(OUT/'scene.json').write_text(json.dumps(s,indent=2))
m=json.loads((UP/'examples/light-to-metal/maps/fj62.json').read_text())
m['materials']=[dict(parts='cabin-*-glass',name='cabin glass',color='#a8c0c6',finish='glass'),dict(parts=['cabin-gauge-*','cabin-radio-face','cabin-vent-*'],name='black instruments',color='#141719',finish='plastic'),dict(parts=['cabin-needle-*'],name='gauge needles',color='#ddddcb',finish='plastic'),dict(parts=['cabin-*'],name='charcoal interior',color='#34383a',finish='plastic')]+m['materials']
(OUT/'materials.json').write_text(json.dumps(m,indent=2))
print(OUT)



# Export and give glass a plain alpha fallback for lightweight rig renderers.
import subprocess,sys,struct,shutil
cli=UP/'construct_hologram/cli.py'
subprocess.run([sys.executable,str(cli),'export',str(OUT/'scene.json'),'--out',str(OUT/'fj62-cabin.glb'),'--materials',str(OUT/'materials.json'),'--wire','seams'],check=True)
path=OUT/'fj62-cabin.glb'; raw=path.read_bytes(); n,kind=struct.unpack_from('<II',raw,12)
assert raw[:4]==b'glTF' and kind==0x4E4F534A
asset=json.loads(raw[20:20+n]); changed=0
for material in asset['materials']:
 ext=material.get('extensions',{})
 if 'KHR_materials_transmission' in ext:
  ext.pop('KHR_materials_transmission'); ext.pop('KHR_materials_ior',None)
  material['alphaMode']='BLEND'; material['doubleSided']=True
  material['pbrMetallicRoughness']['baseColorFactor']=[.65,.8,.85,.10]
  material['pbrMetallicRoughness']['roughnessFactor']=.8
  material['extras']['rig_glass']='10-percent alpha; transmission removed for simple renderer compatibility'
  changed+=1
blob=json.dumps(asset,separators=(',',':')).encode(); blob+=b' '*((-len(blob))%4)
remaining=raw[20+n:]; packed=struct.pack('<4sII',b'glTF',2,20+len(blob)+len(remaining))+struct.pack('<II',len(blob),0x4E4F534A)+blob+remaining
assert len(packed)==struct.unpack_from('<I',packed,8)[0] and changed>0
path.write_bytes(packed)
for name in ('LICENSE','NOTICE'):shutil.copyfile(UP/name,OUT/(name+'-Construct.txt'))
print('GLB alpha-glass material count:',changed)
