"""split_t4.py <class>: cut today's hero_<class>.glb into the five t4 armour pieces (arm_<class>_<slot>_t4.glb).

The geometry, UVs, normals and weights are taken from the exported hero file unchanged, and the pieces use the hero's
own texture files (game/assets/chars/hero_<class>_<kind>_*.jpg, by reference): the full t4 set looks exactly like
today's hero and costs no extra texture memory. Which triangle belongs to which slot is found by re-running the hero's
build script (parts with names) and matching every island of the exported meshes to its part (t4map.py).
Needs hero_<class>_base.glb (skin that a piece does not cover is carried along from there).
"""
import sys, os, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
from gearpiece import *
from gearpiece import _tris_of, _face_verts3, _weld_map, _face_areas
from t4map import *
from mathutils.bvhtree import BVHTree


def components(wf):
    """connected components of triangles given welded vertex ids (F, 3) -> component id per triangle"""
    n = int(wf.max()) + 1
    par = np.arange(n)

    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]
            a = par[a]
        return a
    for a, b, c_ in wf:
        ra, rb, rc = find(a), find(b), find(c_)
        if ra != rb:
            par[rb] = ra
        if rc != ra:
            par[rc] = ra
    root = np.array([find(v) for v in wf[:, 0]])
    _, comp = np.unique(root, return_inverse=True)
    return comp


def cinch(o, ref, z0, z1, gap):
    co = get_co(o)
    ztop = co[:, 2].max()
    if ztop <= z0:
        return
    up = co[:, 2] > z0
    z = co[up, 2]
    co[up, 2] = z0 + (z - z0) * (z1 - z0) / max(ztop - z0, 1e-6)
    for i in np.nonzero(up)[0]:
        ok, loc, nrm, fi = ref.closest_point_on_mesh(Vector(co[i]))
        t = float(sstep(co[i, 2], z0, z0 + 0.5 * (z1 - z0)))
        tgt = np.array(loc) + np.array(nrm) * gap
        co[i] = co[i] * (1 - t) + tgt * t
    set_co(o, co)


def main():
    cls = args()[0]
    cap = capture_hero(cls)
    c = cap["c"]
    B = c.B
    vr = vert_regions(B)
    kd = kdtree.KDTree(len(B.co))
    for i, p in enumerate(B.co):
        kd.insert(Vector(p), i)
    kd.balance()
    rules = [(re.compile(p), t) for p, t in T4MAP[cls]]

    def rule(name):
        for rx, t in rules:
            if rx.match(name):
                return t
        return "chest"
    parts = [o for o in mk.PARTS if o["owner"] == "body"]
    per = {}
    for o in parts:
        mk.apply_mods(o)
        d = per.setdefault(o["cls"], dict(V=[], F=[], owner=[], off=0, parts=[]))
        co = get_co(o)
        tr = _tris_of(o)
        d["V"].append(co)
        d["F"].append(tr + d["off"])
        d["owner"] += [len(d["parts"])] * len(tr)
        d["off"] += len(co)
        d["parts"].append(o)
    for k, d in per.items():
        V = np.concatenate(d["V"])
        F = np.concatenate(d["F"])
        d["bvh"] = BVHTree.FromPolygons([tuple(map(float, v)) for v in V], [tuple(map(int, f)) for f in F], all_triangles=True)
    arm_h, hm = import_glb(os.path.join(OUT_CHARS, "hero_%s.glb" % cls), EXT_HERO)
    out = {s: [] for s in SLOTS}
    stats = {}
    for k in ("cloth", "leather", "metal", "bone", "emis"):
        M = hm.get("body_" + k)
        if M is None or k not in per:
            continue
        d = per[k]
        co = get_co(M)
        fv = _face_verts3(M)
        ar = _face_areas(M)
        cen = co[fv].mean(1)
        lab = np.zeros(len(fv), dtype=np.int32)
        worst = 0.0
        for i in range(len(fv)):
            votes = {}
            for p in (cen[i], co[fv[i, 0]], co[fv[i, 1]], co[fv[i, 2]]):
                loc, nrm, idx, dist = d["bvh"].find_nearest(Vector(p))
                if idx is not None:
                    pi = d["owner"][idx]
                    votes[pi] = votes.get(pi, 0.0) + 1.0 / (2e-4 + dist)
                    worst = max(worst, dist)
            lab[i] = max(votes, key=votes.get)
        comp = components(_weld_map(co, 2e-5)[fv])
        for cid_ in range(int(comp.max()) + 1):
            sel = comp == cid_
            ls = lab[sel]
            a = ar[sel]
            best = max(set(int(v) for v in ls), key=lambda q: float(a[ls == q].sum()))
            lab[sel] = best
        slot_of = np.full(len(fv), -1, dtype=np.int32)
        for pi, o in enumerate(d["parts"]):
            sel = lab == pi
            if not sel.any():
                continue
            t = rule(o.name)
            if t is None:
                stats[o.name] = "dropped"
                continue
            if isinstance(t, tuple):
                rs = t[1]
                idx = np.nonzero(sel)[0]
                cnt = {}
                for i in idx:
                    _, vi, _ = kd.find(Vector(cen[i]))
                    s = rs[SKIN_REGIONS[vr[vi]]]
                    slot_of[i] = SLOTS.index(s)
                    cnt[s] = cnt.get(s, 0) + 1
                stats[o.name] = cnt
            else:
                slot_of[sel] = SLOTS.index(t)
                stats[o.name] = t
        res = split_faces(M, slot_of, {i: "%s_%s" % (SLOTS[i], k) for i in range(5)}, c.arm, remove=True)
        for n, o in res.items():
            out[n.split("_")[0]].append(o)
        log("class %-7s: %d triangles matched to %d parts (largest distance %.1f mm)" % (k, len(fv), len(d["parts"]), worst * 1000))
    for o in hm.values():
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except ReferenceError:
            pass
    bpy.data.objects.remove(arm_h, do_unlink=True)
    by_slot = {}
    for n, t in stats.items():
        by_slot.setdefault(str(t), []).append(n)
    for t, ns in sorted(by_slot.items()):
        log("  -> %-8s %s" % (t, " ".join(sorted(ns))))
    drop_parts()
    arm_b, base = load_base(cls)
    summary = {}
    for slot in SLOTS:
        objs = out[slot]
        if not objs:
            log("t4 %s: nothing in today's hero for this slot" % slot)
            continue
        opt = dict(T4OPT.get((cls, slot), {}))
        cz = opt.pop("cinch", None)
        for o in objs:
            store_corner_normals(o)
        J = fin.join(objs, "arm_%s_%s_t4" % (cls, slot))
        restore_corner_normals(J)
        if cz:
            cinch(J, c.ref, *cz)
        hides, sk, rep = hides_and_carry(slot, [J], base, c.arm, **opt)
        fin_objs = [J]
        if sk is not None:
            sk.name = "arm_%s_%s_t4_skin" % (cls, slot)
            fin_objs.append(sk)
        ent = write_piece(cls, slot, "t4", fin_objs, c.arm, hides, extra=dict(coverage=rep, source="split of hero_%s.glb" % cls))
        summary[slot] = ent
        for o in fin_objs:
            o.hide_render = True
    log("t4 split of %s done: %s" % (cls, {s: e["triangles"] for s, e in summary.items()}))


main()
