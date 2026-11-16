import sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glbinfo
for p in (r"Y:\assets_src\q\UAL1.glb", r"Y:\game\assets\chars\anim_lib.glb"):
    j = glbinfo.read_gltf_json(p)
    for a in j['animations']:
        if a['name'] == 'Spell_Simple_Idle_Loop':
            c = collections.Counter()
            for s in a['samplers']:
                acc = j['accessors'][s['input']]
                c[(round(acc['min'][0], 4), round(acc['max'][0], 4), acc['count'])] += 1
            print(p, dict(c))
