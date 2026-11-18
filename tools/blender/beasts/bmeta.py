"""Merge the per-creature meta files into Y:\game\assets\chars\rigs_bf_meta.json (plain python, no Blender):
  python.exe -I bmeta.py [<extra meta dir> ...]
Only models whose .glb exists in Y:\game\assets\chars are written. Later directories override earlier ones."""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = r"Y:\game\assets\chars"
dirs = [os.path.join(HERE, "out")] + [a for a in sys.argv[1:] if not a.startswith('--')]
KEEP = ('walk_speed', 'run_speed', 'attack1_hit_time', 'attack2_hit_time', 'attack_reach', 'radius', 'height', 'fly_height',
        'hover_in_model', 'buried', 'clips', 'family', 'rig', 'scale', 'triangles', 'bones')
res = {}
for d in dirs:
    if not os.path.isdir(d):
        continue
    for f in sorted(os.listdir(d)):
        if not f.endswith('.meta.json'):
            continue
        m = json.load(open(os.path.join(d, f), encoding='utf8'))
        model = m['model']
        if not os.path.isfile(os.path.join(OUT, model + ".glb")):
            continue
        e = {k: m[k] for k in KEEP if k in m}
        e.setdefault('hover_in_model', False)
        res[model] = e
# The file is exactly {model: {...}} (the contract). Field meanings:
#   walk_speed / run_speed  ground speed (m/s, model space, before MonsterDef.Scale) at which Walk / Run have no foot
#                           sliding; 0 = does not travel (turret)
#   attack1_hit_time / attack2_hit_time  seconds into the clip;  attack_reach  how far in front of the origin the strike
#                           lands (m, model space);  radius / height  body bounds in Idle (m, model space)
#   hover_in_model          the hover height is already inside the rest pose and every clip (fly_height = height of the
#                           body centre above the origin): do not add a fly offset in the game
#   buried                  metres of body below the ground in every clip (burrowers);  clips  clip lengths in seconds
doc = res
with open(os.path.join(OUT, "rigs_bf_meta.json"), 'w', encoding='utf8') as f:
    json.dump(doc, f, indent=1)
print("wrote rigs_bf_meta.json with %d models" % len(res))
