"""Compare stock and custom no-op initialization reports; never qualifies physics."""
import argparse
import json
import math

def differences(a,b,path=''):
    if isinstance(a,dict) and isinstance(b,dict):
        for k in sorted(a.keys()|b.keys()):
            if k not in a or k not in b: yield path+'/'+k+': missing'
            else: yield from differences(a[k],b[k],path+'/'+k)
    elif isinstance(a,list) and isinstance(b,list):
        if len(a)!=len(b): yield path+': different length'
        else:
            for i,(x,y) in enumerate(zip(a,b)): yield from differences(x,y,f'{path}/{i}')
    elif isinstance(a,(int,float)) and isinstance(b,(int,float)):
        if not math.isfinite(a) or not math.isfinite(b) or not math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-6): yield f'{path}: {a} -> {b}'
    elif a in ('NaN','Infinity','-Infinity') or b in ('NaN','Infinity','-Infinity'):
        yield f'{path}: non-finite diagnostic field'
    elif a!=b: yield f'{path}: {a!r} -> {b!r}'

if __name__=='__main__':
    p=argparse.ArgumentParser(); p.add_argument('stock'); p.add_argument('custom'); args=p.parse_args()
    stock=json.load(open(args.stock)); custom=json.load(open(args.custom))
    if not stock.get('gameAssemblySha256') or stock.get('gameAssemblySha256')!=custom.get('gameAssemblySha256'):
        raise SystemExit('Reports must identify the same game assembly hash.')
    identity=custom.get('identity')
    if stock.get('schemaVersion')!=1 or custom.get('schemaVersion')!=1 or stock.get('identity') is not None or not isinstance(identity,dict):
        raise SystemExit('Use one stock report and one custom report in diagnostic schema 1.')
    if stock['donorPrefab']!=custom['donorPrefab'] or identity.get('physicsApplied') is not False:
        raise SystemExit('Use matching donor and custom reports with physical overrides disabled.')
    required={'00-spawn','10-setup','30-dynamics','40-drivetrain','20-wheel-FRONT_LEFT','20-wheel-FRONT_RIGHT','20-wheel-REAR_LEFT','20-wheel-REAR_RIGHT'}
    for report in (stock,custom):
        if not required.issubset(report['phases']): raise SystemExit('Incomplete initialization trace; no comparison acceptance.')
    diff=list(differences(stock['phases'],custom['phases']))
    print('\n'.join(diff) if diff else 'MATCH: captured no-op fields agree; solver, shared assets and save guards still require runtime checks.')
    raise SystemExit(bool(diff))
