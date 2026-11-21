"""build_set.py <class> <look> [--preview] [--slots helm,chest] : build the armour pieces of one look of one class.

The body, proportions and source materials come from today's hero script (run up to its finalize); the look is
described by sets_<class>.py: a function t1 / t2 / t3 (and optionally t4x: extra NEW pieces for slots today's hero has
nothing for). Every slot is baked on its own (one atlas per piece, no shading from the other pieces) and exported as
game/assets/gear/armor/arm_<class>_<slot>_<look>.glb. Needs hero_<class>_base.glb.
"""
import sys, os, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
from gearpiece import *
from gearkit import *


def main():
    a = [x for x in args() if not x.startswith("--")]
    cls, look = a[0], a[1]
    only = None
    if "--slots" in args():
        only = args()[args().index("--slots") + 1].split(",")
    if "--preview" in args():
        fin.PREVIEW = True
        fin.SMP = dict(ao=12, cav=8, mask=4, color=3, orm=2, normal=4)
    t0 = time.time()
    cap = capture_hero(cls)
    c = cap["c"]
    drop_parts()
    c.hide[:] = False
    K = Kit(c, cls, look)
    mod = importlib.import_module("sets_" + cls)
    fn = getattr(mod, "t4x" if look == "t4" else look)
    fn(K)
    report_unweighted("spine_03")
    for o in list(mk.PARTS):
        assert "slot" in o.keys(), "part outside a K.slot block: " + o.name
        mk.apply_mods(o)
        co = get_co(o)
        if len(co) == 0 or len(o.data.polygons) == 0:
            log("EMPTY PART dropped:", o["slot"], o.name)
            remove_part(o)
        elif np.abs(co).max() > 2.6 or co[:, 2].min() < -0.2:
            log("WARNING part with far-off vertices:", o.name, np.round(co.min(0), 2), np.round(co.max(0), 2))
    fin._setup_cycles()
    for o in list(c.hairs) + ([c.brows] if c.brows is not None else []):
        o.hide_render = True
    arm_b, base = load_base(cls)
    done = {}
    by_slot = {s: [o for o in mk.PARTS if o["slot"] == s] for s in SLOTS}
    for slot in SLOTS:
        if only and slot not in only:
            continue
        parts = by_slot[slot]
        if not parts:
            continue
        pid = "arm_%s_%s_%s" % (cls, slot, look)
        baked = [o for o in parts if o["cls"] in fin.BAKED]
        emis = [o for o in parts if o["cls"] == "emis"]
        others = [o for o in bpy.data.objects if o.type == 'MESH' and o not in parts and o not in (c.skin, c.eyes)]
        for s2 in SLOTS:
            if s2 != slot:
                by_slot[s2] = [o for o in by_slot[s2]]
        objs = []
        if baked:
            fur = any(mats.SRCINFO.get(o["src"], {}).get("fur") for o in baked)
            J, at, fm = bake_parts(pid, baked, "gear", size_cap=SIZE_CAP[slot], isolate=others, decimate=K.decimate.get(slot),
                                   fur_budget=2500 if fur else 0)
            J.name = pid
            objs.append(J)
        if emis:
            E = emis_parts(pid, emis)
            E.name = pid + "_emis"
            objs.append(E)
        hides, sk, rep = hides_and_carry(slot, objs, base, c.arm, **K.opts.get(slot, {}))
        if sk is not None:
            sk.name = pid + "_skin"
            objs.append(sk)
        ent = write_piece(cls, slot, look, objs, c.arm, hides, extra=dict(coverage=rep, atlas=at.size if baked else 0))
        done[slot] = ent
        for o in objs:
            o.hide_render = True
    log("SET %s %s done in %.0fs: %s" % (cls, look, time.time() - t0, {s: (e["triangles"], e.get("atlas")) for s, e in done.items()}))


main()
