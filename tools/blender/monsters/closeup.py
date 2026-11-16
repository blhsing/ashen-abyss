"""Close-up renders of an exported creature around given points:
   blender -b --factory-startup --python closeup.py -- <id> [flags] <tag>=x,y,z,yaw,pitch,size[,Action:t] ...
Writes Y:\\shots\\chars\\<id>_<tag><suffix>.png for every spec and <id>_combo3<suffix>.png.

flags
  --rake            surface-relief mode: hard key light grazing the surfaces that face the camera (12 deg), weak fill,
                    HDRI reflections. Use it to judge normal-map / roughness detail (looks flat = too smooth).
  --graze=<deg>     grazing angle of the rake key (default 14)
  --src=<dir>       load <dir>\\<id>.glb instead of Y:\\game\\assets\\chars (e.g. out\\v1_glb for a "before" render)
  --suffix=<text>   appended to the output file names (e.g. _v1)
  --res=<px>        render size (default 700)
  --auto            add the standard crops of this creature (head/torso + material crop), see AUTO below
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

# standard relief crops: tag=x,y,z,yaw,pitch,size   (rest pose; "bust" = head + chest, "mat" = a material patch)
AUTO = {
    'mon_husk': ["bust=0,-0.05,1.42,24,6,0.30", "mat=0.02,-0.06,1.08,-28,4,0.17", "face=0,-0.1,1.56,20,4,0.12",
                 "macro=0.05,-0.08,1.12,-20,2,0.07"],
    'mon_husk_bloated': ["bust=0,-0.05,1.40,24,6,0.36", "mat=0.0,-0.2,1.05,-25,4,0.22"],
    'mon_knight': ["bust=0,-0.02,1.52,26,6,0.34", "mat=0.04,0.0,1.08,-30,4,0.20", "cape=0,0.3,1.0,165,5,0.26", "belt=0.02,-0.15,1.06,12,2,0.075"],
    'mon_cultist': ["bust=0,-0.02,1.55,24,6,0.32", "mat=0.05,-0.05,0.95,-28,4,0.22", "cloth=0.0,-0.2,0.72,14,2,0.11", "hem=0.1,-0.2,1.42,20,2,0.09"],
    'mon_cult_caster': ["bust=0,-0.02,1.58,24,6,0.34", "mat=0.05,-0.05,0.95,-28,4,0.22"],
    'mon_skeleton': ["bust=0,-0.02,1.50,24,6,0.30", "mat=0.0,-0.03,1.02,-28,4,0.19"],
    'mon_skeleton_thrower': ["bust=0,-0.02,1.50,24,6,0.32", "mat=0.0,-0.03,1.02,-28,4,0.19"],
    'mon_fiend': ["bust=0,-0.05,1.55,24,6,0.36", "mat=0.03,-0.06,1.12,-28,4,0.19"],
    'mon_brute': ["bust=0,-0.15,1.42,24,6,0.46", "mat=0.1,-0.2,1.0,-28,4,0.24"],
    'mon_wraith': ["bust=0,-0.02,1.62,24,6,0.36", "mat=0.05,-0.05,1.05,-28,4,0.24"],
    'mon_choir': ["bust=0,-0.02,1.66,24,6,0.36", "mat=0.05,-0.05,1.1,-28,4,0.24"],
    'mon_frostbound': ["bust=0,-0.03,1.50,24,6,0.34", "mat=0.03,-0.05,1.08,-28,4,0.20"],
    'mon_rime_stalker': ["bust=0,-0.1,1.38,24,6,0.36", "mat=0.03,-0.1,1.0,-28,4,0.20"],
    'mon_sand_revenant': ["bust=0,-0.03,1.50,24,6,0.32", "mat=0.03,-0.05,1.08,-28,4,0.19"],
}

args = sys.argv[sys.argv.index('--') + 1:]
cid = args[0]
flags = [a for a in args[1:] if a.startswith('--')]
specs = [a for a in args[1:] if not a.startswith('--')]


def flag(name, default=None):
    for f in flags:
        if f.startswith('--' + name + '='):
            return f.split('=', 1)[1]
    return default


rake = '--rake' in flags
src = flag('src', M.OUT_DIR)
suffix = flag('suffix', '')
res = int(flag('res', '700'))
graze = float(flag('graze', '20'))
if '--auto' in flags:
    specs = AUTO.get(cid, ["bust=0,-0.03,1.5,24,6,0.33", "mat=0.03,-0.05,1.05,-28,4,0.2"]) + specs

M.reset_scene()
acts = S.load_actions()
arm, meshes = S.import_creature(os.path.join(src, cid + ".glb"))
S.setup_render(res, res, samples=32)
if rake:
    S.setup_rake_stage()
else:
    S.setup_stage()
cam = S.make_camera()
out = []
for spec in specs:
    tag, vals = spec.split('=')
    q = vals.split(',')
    x, y, z, yaw, pitch, size = (float(v) for v in q[:6])
    act = None; t = 0.0
    if len(q) > 6:
        a = q[6].split(':')
        act = acts[a[0]]; t = float(a[1]) if len(a) > 1 else 0.5
    S.set_pose(arm, act, t)
    c = np.array([x, y, z])
    pts = np.array([c + size, c - size])
    S.frame(cam, [], yaw, pitch, 70.0, pts=pts)
    if rake:
        S.rake_lights(yaw, pitch, graze=graze, side=(-1.0 if yaw < 0 else 1.0))
    p = os.path.join(M.SHOT_DIR, "%s_%s%s.png" % (cid, tag, suffix))
    S.render(p)
    out.append(p)
if out:
    while len(out) % 2:
        out.append(out[-1])
    S.combo(out, os.path.join(M.SHOT_DIR, "%s_combo3%s.png" % (cid, suffix)), cols=2)
M.log("CLOSEUP OK", cid)
