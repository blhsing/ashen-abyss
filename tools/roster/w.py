import json,sys
sys.stdout.reconfigure(encoding="utf-8")
d=json.load(open(r'Y:\game\data\bestiary.json',encoding='utf-8'))
for r in d['regions']:
  print(r['id'], ' '.join(f"{f['id']}:{f['weight']}({len(f['types'])})" for f in r['families']))
  for dg in r['dungeons']: print('   ', dg['id'], dg['families'])
