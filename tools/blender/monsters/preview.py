"""Render previews of an exported creature .glb:
   blender -b --factory-startup --python preview.py -- <id> [act:t act:t ...] [--back] [--close=bone]
Writes Y:\\shots\\chars\\<id>_rest.png, _game.png, _poseA.png, _poseB.png (+ _back.png) and <id>_combo.png (2x2)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

DEFAULT = {
    'sword': ["Sword_Idle:0.3", "Sword_Regular_A:0.55", "Zombie_Walk_Fwd_Loop:0.3"],
    'heavy': ["Sword_Idle:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"],
    'zombie': ["Zombie_Idle_Loop:0.3", "Zombie_Scratch:0.45", "Zombie_Walk_Fwd_Loop:0.3"],
    'caster': ["Idle_Loop:0.3", "Spell_Simple_Shoot:0.5", "Walk_Loop:0.3"],
    'thrower': ["Sword_Idle:0.3", "OverhandThrow:0.3", "Jog_Fwd_Loop:0.3"],
}
KIND = {
    'mon_skeleton': 'sword', 'mon_skeleton_thrower': 'thrower', 'mon_husk': 'zombie', 'mon_husk_bloated': 'zombie',
    'mon_cultist': 'sword', 'mon_cult_caster': 'caster', 'mon_knight': 'heavy', 'mon_wraith': 'caster',
    'mon_fiend': 'zombie', 'mon_brute': 'heavy', 'boss_gravedigger': 'heavy', 'boss_bishop': 'caster',
    'boss_osaros': 'heavy', 'mon_frostbound': 'sword', 'mon_rime_stalker': 'zombie', 'boss_rimelord': 'heavy',
    'mon_mire_hag': 'caster', 'mon_rotspawn': 'zombie', 'boss_mire_mother': 'caster', 'mon_sand_revenant': 'sword',
    'mon_brass_sentinel': 'heavy', 'boss_sand_tyrant': 'heavy', 'mon_choir': 'caster', 'boss_grand_hush': 'heavy',
}

args = sys.argv[sys.argv.index('--') + 1:]
cid = args[0]
specs = [a for a in args[1:] if not a.startswith('--')]
flags = [a for a in args[1:] if a.startswith('--')]
if not specs:
    specs = DEFAULT[KIND.get(cid, 'sword')]
specs = [(s.split(':')[0], float(s.split(':')[1]) if ':' in s else 0.5) for s in specs]

M.reset_scene()
acts = S.load_actions()
arm, meshes = S.import_creature(os.path.join(M.OUT_DIR, cid + ".glb"))
res = 700
S.setup_render(res, res)
S.setup_stage()
cam = S.make_camera()
out = []


def shot(tag, act, t, yaw, pitch, lens=70.0):
    S.set_pose(arm, acts[act] if act else None, t)
    S.frame(cam, meshes, yaw, pitch, lens)
    p = os.path.join(M.SHOT_DIR, "%s_%s.png" % (cid, tag))
    S.render(p)
    return p


out.append(shot("rest", None, 0, 28, 10))
out.append(shot("game", specs[0][0], specs[0][1], 35, 52, 100.0))
out.append(shot("poseA", specs[1][0], specs[1][1], 30, 14))
out.append(shot("poseB", specs[2][0], specs[2][1], -40, 14))
S.combo(out, os.path.join(M.SHOT_DIR, cid + "_combo.png"), cols=2)
extra = []
if '--back' in flags:
    extra.append(shot("back", None, 0, 152, 12))
    extra.append(shot("backpose", specs[0][0], specs[0][1], 150, 20))
if '--face' in flags:
    S.set_pose(arm, None, 0)
    P = S.posed_points(meshes)
    top = P[P[:, 2] > P[:, 2].max() - 0.42]
    top = top[np.abs(top[:, 0]) < 0.3]
    for k, (yw, pt) in enumerate(((22, 4), (-75, 8))):
        S.frame(cam, [], yw, pt, 70.0, pts=top)
        p = os.path.join(M.SHOT_DIR, "%s_face%d.png" % (cid, k))
        S.render(p); extra.append(p)
if '--death' in flags:
    extra.append(shot("death", "Death01", 0.55, 30, 30))
for s in specs[3:]:
    extra.append(shot("pose_" + s[0], s[0], s[1], 30, 14))
for f in flags:
    if f.startswith('--close='):
        # close-up around a bone in the first pose and the attack pose
        bone = f.split('=')[1]
        for k, (a, t) in enumerate(specs[:2]):
            S.set_pose(arm, acts[a], t)
            h = np.array(arm.pose.bones[bone].head)
            pts = np.array([h + 0.2, h - 0.2])
            yw = (-40 if bone.endswith('_r') else 35) if k == 0 else (-30 if not bone.endswith('_l') else 40)
            S.frame(cam, [], yw, 12, 70.0, pts=pts)
            p = os.path.join(M.SHOT_DIR, "%s_close_%s%d.png" % (cid, bone, k))
            S.render(p); extra.append(p)
if extra:
    while len(extra) % 2:
        extra.append(extra[-1])
    S.combo(extra, os.path.join(M.SHOT_DIR, cid + "_combo2.png"), cols=2)
M.log("PREVIEW OK", cid)
