"""asm.py -- data-driven creature assembler for rig A (the shared UAL humanoid armature, unmodified).

One SPEC (a python dict / JSON object, see specs_*.py) -> one finished creature:

    id       model id = file name in Y:\\game\\assets\\chars (mon_<type id>)
    family   bestiary family id (decides the contact sheet the creature appears on)
    scale    engine scale from bestiary.json (only sizes the creature on the family sheets)
    poses    ["Idle:t", "Attack:t", "Walk:t"]  clips for the preview shots
    body     {type: skeleton | flesh | shroud | from, ...}          -> BODIES in this file
    props    proportion / posture controls (bulk, chest, waist, hips, belly, depth, neck, head, shoulders, arm, forearm,
             hands, leg, calf, feet, arm_len[_l/_r], leg_len, hunch, stoop, lean, head_up, pose={raw pose_sculpt entries})
             NOTE: the skeleton is never changed, so everything a length or posture control moves is moved OFF its joint
             and swings on a wider arc when the clip rotates that joint.  Keep arm_len / leg_len within about +-15 %,
             hunch below ~30 and stoop below ~15; never lengthen an articulated rigid chain (skeleton hands: use
             forearm_len + a fixed claw).  Strong postures belong to the clip set (Zombie_*, Sprint_Loop ...).
    post     the same posture keys, but applied on top of a `from` body as well (rot / move only)
    looks    per-creature look overrides:  name: {param: value}            (preset of the same name + overrides)
                                           name: "preset" | ["preset", {..}] (another preset)
                                           name: [kind, slot, {..}]          (full definition, slot = skin|cloth|leather|
                                                                              metal|bone|rock|emis|spectral)
    emis     (r, g, b) emission colour of the generic emissive looks 'eye' and 'glow'
    dress    element dressing of every surface look: {frost: 0..1 | moss | sand | ash | soot | mud: 0..1, rust: 0..1}
    parts    [{p: <name in asm_parts.PARTS>, ...kwargs}]     outfit, growths, props
    weapons  [{w: <name in asm_weapons.WEAPONS>, hand: 'r' | 'l', ...kwargs}]
    script   optional module with extra(A) (bespoke sculpt on top of the spec), called after parts and weapons
    float    metres the lowest point hovers above the ground (legless creatures); default 0 = feet on the ground
    budget   triangle budget (default 15000; giants 22000)
    (parts: A.add(..., sway=(low bone, t)) makes a rigid piece hang -- see add();  body bind_w -- see body_flesh)
    jpeg     (base colour, ORM, normal) JPEG qualities, default (90, 88, 95): lower them when an all-rock creature exceeds 6 MB
    notes    free text stored in the build json

Everything is built in REST space around the unsculpted T-pose body with the kit helpers; `finish()` then applies the
proportion sculpt (props -> pose_sculpt dict incl. non-uniform bone scales) to EVERY part -- skinned parts through their
weights, rigid parts with the scale and pose of their bone -- so teeth, hats, straps and props stay seated whatever the
proportions are, and finally grounds the creature.  Parts that are already in final space are added with final=True.
"""
import math, os, sys, importlib, zlib
import numpy as np
import monlib as M
import looks as L
import kit_bones as KB
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join

HERE = os.path.dirname(os.path.abspath(__file__))
SCRATCH = os.path.join(HERE, "asm_out")
SLOT = dict(skin='skin_body', cloth='cloth_wear', leather='leather_gear', metal='metal_gear', bone='bone_parts',
            rock='rock_parts', emis='emis_glow', spectral='spectral_veil')


# ------------------------------------------------------------------------------------------------ dressed look kinds
def coated(base):
    """wrap a look kind with a settled coat (moss, sand, ash, soot, mud): on up-facing surfaces and in large patches.
    params: coat (colour), coat_amt, coat_lo (how steep a surface still catches it), coat_patch, coat_rough, coat_h"""
    def k(N, p):
        r = L.KINDS[base](N, p)
        cc = p.get('coat', (0.30, 0.29, 0.28))
        up = N.math('MULTIPLY', N.upfacing(p.get('coat_lo', 0.15), 0.9), N.mapr(N.noise(11, 4, 0.7), 0.2, 0.62))
        lo = p.get('coat_patch_lo', 0.5)
        patch = N.math('MULTIPLY', N.mapr(N.noise(p.get('coat_scale', 5.0), 4, 0.65), lo, lo + 0.16), p.get('coat_patch', 0.5))
        m = N.math('MULTIPLY', N.math('MAXIMUM', up, patch), p.get('coat_amt', 0.6), clamp=True)
        grain = N.noise(90, 2, 0.6)
        ccol = N.mix(N.mapr(grain, 0.3, 0.75, 0.0, 0.45), cc, N.mul(cc, 0.6))
        out = dict(r)
        out['color'] = N.mix(m, r['color'], ccol)
        out['rough'] = N.mixf(m, r['rough'], p.get('coat_rough', 0.92))
        out['metal'] = N.mixf(m, r['metal'], 0.0)
        if r.get('height') is not None:
            out['height'] = N.math('ADD', r['height'], N.math('MULTIPLY', N.math('MULTIPLY', m, grain), p.get('coat_h', 1.0)))
        return out
    return k


COATABLE = ('skin', 'metal', 'cloth', 'leather', 'bone', 'wood')
for _k in COATABLE:
    L.KINDS['coat_' + _k] = coated(_k)
COAT = dict(moss=dict(coat=(0.075, 0.105, 0.035), coat_lo=0.05, coat_patch=0.55, coat_h=1.6),
            sand=dict(coat=(0.46, 0.36, 0.21), coat_lo=0.2, coat_patch=0.35, coat_rough=0.88),
            ash=dict(coat=(0.27, 0.265, 0.26), coat_lo=0.1, coat_patch=0.5, coat_rough=0.95),
            soot=dict(coat=(0.018, 0.017, 0.016), coat_lo=0.3, coat_patch=0.7, coat_patch_lo=0.42, coat_rough=0.85),
            mud=dict(coat=(0.075, 0.058, 0.04), coat_lo=0.6, coat_patch=0.75, coat_patch_lo=0.44, coat_rough=0.45, coat_h=1.4))


# ------------------------------------------------------------------------------------------------ look presets
def _p(kind, slot, **params):
    return (kind, slot, params)


PRE = dict(
    # skin
    skin=_p('skin', 'skin', color=(0.33, 0.34, 0.31), blotch=(0.19, 0.16, 0.18), hands_dark=0.85, feet_dirt=0.8, sore_amt=0.5, bump=0.5),
    skin_ash=_p('skin', 'skin', color=(0.29, 0.285, 0.275), blotch=(0.15, 0.14, 0.14), hands_dark=0.0, vein_amt=0.4),
    skin_live=_p('skin', 'skin', color=(0.40, 0.29, 0.23), blotch=(0.30, 0.17, 0.14), sore_amt=0.15, vein_amt=0.3, hands_dark=0.35, feet_dirt=0.7),
    skin_dry=_p('skin', 'skin', color=(0.15, 0.10, 0.068), blotch=(0.07, 0.048, 0.036), vein=(0.04, 0.028, 0.024), vein_amt=0.6,
                vein_scale=16.0, sore_amt=0.0, hands_dark=0.5, dark=(0.04, 0.028, 0.024), bump=0.9, rough=0.62),
    skin_shade=_p('skin', 'skin', color=(0.075, 0.095, 0.105), blotch=(0.03, 0.04, 0.05), vein=(0.16, 0.30, 0.34), vein_amt=0.5,
                  sore_amt=0.0, hands_dark=0.7, dark=(0.012, 0.016, 0.02), hands_x0=0.55, hands_x1=0.9),
    skin_char=_p('rock', 'rock', color=(0.05, 0.043, 0.04), ash=(0.17, 0.155, 0.145), scale=24.0, heat=(0.5, 0.1, 0.015), heat_amt=0.6,
                 ash_amt=0.45, bump=0.8, crack_w=0.05),
    maw=_p('flat', 'skin', color=(0.035, 0.008, 0.008), rough=0.5),
    # cloth
    robe=_p('cloth', 'cloth', color=(0.205, 0.20, 0.195), dirt=(0.10, 0.09, 0.08), hem_lo=0.05, hem_hi=0.65, fold=9.0),
    hood=_p('cloth', 'cloth', color=(0.125, 0.125, 0.13), hem_lo=-2, hem_hi=-1, fold=12.0),
    rag=_p('cloth', 'cloth', color=(0.105, 0.085, 0.07), hem_lo=0.5, hem_hi=1.0, stain_amt=0.7),
    linen=_p('cloth', 'cloth', color=(0.50, 0.47, 0.40), dirt=(0.16, 0.13, 0.10), hem_lo=0.5, hem_hi=1.0, hem_amt=0.7, stain_amt=0.6, streak=0.5),
    madder=_p('cloth', 'cloth', color=(0.25, 0.085, 0.06), dirt=(0.10, 0.085, 0.07), hem_lo=1.2, hem_hi=1.46, hem_amt=0.75, fold=11.0,
              stain_amt=0.6, streak=0.5),
    shroudcloth=_p('cloth', 'cloth', color=(0.055, 0.055, 0.06), dirt=(0.09, 0.085, 0.08), hem_lo=-2, hem_hi=-1, fold=16.0, streak=0.6, fray=0.5),
    rope=_p('cloth', 'cloth', color=(0.30, 0.235, 0.14), hem_lo=-2, hem_hi=-1, scale=30.0, bump=0.8),
    void=_p('flat', 'cloth', color=(0.008, 0.008, 0.01), rough=1.0),
    wraps=_p('wraps', 'cloth', axis=(0.16, 0.05, 1.0), bands=23.0, color=(0.5, 0.435, 0.315), dark=(0.11, 0.08, 0.055),
             stain=(0.33, 0.215, 0.1), stain_amt=0.55),
    wraps_h=_p('wraps', 'cloth', axis=(1.0, 0.12, 0.16), bands=25.0, color=(0.5, 0.435, 0.315), dark=(0.11, 0.08, 0.055),
               stain=(0.33, 0.215, 0.1), stain_amt=0.55),
    burlap=_p('burlap', 'cloth'),
    # leather / wood / fur
    strap=_p('leather', 'leather', color=(0.085, 0.055, 0.035)),
    hide=_p('leather', 'leather', color=(0.115, 0.066, 0.042), scuff=0.6),
    apron=_p('leather', 'leather', color=(0.13, 0.075, 0.04), scuff=0.7, scale=30.0),
    fur=_p('fur', 'leather', color=(0.062, 0.05, 0.04), tip=(0.17, 0.14, 0.115), scale=38.0, bump=1.0),
    wood=_p('wood', 'leather', color=(0.115, 0.078, 0.048), axis=(0, 1, 0)),
    wood_z=_p('wood', 'leather', color=(0.105, 0.072, 0.045), axis=(0, 0, 1)),
    oak=_p('wood', 'leather', color=(0.085, 0.062, 0.042), axis=(0, 0, 1)),
    # metal
    iron=_p('metal', 'metal', amount=0.45, color=(0.20, 0.20, 0.21), edge=0.9, rough=0.42),
    rust=_p('metal', 'metal', amount=0.85, color=(0.19, 0.18, 0.17)),
    steel=_p('metal', 'metal', amount=0.5, color=(0.27, 0.26, 0.25), edge=1.0),
    blade=_p('metal', 'metal', amount=0.55, color=(0.27, 0.26, 0.25), edge=1.0, rough=0.36),
    black_plate=_p('metal', 'metal', **L.PRESET['black_plate']),
    brass=_p('metal', 'metal', **L.PRESET['brass']),
    brass_bright=_p('metal', 'metal', color=(0.62, 0.46, 0.17), rust=(0.07, 0.17, 0.13), rust2=(0.04, 0.08, 0.06), amount=0.12,
                    edge_color=(0.9, 0.75, 0.4), rough=0.16, hammer=8.0),
    copper=_p('metal', 'metal', color=(0.42, 0.20, 0.11), rust=(0.08, 0.26, 0.20), rust2=(0.05, 0.12, 0.09), amount=0.5,
              edge_color=(0.75, 0.42, 0.26), rough=0.38),
    silver=_p('metal', 'metal', color=(0.36, 0.37, 0.39), rust=(0.07, 0.07, 0.085), rust2=(0.03, 0.03, 0.04), amount=0.55,
              edge_color=(0.78, 0.80, 0.84), rough=0.3),
    gold=_p('metal', 'metal', **L.PRESET['gold_tarnish']),
    lead=_p('metal', 'metal', color=(0.16, 0.165, 0.18), rust=(0.22, 0.23, 0.24), rust2=(0.10, 0.10, 0.11), amount=0.5, rough=0.6, edge=0.4),
    # bone-like
    bone=_p('bone', 'bone', dirt_lo=0.0, dirt_hi=0.55),
    tooth=_p('bone', 'bone', color=(0.52, 0.46, 0.33), stain=(0.2, 0.13, 0.07)),
    claw=_p('bone', 'bone', color=(0.16, 0.13, 0.10), stain=(0.05, 0.04, 0.035), crack_amt=0.2),
    horn=_p('bone', 'bone', color=(0.40, 0.35, 0.27), stain=(0.11, 0.075, 0.05), crack=40.0, blotch=5.0, bump=0.6),
    mask=_p('bone', 'bone', color=(0.66, 0.64, 0.58), stain=(0.30, 0.26, 0.21), crack=13.0, crack_amt=0.4, blotch=11.0),
    wax=_p('wax', 'bone', color=(0.60, 0.55, 0.42), soot=0.25),
    wick=_p('flat', 'bone', color=(0.02, 0.02, 0.02), rough=1.0),
    # rock-like
    stone=_p('rock', 'rock', color=(0.15, 0.15, 0.145), ash=(0.30, 0.30, 0.29), ash_amt=0.5, scale=11.0, crack_w=0.03),
    slab=_p('rock', 'rock', color=(0.034, 0.031, 0.031), ash=(0.20, 0.19, 0.18), scale=13.0, ash_amt=0.8, bump=0.9),
    clay=_p('glass', 'rock', color=(0.016, 0.015, 0.017), band=(0.05, 0.045, 0.04), rough=0.22),
    ice=_p('crystal', 'rock', color=(0.2, 0.42, 0.68), deep=(0.018, 0.085, 0.25), scale=8.0, crack=14.0, frost_amt=0.65),
    ice2=_p('crystal', 'rock', color=(0.4, 0.66, 0.9), deep=(0.09, 0.28, 0.52), scale=14.0, crack=30.0, frost_amt=0.35, edge=0.45),
    # emissive (emit defaults to the spec's `emis` colour)
    eye=_p('emis', 'emis', strength=3.0),
    glow=_p('emis', 'emis', strength=3.5, mottle=0.4),
    ember=_p('emis', 'emis', emit=(1.0, 0.3, 0.04), strength=2.4, base_mul=0.8, mottle=0.5, _name='ember'),
    # spectral
    shroud=_p('spectral', 'spectral', color=(0.34, 0.47, 0.50), dark=(0.06, 0.10, 0.125), wisp=(0.60, 0.80, 0.84), fade_lo=0.15, fade_hi=1.05),
    cowl=_p('spectral', 'spectral', color=(0.22, 0.31, 0.35), dark=(0.035, 0.06, 0.08), fade_lo=-2.0, fade_hi=-1.0),
)

REGION = dict(head=['Head'], neck=['neck_01'], torso=['pelvis', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r'],
              upperarms=['upperarm_l', 'upperarm_r'], forearms=['lowerarm_l', 'lowerarm_r'], thighs=['thigh_l', 'thigh_r'],
              calves=['calf_l', 'calf_r'], feet=['foot_l', 'foot_r', 'ball_l', 'ball_r'])


def region_bones(names):
    out = []
    for n in names:
        if n == 'hands':
            out += M.RIG.chain('hand_l') + M.RIG.chain('hand_r')
        elif n in ('hand_l', 'hand_r'):
            out += M.RIG.chain(n)
        elif n in REGION:
            out += REGION[n]
        else:
            out.append(n)
    return out


# ------------------------------------------------------------------------------------------------ proportions
def _rots(e):
    r = e.get('rot', [])
    if r and not isinstance(r[0], (tuple, list)):
        r = [r]
    return [tuple(x) for x in r]


def merge_pose(a, b):
    """b on top of a: rotations appended, moves added, scales multiplied"""
    out = {}
    for src in (a, b):
        for bone, e in src.items():
            o = out.setdefault(bone, {})
            if _rots(e):
                o['rot'] = o.get('rot', []) + _rots(e)
            if e.get('move') is not None:
                o['move'] = tuple(np.asarray(o.get('move', (0, 0, 0)), float) + np.asarray(e['move'], float))
            if e.get('scale') is not None:
                s = e['scale']
                s = np.array([s, s, s], float) if np.isscalar(s) else np.asarray(s, float)
                o['scale'] = tuple(np.asarray(o.get('scale', (1, 1, 1)), float) * s)
    return out


def props_pose(pr):
    """proportion / posture controls -> pose_sculpt dict (see the module docstring for the keys)"""
    R = M.RIG
    P_ref = [{}]

    def sc(b, s):
        P_ref[0] = merge_pose(P_ref[0], {b: dict(scale=s)})

    def rot(b, ax, d):
        if abs(d) > 1e-6:
            P_ref[0] = merge_pose(P_ref[0], {b: dict(rot=[(ax, d)])})

    def mv(b, v):
        P_ref[0] = merge_pose(P_ref[0], {b: dict(move=v)})

    g = pr.get
    bulk = g('bulk', 1.0); depth = g('depth', 1.0); belly = g('belly', 0.0)
    chest = g('chest', 1.0) * bulk; waist = g('waist', 1.0) * bulk; hips = g('hips', 1.0) * bulk
    sc('pelvis', (hips, hips * depth * (1 + 0.5 * belly), 1.0))
    sc('spine_01', (waist * (1 + 0.35 * belly), waist * depth * (1 + belly), 1.0))
    sc('spine_02', ((waist + chest) / 2 * (1 + 0.2 * belly), (waist + chest) / 2 * depth * (1 + 0.6 * belly), 1.0))
    sc('spine_03', (chest, chest * depth, 1.0))
    nk = g('neck', 1.0) * bulk ** 0.5
    sc('neck_01', (nk, nk, g('neck_len', 1.0)))
    if g('neck_len', 1.0) != 1.0:
        mv('Head', (0, 0, (g('neck_len', 1.0) - 1.0) * 0.081))
    if g('head', 1.0) != 1.0:
        sc('Head', g('head', 1.0))
    for s in ('l', 'r'):
        sx = 1.0 if s == 'l' else -1.0
        a = g('arm_' + s, g('arm', 1.0)) * bulk; fa = g('forearm_' + s, g('forearm', g('arm_' + s, g('arm', 1.0)))) * bulk
        sh = g('shoulders', 1.0)
        sc('clavicle_' + s, (sh, 0.5 + 0.5 * a, 0.5 + 0.5 * a))
        if sh != 1.0:
            mv('upperarm_' + s, (sx * (sh - 1.0) * 0.17, 0, 0))
        sc('upperarm_' + s, (1.0, a, a)); sc('lowerarm_' + s, (1.0, fa, fa))
        hs = g('hands_' + s, g('hands', 1.0))
        if hs != 1.0:
            for b in R.chain('hand_' + s):
                sc(b, hs)
        lg = g('leg', 1.0) * bulk; cf = g('calf', g('leg', 1.0)) * bulk
        sc('thigh_' + s, (lg, lg, 1.0)); sc('calf_' + s, (cf, cf, 1.0))
        ft = g('feet', 1.0)
        if ft != 1.0:
            sc('foot_' + s, ft); sc('ball_' + s, ft)
        al = g('arm_len_' + s, g('arm_len', 1.0))
        if al != 1.0:
            sc('upperarm_' + s, (al, 1, 1)); mv('lowerarm_' + s, (sx * 0.274 * (al - 1.0), 0, 0))
            sc('lowerarm_' + s, (al, 1, 1)); mv('hand_' + s, (sx * 0.273 * (al - 1.0), 0, 0))
        fl = g('forearm_len_' + s, g('forearm_len', 1.0))   # forearm only, the hand stays on its joint (drop it, add a fixed claw)
        if fl != 1.0:
            sc('lowerarm_' + s, (fl, 1, 1))
        ll = g('leg_len', 1.0)
        if ll != 1.0:
            sc('thigh_' + s, (1, 1, ll)); mv('calf_' + s, (0, 0, -0.40 * (ll - 1.0)))
            sc('calf_' + s, (1, 1, ll)); mv('foot_' + s, (0, 0, -0.428 * (ll - 1.0)))
    h = g('hunch', 0.0)
    rot('spine_02', 'x', 0.25 * h); rot('spine_03', 'x', 0.40 * h); rot('neck_01', 'x', 0.60 * h)
    rot('Head', 'x', -0.45 * h * g('head_up', 1.0))
    st = g('stoop', 0.0)
    rot('spine_01', 'x', 0.55 * st); rot('spine_02', 'x', 0.45 * st); rot('Head', 'x', -0.6 * st * g('head_up', 1.0))
    rot('spine_01', 'x', g('lean', 0.0))
    rot('Head', 'x', g('head_pitch', 0.0)); rot('Head', 'y', g('head_roll', 0.0)); rot('Head', 'z', g('head_turn', 0.0))
    return merge_pose(P_ref[0], g('pose', {}))


def _G(pose, bone):
    return M.posed_point(pose, bone, (0, 0, 0))[1]


# ------------------------------------------------------------------------------------------------ the assembler
class Asm:
    def __init__(self, spec):
        self.spec = spec
        self.id = spec['id']
        self.C = M.Creature(self.id, int(spec.get('budget', 15000)))
        self.R = M.RIG
        self.seed = int(spec.get('seed', zlib.crc32(self.id.encode()) % 997))
        self.emit = tuple(spec.get('emis', (0.30, 1.0, 0.45)))
        self.dress = dict(spec.get('dress', {}))
        self.pose = props_pose(spec.get('props', {}))
        self.post = props_pose(spec['post']) if spec.get('post') else {}
        self.post = {b: {k: v for k, v in e.items() if k != 'scale'} for b, e in self.post.items()}
        self.post = {b: e for b, e in self.post.items() if e}
        self.rest = None            # skinned reference body in rest space (weights, seating)
        self.kind = 'male'
        self.surfs = {}
        self.refs = {}             # name -> Part in rest space that other parts may sit on (robe, mantle ...)
        self.notes = []
        self.mouth_c = None; self.gap = 0.0
        self.merge = {}            # bone -> bone it is merged into (stiff limbs, see body_flesh merge_w)
        self.late = {}             # bone -> bone it is bound to AFTER the sculpt pose (body_flesh bind_w)

    # ---- looks
    def _pre(self, name):
        if name not in PRE:
            raise KeyError("unknown look / preset %r" % name)
        return PRE[name][0], PRE[name][1], dict(PRE[name][2])

    def lk(self, name, **over):
        """make sure the look exists on the creature (spec override > preset of that name); returns the name"""
        if name in self.C.looks:
            return name
        d = self.spec.get('looks', {}).get(name)
        if d is None:
            kind, slot, p = self._pre(name)
        elif isinstance(d, str):
            kind, slot, p = self._pre(d)
        elif isinstance(d, dict):
            kind, slot, p = self._pre(name); p.update(d)
        elif len(d) == 3:
            kind, slot, p = d[0], d[1], dict(d[2])
        else:
            kind, slot, p = self._pre(d[0]); p.update(d[1])
        p.update(over)
        slot = p.pop('_slot', slot)
        if kind == 'emis':
            p.setdefault('emit', self.emit)
        elif not p.pop('_nodress', False):
            fr = self.dress.get('frost')
            if fr and kind in ('skin', 'metal', 'cloth', 'leather', 'bone', 'wood'):
                kind = 'frost_' + kind
                p.setdefault('frost_amt', fr); p.setdefault('rime', 0.6 * fr); p.setdefault('frost', (0.72, 0.82, 0.92))
            for key, cp in COAT.items():
                amt = self.dress.get(key)
                if amt and kind in COATABLE:
                    kind = 'coat_' + kind
                    for a, b in cp.items():
                        p.setdefault(a, b)
                    p.setdefault('coat_amt', amt)
            if self.dress.get('rust') and kind.endswith('metal'):
                p['amount'] = min(1.0, p.get('amount', 0.5) + self.dress['rust'])
        if slot in SLOT:
            # a `from` body brings its own material slots: join the one with the same prefix instead of adding a second
            have = [d['slot'] for d in self.C.looks.values() if d['slot'].startswith(slot + '_')]
            slot = have[0] if have else SLOT[slot]
        self.C.look(name, kind, slot, **p)
        return name

    def _wood(self, name, axis, base='wood'):
        """a wood look whose grain runs along `axis` (rest space); returns its name"""
        if name in self.C.looks:
            return name
        d = self.spec.get('looks', {}).get(base)
        kind, slot, p = self._pre(base)
        if isinstance(d, dict):
            p.update(d)
        p['axis'] = tuple(float(x) for x in axis)
        sp = dict(self.spec.get('looks', {})); sp[name] = [kind, slot, p]
        old = self.spec.get('looks'); self.spec['looks'] = sp
        try:
            return self.lk(name)
        finally:
            if old is None:
                del self.spec['looks']
            else:
                self.spec['looks'] = old

    # ---- parts
    def add(self, part, bone=None, look=None, obj=None, smooth=None, mirror=False, flip=False, final=False, skin=None,
            ride=None, like=None, noground=False, noscale=False, keep=False, sway=None):
        """add a Part built in REST space.  bone = rigid on that bone;  skin = n -> weights transferred from the rest body
        (n smoothing passes);  like = Part -> weights copied from that skinned Part;  ride = point -> the whole part takes
        the weights of the body surface nearest to that point (a lump riding on the skin);  flip = mirror to the right
        side;  mirror = add both sides;  final = already in final space (skips the sculpt in finish());  keep = a carried
        object on a scaled bone keeps its own shape and size, only its position follows the bone's scale: keep=True
        moves the piece with its own centre, keep=(x, y, z) with that anchor -- give EVERY piece of one assembly the
        same anchor (on the face that touches the body), otherwise the assembly comes apart on bulky bodies."""
        C = self.C
        if look is not None:
            look = self.lk(look)
        if skin is not None:
            KC.skin_cloth(part, body=self.rest, smooth=int(skin))
        if like is not None:
            KB2.skin_like(part, like, smooth=1)
        if ride is not None:
            self.ride(part, ride)
        if flip:
            if bone is not None:
                part.bone = bone; part.W = None; bone = None
            part.mirror()
        n0 = len(C.parts)
        C.add(part, bone=bone, look=look, obj=obj, smooth=smooth, mirror=mirror)
        for p in C.parts[n0:]:
            p._final = final; p._noground = noground; p._noscale = noscale
            # keep = True: the piece keeps its shape, its own centre follows the bone's scale;  keep = (x, y, z): an anchor
            # shared by every piece of one assembly (yoke + chains + loads), so the assembly moves as ONE rigid thing
            p._keep = keep is not False and keep is not None
            p._keep_c = None if isinstance(keep, bool) or keep is None else np.asarray(keep, float)
            # sway = (low bone, t): a rigid piece that HANGS from its bone.  t (per vertex, 0..1, from rest coordinates) is the
            # share of `low bone` (normally 'pelvis', which tips far less than the chest in the attack clips): after the piece
            # has been placed like any rigid part it becomes skinned  (1 - t) x its bone + t x low bone,  so its lower end lags
            # and stays nearer the vertical when the trunk tips.  No extra bones, the shared skeleton is untouched.
            p._sway = None
            if sway is not None and bone is not None:
                t = np.clip(np.broadcast_to(np.asarray(sway[1], float), (len(p.v),)), 0.0, 1.0).copy()
                p._sway = (sway[0], t)
        return part

    def ride(self, part, at=None):
        at = part.center() if at is None else np.asarray(at, float)
        vi = int(np.argmin(np.linalg.norm(self.rest.v - at, axis=1)))
        KA.stick(part, self.rest, vi)
        return part

    def S(self, name='body'):
        """BVH surface for seating: 'body' = the rest body, else a Part published in self.refs"""
        if name not in self.surfs:
            self.surfs[name] = KB2.Surf(self.rest if name == 'body' else self.refs[name])
        return self.surfs[name]

    def sides(self, side):
        return {'l': (1,), 'r': (-1,), 'both': (1, -1)}[side]

    # ---- build steps
    def build(self):
        sp = self.spec
        import asm_parts as AP, asm_weapons as AW
        b = dict(sp.get('body', {'type': 'flesh'}))
        BODIES[b.get('type', 'flesh')](self, b)
        for d in sp.get('parts', []):
            d = dict(d); name = d.pop('p')
            if name not in AP.PARTS:
                raise KeyError("unknown part %r (see asm_parts.PARTS)" % name)
            AP.PARTS[name](self, **d)
        for d in sp.get('weapons', []):
            d = dict(d); name = d.pop('w')
            if name not in AW.WEAPONS:
                raise KeyError("unknown weapon %r (see asm_weapons.WEAPONS)" % name)
            AW.WEAPONS[name](self, **d)
        if sp.get('script'):
            importlib.import_module(sp['script']).extra(self)
        self.finish()
        slots = self._fit_materials()
        if sp.get('jpeg'):
            self.C.jpg_q = tuple(sp['jpeg'])
        M.log("asm %s: %d parts, materials %s" % (self.id, len(self.C.parts), slots))
        return self.C, sp.get('notes', '') + ((' ' + ' '.join(self.notes)) if self.notes else '')

    def finish(self):
        C = self.C; R = self.R
        full = merge_pose(self.pose, self.post)
        if self.merge:                          # stiff limbs: everything that was put on a merged bone follows its target
            for p in C.parts:
                if p.bone is not None:
                    p.bone = self.merge.get(p.bone, p.bone)
                elif p.W is not None:
                    for sb, tgt in self.merge.items():
                        i, t = R.idx[sb], R.idx[tgt]
                        if p.W[:, i].any():
                            p.W[:, t] += p.W[:, i]; p.W[:, i] = 0
        for p in C.parts:
            if getattr(p, '_final', False):
                if self.post:
                    self._delta(p, self.pose, full)
                continue
            if p.bone is None:
                M.pose_sculpt(p, full)
            else:
                e = full.get(p.bone)
                if e and e.get('scale') is not None and not getattr(p, '_noscale', False):
                    s = np.asarray(e['scale'], float)
                    if getattr(p, '_keep', False):
                        c = p.center() if getattr(p, '_keep_c', None) is None else p._keep_c
                        h = R.H(p.bone)
                        p.move(h + s * (c - h) - c)
                    elif not p.obj.startswith('wpn_'):
                        p.scale(s, pivot=R.H(p.bone))
                    elif abs(s[0] - s[1]) < 1e-6 and abs(s[1] - s[2]) < 1e-6:
                        p.scale(float(s[0]), pivot=R.H(p.bone))
                M.pose_part(p, full, p.bone)
                sw = getattr(p, '_sway', None)
                if sw is not None:                  # hanging piece: rigid placement done, now it lags by a weight blend
                    W = M.rigid_W(len(p.v), p.bone) * (1.0 - sw[1])[:, None].astype(np.float32)
                    W[:, R.idx[sw[0]]] += sw[1].astype(np.float32)
                    p.W = W; p.bone = None
        if self.late:                           # limbs posed with their own bones, animated by another one (bind_w)
            for p in C.parts:
                if p.bone is not None:
                    p.bone = self.late.get(p.bone, p.bone)
                elif p.W is not None:
                    for sb, tgt in self.late.items():
                        i, t = R.idx[sb], R.idx[tgt]
                        if i != t and p.W[:, i].any():
                            p.W[:, t] += p.W[:, i]; p.W[:, i] = 0
        self._fit_budget()
        zs = [p.v[:, 2].min() for p in C.parts if not getattr(p, '_noground', False) and not p.obj.startswith('wpn_')]
        dz = min(zs) - float(self.spec.get('float', 0.0))
        for p in C.parts:
            if not getattr(p, '_noground', False):
                p.v[:, 2] -= dz
        self.dz = dz
        M.log("asm ground shift %.4f" % dz)

    def _fit_materials(self, limit=5):
        """at most `limit` material slots: the smallest surplus slots are merged into a related one (emis_ / spectral_
        slots mean something to the engine and are never merged away)"""
        C = self.C
        pref = dict(leather=('cloth', 'metal', 'bone'), bone=('skin', 'rock', 'cloth'), rock=('bone', 'metal', 'cloth'),
                    metal=('leather', 'rock', 'bone'), cloth=('leather', 'skin'), skin=('bone', 'cloth'))
        while True:
            tris = {}
            for p in C.parts:
                sl = C.looks[p.look]['slot']
                tris[sl] = tris.get(sl, 0) + p.ntri
            if len(tris) <= limit:
                return sorted(tris)
            cand = sorted((n, sl) for sl, n in tris.items() if sl.split('_')[0] in pref)
            n, sl = cand[0]
            tgt = next((t for pfx in pref[sl.split('_')[0]] for t in tris if t.startswith(pfx + '_') and t != sl), None)
            if tgt is None:
                tgt = next(t for t in tris if t != sl and t.split('_')[0] in pref)
            for d in C.looks.values():
                if d['slot'] == sl:
                    d['slot'] = tgt
            M.log("asm materials: merged %s (%d tris) into %s" % (sl, n, tgt))

    def _delta(self, p, pa, pb):
        if p.bone is not None:
            p.tf(_G(pb, p.bone) @ np.linalg.inv(_G(pa, p.bone)))
            return
        used = np.where(p.W.max(0) > 0)[0]
        vh = np.c_[p.v, np.ones(len(p.v))]
        out = np.zeros((len(p.v), 3))
        for b in used:
            D = _G(pb, R_name(b)) @ np.linalg.inv(_G(pa, R_name(b)))
            out += p.W[:, b:b + 1] * (vh @ D.T)[:, :3]
        p.v = out / np.maximum(p.W[:, used].sum(1, keepdims=True), 1e-9)

    def _fit_budget(self):
        """over the triangle budget: decimate the big parts first, then (creatures made of many small rigid pieces,
        e.g. skeleton amalgams) the medium ones"""
        C = self.C
        lim = C.budget - 250
        for floor in (900, 200, 80):
            tot = sum(p.ntri for p in C.parts)
            if tot <= lim:
                return
            big = [p for p in C.parts if p.ntri >= floor and not p.obj.startswith('wpn_')]
            pool = sum(p.ntri for p in big)
            if not pool:
                continue
            ratio = max(0.4, 1.0 - (tot - lim) / pool)
            for p in big:
                rigid = p.W is None
                if rigid:
                    p.W = M.rigid_W(len(p.v), p.bone)
                q = M.decimate(p, ratio)
                p.v, p.f = q.v, q.f
                p.W = None if rigid else q.W
            M.log("asm budget: %d tris -> %d (decimated %d parts >= %d tris x%.2f)" % (tot, sum(p.ntri for p in C.parts), len(big), floor, ratio))


def R_name(i):
    return M.RIG.names[int(i)]


# ------------------------------------------------------------------------------------------------ bodies
def body_skeleton(A, b):
    """articulated rigid-part skeleton (kit_bones) with the seated skull.  thick, jaw, skull (False = headless),
    drop = [bones / regions whose pieces are left out], eye = look of the socket lights ('eye' | 'void' ...)"""
    C = A.C
    th = b.get('thick', 1.2); jaw = b.get('jaw', 12.0)
    A.lk('bone'); eye = A.lk(b.get('eye', 'eye'))
    n0 = len(C.parts)
    if b.get('skull', True):
        KX.add_skull_seated(C, look='bone', eye_look=eye, jaw_open=jaw)
    KB.add_skeleton(C, look='bone', eye_look=eye, thick=th, jaw_open=jaw, skull=False)
    drop = set(region_bones(b.get('drop', [])))
    C.parts = C.parts[:n0] + [p for p in C.parts[n0:] if p.bone not in drop]
    A.rest = KC.ref_body('male')
    A.kind = 'male'


def _maw_teeth(A, deg, n=7, size=1.0):
    """mouth cavity + two tooth rows seated on the lip line of the open-jaw rest face (harvested from mon_husk)"""
    rest = A.rest
    hv = np.where(rest.wsum(['Head']) > 0.5)[0]
    hinge = np.array(KF.FACE['jaw_hinge']); Rj = M.rot_axis('x', deg)
    m0 = np.array(KF.FACE['mouth']); up = Rj @ np.array([0.0, 0.0, 1.0])

    def seat(t):
        return rest.v[hv[np.argmin(np.linalg.norm(rest.v[hv] - t, axis=1))]].copy()

    maw = ellipsoid(0.021, 0.014, max(A.gap * 0.42, 0.012), n=10, m=6).move(A.mouth_c + np.array([0, 0.026, 0.002]))
    A.add(maw, bone='Head', look='maw')
    for i, a in enumerate(np.linspace(-56, 56, n)):
        ar = math.radians(a)
        s = seat(m0 + np.array([0.026 * math.sin(ar), 0.020 * (1 - math.cos(ar)), 0.004]))
        t = M.cone_spike(0.0042 * size, (0.012 + 0.004 * (i % 2)) * size, n=4).rot('x', 180).move(s + np.array([0, 0.006, 0.003]))
        A.add(t, bone='Head', look='tooth')
        tl = (m0 + np.array([0.024 * math.sin(ar), 0.020 * (1 - math.cos(ar)), -0.008]) - hinge) @ Rj.T + hinge
        s = seat(tl)
        t = M.cone_spike(0.004 * size, (0.010 + 0.003 * ((i + 1) % 2)) * size, n=4)
        t.tf(M.rot_to((0, 0, 1), up)).move(s + np.array([0, 0.006, 0.0]) - up * 0.003)
        A.add(t, bone='Head', look='tooth')


def body_flesh(A, b):
    """sculpted CC0 base body (weights preserved).  kind male|female, decimate, look, show = 'all' | [regions] (only
    these regions are emitted; the rest stays as the hidden reference under clothes), hide = [regions], gaunt, jaw (deg),
    teeth, eyes ('eye' look name | None), eye_y, ribs, knobs, claws (length), toe_claws, fingers (stretch factor),
    melt (smoothing passes that dissolve the anatomy), head_look (the head as a separate piece with that look),
    merge_w = {bone: [bones / regions]} (their weights go to `bone`: a stiff limb),
    bind_w = {bone: [bones / regions]} (the same, but AFTER the sculpt pose: the limb is posed by its own bones -- props
    pose -- and then animated by `bone`: an arm that holds on to a load which is rigid on that bone)"""
    C = A.C
    kind = b.get('kind', 'male')
    body = M.load_body(kind)
    dec = b.get('decimate', 0.6)
    if dec < 1.0:
        body = M.decimate(body, dec)
    if b.get('gaunt'):
        KF.gaunt_face(body, b['gaunt'])
    jaw = b.get('jaw', 0.0)
    if jaw:
        A.mouth_c, A.gap = KF.open_jaw(body, jaw)
    if b.get('fingers'):
        KW.stretch_fingers(body, b['fingers'], 1.0 + (b['fingers'] - 1.0) * 0.6)
    A.rest = body; A.kind = kind
    look = b.get('look', 'skin')
    show = b.get('show', 'all')
    vis = body
    if show != 'all':
        vis = KC.body_piece(region_bones(show), body=body) if show else None
    if b.get('hide') and vis is not None:
        m = vis.wsum(region_bones(b['hide'])) > 0.5
        vis = vis.sub(~vis.vmask_faces(m, 'all'))
    shown = (lambda r: (show == 'all' or r in show) and r not in b.get('hide', []))
    A.vis = None
    if vis is not None and len(vis.f):
        vis = vis.copy()
    for tgt, srcs in b.get('bind_w', {}).items():
        for sb in region_bones(srcs):
            A.late[sb] = tgt
    # merge_w = {target bone: [bones / regions]}: limbs that were baked far off their joints (arms raised for good) become
    # stiff -- everything below the joint is weighted to the bone that still pivots at the right place
    for tgt, srcs in b.get('merge_w', {}).items():
        ids = M.RIG.ids(region_bones(srcs)); t = M.RIG.idx[tgt]
        for sb in region_bones(srcs):
            A.merge[sb] = tgt
        for q in (body, vis):
            if q is not None and q.W is not None:
                q.W[:, t] += q.W[:, ids].sum(1); q.W[:, ids] = 0
    if vis is not None and len(vis.f):
        if b.get('melt'):                       # smooth the anatomy away (wax models, crusts): n smoothing passes
            M.smooth_verts(vis, int(b['melt']), 0.5)
        if b.get('head_look') and shown('head'):   # the head as its own piece with another look (paint, other skin)
            hm = vis.vmask_faces(vis.wsum(['Head']) > 0.5, 'all')
            A.add(vis.sub(hm), look=b['head_look'], smooth=180)
            vis = vis.sub(~hm)
        A.vis = A.add(vis, look=look, smooth=180)
    if jaw and b.get('teeth', True) and shown('head'):
        _maw_teeth(A, jaw, size=b.get('tooth_size', 1.0))
    if b.get('eyes', 'eye') and shown('head'):
        ey = b.get('eye_y', -0.094 if b.get('gaunt') else -0.101)
        r = b.get('eye_r', 0.008)
        eyes = join([ellipsoid(r, r * 0.75, r, n=7, m=5).move(sx * 0.031, ey, 1.668) for sx in (-1, 1)])
        A.add(eyes, bone='Head', look=b.get('eyes', 'eye'))
    if b.get('ribs'):
        for k in range(2, 8):
            for sx in (1, -1):
                pts = KB.rib_path(k, sx, 9, wide=0.93, deep=0.90)
                pts[:, 1] -= 0.004
                A.add(tube(pts, 0.011, n=5, r2=0.0075, up=(0, 0, 1)), bone='spine_03', look=look, smooth=60)
    if b.get('knobs'):
        KF.spine_knobs(C, A.lk(look), pose=None, n=9, r=0.02, y_off=-0.028)
    if b.get('claws'):
        KF.claws(C, A.lk('claw'), body=body, length=b['claws'], curl=b.get('claw_curl', 0.5))
    if b.get('toe_claws'):
        KF.toe_claws(C, A.lk('claw'), length=b['toe_claws'], r=0.006)


def body_shroud(A, b):
    """legless gaunt core for floating shrouded creatures (kit_wisp.core_body): torso + arms only"""
    body = KW.core_body(b.get('kind', 'male'), ratio=b.get('decimate', 0.6), z_cut=b.get('z_cut', 1.08), head=b.get('head', False))
    if b.get('fingers'):
        KW.stretch_fingers(body, b['fingers'], 1.0 + (b['fingers'] - 1.0) * 0.6)
    A.rest = KC.ref_body(b.get('kind', 'male')); A.core = body
    A.add(body.copy(), look=b.get('look', 'skin_shade'), smooth=180)
    if b.get('claws'):
        KF.claws(A.C, A.lk('claw'), body=body, length=b['claws'], curl=0.45, n=5)


def body_from(A, b):
    """the finished body of an existing creature script: module = 'mon_x', drop = [{look:, bone:, obj:}] filters of parts
    to leave out (a filter matches when all its keys match; bone 'SKIN' = skinned parts; values may be lists);
    cut = [bones / regions to amputate]; relook = {base look: {param overrides} | [kind, {params}]} re-skins the base.
    The script's sculpt pose is captured so that new parts follow the same proportions."""
    poses = []
    orig = M.pose_sculpt

    def cap(part, pose, _o=orig):
        poses.append((len(part.v), pose))
        return _o(part, pose)
    M.pose_sculpt = cap
    try:
        C0, notes = importlib.import_module(b['module']).build()
    finally:
        M.pose_sculpt = orig
    C = A.C = C0
    C.id = A.id
    C.budget = int(A.spec.get('budget', 15000))

    def match(p, f):
        for k, val in f.items():
            vals = val if isinstance(val, (list, tuple)) else [val]
            have = {'look': p.look, 'bone': p.bone or 'SKIN', 'obj': p.obj}[k]
            if have not in vals:
                return False
        return True
    drops = b.get('drop', [])
    kept = [p for p in C.parts if not any(match(p, f) for f in drops)]
    M.log("asm from %s: %d parts, %d dropped" % (b['module'], len(C.parts), len(C.parts) - len(kept)))
    cut = region_bones(b.get('cut', []))          # amputate: faces skinned to these bones and rigid parts on them go
    if cut:
        ids = M.RIG.ids(cut)
        out = []
        for p in kept:
            if p.bone is not None:
                if p.bone not in cut:
                    out.append(p)
                continue
            fm = p.vmask_faces(p.W[:, ids].sum(1) > 0.5, 'all')
            if fm.any():
                q = p.sub(~fm)
                q.bone, q.look, q.obj, q.smooth = p.bone, p.look, p.obj, p.smooth
                p = q
            if len(p.f):
                out.append(p)
        kept = out
    C.parts = kept
    for p in C.parts:
        p._final = True; p._noground = False; p._noscale = False
    for name, d in b.get('relook', {}).items():      # re-skin looks of the base: name -> {param overrides} | [kind, {params}]
        if name in C.looks:
            if isinstance(d, dict):
                C.looks[name]['params'].update(d)
            else:
                C.looks[name]['kind'] = d[0]; C.looks[name]['params'] = dict(d[1])
    base_pose = max(poses, key=lambda t: t[0])[1] if poses else {}
    A.pose = merge_pose(base_pose, A.pose)
    A.rest = KC.ref_body(b.get('kind', 'male'))
    A.notes.append("Body of %s." % b['module'])


BODIES = dict(skeleton=body_skeleton, flesh=body_flesh, shroud=body_shroud)
BODIES['from'] = body_from


def build(spec):
    """spec dict -> (Creature, notes): the build.py protocol"""
    return Asm(spec).build()
