"""Spec registry of the creature assembler: every specs_*.py next to this file holds SPECS = {model id: spec dict}
(see asm.py for the spec format).  EXISTING lists the hand-built rig-A creatures so family sheets show whole families.
   <blender python> asm_specs.py --family <fam>     -> ids of that family that have a spec (build order)
   <blender python> asm_specs.py --list             -> every spec id with its family"""
import glob, importlib, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
CHARS = r"Y:\game\assets\chars"
SCRATCH = os.path.join(HERE, "asm_out")

POSES = {
    'sword': ["Sword_Idle:0.3", "Sword_Regular_A:0.55", "Walk_Loop:0.3"],
    'heavy': ["Sword_Idle:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"],
    'zombie': ["Zombie_Idle_Loop:0.3", "Zombie_Scratch:0.45", "Zombie_Walk_Fwd_Loop:0.3"],
    'caster': ["Idle_Loop:0.3", "Spell_Simple_Shoot:0.5", "Walk_Loop:0.3"],
    'thrower': ["Sword_Idle:0.3", "OverhandThrow:0.3", "Jog_Fwd_Loop:0.3"],
    'shield': ["Sword_Block:0.5", "Shield_OneShot:0.45", "Walk_Loop:0.3"],
    'punch': ["Idle_Loop:0.3", "Punch_Cross:0.45", "Sprint_Loop:0.3"],
    'pistol': ["Pistol_Idle_Loop:0.3", "Pistol_Shoot:0.3", "Walk_Loop:0.3"],
    'lantern': ["Idle_Lantern_Loop:0.3", "Spell_Simple_Shoot:0.5", "Walk_Loop:0.3"],
    # what the game really plays for a type whose `anims` text names no idle / walk: Idle_Loop + Walk_Loop (Bestiary.cs)
    'brute': ["Idle_Loop:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"],
    'bomber': ["Idle_Loop:0.3", "OverhandThrow:0.3", "Walk_Loop:0.3"],
    'zbrute': ["Zombie_Idle_Loop:0.3", "Sword_Attack:0.45", "Zombie_Walk_Fwd_Loop:0.3"],
}
# hand-built creatures: id -> (family, engine scale, pose kind)
EXISTING = {
    'mon_skeleton': ('bone', 1.0, 'sword'), 'mon_skeleton_thrower': ('bone', 1.0, 'thrower'),
    'mon_husk': ('husk', 1.0, 'zombie'), 'mon_husk_bloated': ('husk', 1.15, 'zombie'),
    'mon_cultist': ('cult', 1.0, 'sword'), 'mon_cult_caster': ('cult', 1.0, 'caster'), 'mon_knight': ('cult', 1.15, 'heavy'),
    'mon_choir': ('cult', 1.0, 'caster'), 'mon_wraith': ('wraith', 1.0, 'caster'),
    'mon_frostbound': ('frost', 1.0, 'sword'), 'mon_rime_stalker': ('frost', 1.0, 'zombie'),
    'mon_sand_revenant': ('sands', 1.0, 'sword'), 'mon_brass_sentinel': ('sands', 1.5, 'heavy'),
    'mon_fiend': ('fiend', 1.0, 'zombie'), 'mon_brute': ('fiend', 1.5, 'heavy'),
    'mon_mire_hag': ('mire', 1.0, 'caster'), 'mon_rotspawn': ('mire', 1.4, 'zombie'),
}

SPECS = {}
for _f in sorted(glob.glob(os.path.join(HERE, 'specs_*.py'))):
    _m = importlib.import_module(os.path.basename(_f)[:-3])
    for _k, _v in _m.SPECS.items():
        _v = dict(_v); _v['id'] = _k
        SPECS[_k] = _v


def get(cid):
    if cid not in SPECS:
        raise KeyError("no spec for %r (specs_*.py)" % cid)
    return dict(SPECS[cid])


def poses_of(spec):
    p = spec.get('poses', 'sword')
    return list(POSES[p]) if isinstance(p, str) else list(p)


def glb_path(cid):
    return os.path.join(SCRATCH if SPECS.get(cid, {}).get('scratch') else CHARS, cid + ".glb")


def members(fam):
    """[(id, scale, [3 pose specs], glb path)] of a family: hand-built ones first, then specs in file order"""
    out = [(k, v[1], list(POSES[v[2]]), os.path.join(CHARS, k + ".glb")) for k, v in EXISTING.items() if v[0] == fam and k not in SPECS]
    out += [(k, float(v.get('scale', 1.0)), poses_of(v), glb_path(k)) for k, v in SPECS.items() if v.get('family') == fam]
    return out


def member(cid):
    if cid in SPECS:
        v = SPECS[cid]
        return (cid, float(v.get('scale', 1.0)), poses_of(v), glb_path(cid))
    v = EXISTING[cid]
    return (cid, v[1], list(POSES[v[2]]), os.path.join(CHARS, cid + ".glb"))


if __name__ == '__main__':
    a = sys.argv[1:]
    if a[:1] == ['--family']:
        print(",".join(k for k, v in SPECS.items() if v.get('family') == a[1]))
    else:
        for k, v in SPECS.items():
            print(k, v.get('family'), 'scratch' if v.get('scratch') else '')
