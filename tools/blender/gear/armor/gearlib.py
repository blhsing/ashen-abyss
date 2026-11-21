"""gearlib -- shared code of the modular armour builds (ART-A): hero capture, body regions, paths.

The hero pipeline in Y:\\tools\\blender\\heroes is used as a library and is NOT modified: its modules are imported from
there, the log and the texture work folder are redirected into this folder.
"""
import sys, os, json, math, time
HERE = os.path.dirname(os.path.abspath(__file__))
HERO = r"Y:\tools\blender\heroes"
if HERO not in sys.path:
    sys.path.insert(0, HERO)
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import rigio
rigio._LOG = os.path.join(HERE, "_build.log")
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, kdtree
import charlib
from charlib import *            # rigio, mk, mats, fin + helpers
import kit_cst, kit_mel, kit_twn
from kit_twn import *

WORK = os.path.join(HERE, "_work")
TEXW = os.path.join(WORK, "tex")
OUT_ARMOR = r"Y:\game\assets\gear\armor"
OUT_CHARS = r"Y:\game\assets\chars"
SHOTS_GEAR = r"Y:\shots\gear\armor"
for _d in (WORK, TEXW, OUT_ARMOR, SHOTS_GEAR):
    os.makedirs(_d, exist_ok=True)
fin.TEX = TEXW                    # baked atlases of this pipeline go to _work/tex/<id>/

CLASSES = ["emberblade", "astromancer", "hunter", "soulbinder", "wildsoul", "forgebody", "lampknight", "pactbinder"]
CLASS_NAME = {"emberblade": "Emberblade", "astromancer": "Astromancer", "hunter": "Hunter", "soulbinder": "Soulbinder",
              "wildsoul": "Wildsoul", "forgebody": "Forgebody", "lampknight": "Lampknight", "pactbinder": "Pactbinder"}
SLOTS = ["helm", "chest", "gloves", "pants", "boots"]

# body regions: token -> mesh node name in hero_<class>_base.glb (see game/assets/gear/armor/REGIONS.md)
SKIN_REGIONS = ["head", "neck", "torso", "upper_arms", "forearms", "hands", "hips", "thighs", "shins", "feet"]
REGION_MESH = {r: "body_" + r for r in SKIN_REGIONS}
REGION_MESH.update({"hair": "hair", "beard": "beard", "brows": "brows", "wrap_loin": "wrap_loin", "wrap_chest": "wrap_chest"})
# which regions a slot may take over (hide + carry the uncovered remainder as skin of its own)
SLOT_OWNS = {"helm": ["hair", "beard", "brows", "head"],
             "chest": ["torso", "upper_arms", "neck", "wrap_chest"],
             "gloves": ["hands", "forearms"],
             "pants": ["hips", "thighs", "shins", "wrap_loin"],
             "boots": ["feet", "shins"]}


def args():
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


# ---------------------------------------------------------------------------------------------- hero capture
class _Stop(Exception):
    pass


def capture_hero(cls, keep_face=True):
    """Run Y:\\tools\\blender\\heroes\\hero_<cls>.py up to its finalize() call (which is intercepted) and return
    dict(c=ctx, cid=, kw=finalize keywords, dropped=bool). With keep_face the calls that delete the skin / eyes / brows
    of fully covered heroes are ignored, so the reference body, skin, eyes and brows are always there.
    mk.PARTS then holds every part of today's hero (modifiers not applied yet), mats.SRCM its source materials."""
    cap = dict(dropped=False, drop_face=None)

    def fake_finalize(c, cid, **kw):
        cap["c"], cap["cid"], cap["kw"] = c, cid, kw
        raise _Stop()

    def fake_drop_skin(c):
        cap["dropped"] = True

    def fake_drop_face(c, eyes=True, brows=True):
        cap["drop_face"] = (eyes, brows)
    saved = {}
    for m in (charlib, kit_cst, kit_mel, kit_twn):
        saved[m] = (m.finalize, m.drop_skin, getattr(m, "drop_face_parts", None))
        m.finalize = fake_finalize
        if keep_face:
            m.drop_skin = fake_drop_skin
            if hasattr(m, "drop_face_parts"):
                m.drop_face_parts = fake_drop_face
    path = os.path.join(HERO, "hero_%s.py" % cls)
    with open(path, "r", encoding="utf8") as f:
        src = f.read()
    t0 = time.time()
    try:
        exec(compile(src, path, 'exec'), {"__name__": "__main__", "__file__": path})
    except _Stop:
        pass
    finally:
        for m, (a, b, d) in saved.items():
            m.finalize, m.drop_skin = a, b
            if d is not None:
                m.drop_face_parts = d
    assert "c" in cap, "hero script did not reach finalize()"
    log("captured hero_%s: %d parts, kind=%s, %.0fs" % (cls, len(mk.PARTS), cap["c"].kind, time.time() - t0))
    return cap


def drop_parts(keep=()):
    """remove every part built so far (the hero's clothing / weapons) except those listed"""
    for o in list(mk.PARTS):
        if o in keep:
            continue
        mk.PARTS.remove(o)
        bpy.data.objects.remove(o, do_unlink=True)


# ---------------------------------------------------------------------------------------------- body regions
def rig_marks():
    """joint positions (rest space, left side) used for the region borders"""
    j = lambda n: np.array(mk.ARM.data.bones[n].head_local)
    return dict(shoulder=j("upperarm_l"), elbow=j("lowerarm_l"), wrist=j("hand_l"), hip=j("thigh_l"), knee=j("calf_l"),
                ankle=j("foot_l"), neck=j("neck_01"), head=j("Head"), spine1=j("spine_01"), pelvis=j("pelvis"))


Z_WAIST = 1.005     # torso | hips
Z_HIPLINE = 0.800   # hips | thighs (just under the gluteal fold; the loincloth covers down to here)
Z_KNEE = 0.440      # thighs | shins (under the kneecap: knee boots end here)
Z_ANKLE = 0.125     # shins | feet
X_SHOULDER = 0.205  # torso | upper arms (armhole)


def vert_regions(B):
    """region index (into SKIN_REGIONS) of every reference-body vertex (T-pose rest coordinates + skin weights)"""
    mk_ = rig_marks()
    x, z = np.abs(B.x), B.z
    armw = B.w("upperarm", "lowerarm", *BONES_HAND)
    handw = B.w(*BONES_HAND)
    headw = B.w("Head")
    neckw = B.w("neck_01")
    legw = B.w(*BONES_LEG, *BONES_FOOT)
    R = {n: i for i, n in enumerate(SKIN_REGIONS)}
    reg = np.full(len(B.co), R["torso"], dtype=np.int32)
    lower = (z < Z_WAIST) & (armw < 0.5)
    reg[lower] = R["hips"]
    leg = lower & (z < Z_HIPLINE)
    reg[leg] = R["thighs"]
    reg[leg & (z < Z_KNEE)] = R["shins"]
    reg[leg & (z < Z_ANKLE)] = R["feet"]
    arm = (armw > 0.5) & (x > X_SHOULDER)
    reg[arm] = R["upper_arms"]
    reg[arm & (x >= mk_["elbow"][0])] = R["forearms"]
    reg[handw > 0.5] = R["hands"]
    nk = (neckw + headw > 0.5) & (armw < 0.3)
    reg[nk] = R["neck"]
    reg[headw > 0.5] = R["head"]
    return reg


def face_regions(B, obj=None):
    """region index per polygon of the reference body mesh (majority of its vertices, ties -> lowest index)"""
    vr = vert_regions(B)
    me = (obj or B.obj).data
    out = np.zeros(len(me.polygons), dtype=np.int32)
    for p in me.polygons:
        vs = [vr[v] for v in p.vertices]
        out[p.index] = max(set(vs), key=lambda k: (vs.count(k), -k))
    return out, vr
