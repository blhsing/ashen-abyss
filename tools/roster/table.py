import json,sys,os
sys.stdout.reconfigure(encoding="utf-8")
d=json.load(open(r'Y:\game\data\bestiary.json',encoding='utf-8'))
for t in d['types']:
  ex=os.path.exists(r'Y:\game\assets\chars\%s.glb'%t['model'])
  print(f"{t['id']:20} {t['family']:8} {t['role'][:22]:22} {t['ai']:34} {t['rig']} sc{t['scale']:<4} sp{t['speed']:<4} hp{t['hp']:<4} w{t['spawn_weight']:<4} {'M' if ex else '-'} {','.join(t['regions'])}")
print(len(d['types']))
