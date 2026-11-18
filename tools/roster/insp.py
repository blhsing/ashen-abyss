import json,sys
sys.stdout.reconfigure(encoding="utf-8")
d=json.load(open(r'Y:\game\data\bestiary.json',encoding='utf-8'))
print(list(d.keys()))
print(json.dumps(d['types'][5],ensure_ascii=False)[:1800])
print(list(d['regions'][0].keys()))
print(json.dumps(d['regions'][0]['dungeons'][0],ensure_ascii=False))
print(json.dumps(d['regions'][0]['families'][0],ensure_ascii=False)[:400])
for k in d:
  if k not in('types','reskins','regions','families','named'): print(k, json.dumps(d[k],ensure_ascii=False)[:1500])
