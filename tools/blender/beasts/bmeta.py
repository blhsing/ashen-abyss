"""Merge the per-creature meta files into Y:\game\assets\chars\rigs_bf_meta.json (plain python, no Blender):
  python.exe -I bmeta.py [<extra meta dir> ...]
Only models whose .glb exists in Y:\game\assets\chars are written. Later directories override earlier ones."""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = r"Y:\game\assets\chars"
dirs = [os.path.join(HERE, "out")] + [a for a in sys.argv[1:] if not a.startswith('--')]
KEEP = ('walk_speed', 'run_speed', 'attack1_hit_time', 'attack2_hit_time', 'attack_reach', 'radius', 'height', 'fly_height',
        'hover_in_model', 'buried', 'clips', 'family', 'rig', 'scale', 'triangles', 'bones', 'light', 'enlarged', 'wheel_radius',
        'wheel_center_run')
# soft lights the game hangs on glowing creatures (at most 8 live at once in the game): used when a creature's own meta
# has no 'light' entry. Heights / ranges in metres, model space.
LIGHTS = {
    'mon_cf_flicker': dict(color=[0.45, 0.75, 1.0], energy=0.7, range=2.6, height=0.45),
    'mon_lamp_lure': dict(color=[1.0, 0.62, 0.22], energy=1.0, range=4.0, height=1.75),
    'mon_dusk_gadfly': dict(color=[0.72, 0.9, 0.2], energy=0.6, range=2.4, height=1.25),
    'mon_fiend_wing': dict(color=[1.0, 0.4, 0.08], energy=0.7, range=3.2, height=1.7),
}
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
        if model in LIGHTS:
            e.setdefault('light', LIGHTS[model])
        res[model] = e
# The file is exactly {model: {...}} (the contract). Field meanings:
#   walk_speed / run_speed  ground speed (m/s, model space, before MonsterDef.Scale) at which Walk / Run have no foot
#                           sliding; 0 = does not travel (turret)
#   attack1_hit_time / attack2_hit_time  seconds into the clip;  attack_reach  how far in front of the origin the strike
#                           lands (m, model space);  radius / height  body bounds in Idle (m, model space)
#   hover_in_model          the hover height is already inside the rest pose and every clip (fly_height = height of the
#                           body centre above the origin): do not add a fly offset in the game
#   buried                  metres of body below the ground in every clip (burrowers);  clips  clip lengths in seconds
#   light                   optional {color [r,g,b], energy, range (m), height (m, model space)}: a soft shadowless light
#                           the creature carries (a pool of light on the ground under it); off when it dies
doc = res
with open(os.path.join(OUT, "rigs_bf_meta.json"), 'w', encoding='utf8') as f:
    json.dump(doc, f, indent=1)
print("wrote rigs_bf_meta.json with %d models" % len(res))
