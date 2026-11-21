"""poke_test.py <class> <outfit> [<outfit> ...] [--clips Clip:t,t,t;Clip2:t] -- numeric deformation check.

For every pose it counts the vertices that are covered at rest (by another worn piece) but come out in the pose:
  body skin through armour, the inner piece of two overlapping slots through the outer one (pants through boots / chest
  hem, sleeves through gloves ...). "Covered" = at least 4 of 5 rays in a cone round the vertex normal hit the other
  mesh within 6 cm; "out" = at most 1 of them does AND the cover is found within 3 cm behind the vertex. Result: one line per outfit and pose with the worst pairs, and
  _work/poke_<class>.json.  outfit syntax as render_gear.py (t1..t4, base, helm=t1,chest=t3,...).
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
import gearfin as gf
from mathutils.bvhtree import BVHTree

DEFAULT = {
    "melee": "Sword_Regular_A:0.3,0.45,0.55,0.7;Jog_Fwd_Loop:0.0,0.25,0.5,0.75;Sprint_Loop:0.3;Roll:0.45;Sword_Idle:0.5",
    "caster": "Spell_Simple_Shoot:0.3,0.45,0.6;Jog_Fwd_Loop:0.0,0.25,0.5,0.75;Sprint_Loop:0.3;Roll:0.45;Spell_Simple_Idle_Loop:0.5",
    "fist": "Punch_Cross:0.3,0.45,0.6;Punch_Jab:0.45;Jog_Fwd_Loop:0.0,0.25,0.5,0.75;Sprint_Loop:0.3;Roll:0.45;Idle_Loop:0.5",
    "ranged": "Pistol_Shoot:0.3,0.5;Sword_Regular_A:0.45,0.55;Jog_Fwd_Loop:0.0,0.25,0.5,0.75;Sprint_Loop:0.3;Roll:0.45;Pistol_Idle_Loop:0.5",
}
KIND = {"emberblade": "melee", "astromancer": "caster", "hunter": "ranged", "soulbinder": "caster", "wildsoul": "melee",
        "forgebody": "fist", "lampknight": "melee", "pactbinder": "caster"}


def load_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH' and o.parent is arm]
    for o in objs:
        if o is not arm and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    return arm, meshes


def hides_of(path):
    js, _ = gf.glb_read(path)
    for sc in js.get("scenes", []):
        v = (sc.get("extras") or {}).get("hide_body_regions")
        if v is not None:
            return [s for s in str(v).split(",") if s]
    return []


def parse_outfit(s):
    label = None
    if ":" in s:
        label, s = s.split(":", 1)
    if s == "base":
        return label or "base", {}
    if s in ("t1", "t2", "t3", "t4"):
        return label or s, {sl: s for sl in SLOTS}
    d = {}
    for kv in s.split(","):
        k, v = kv.split("=")
        d[k] = v
    return label or s, d


def posed(o, dg):
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float32)
    me.vertices.foreach_get('co', co)
    nr = np.empty(n * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', nr)
    me.calc_loop_triangles()
    t = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get('vertices', t)
    ev.to_mesh_clear()
    return co.reshape(-1, 3).astype(np.float64), nr.reshape(-1, 3).astype(np.float64), t.reshape(-1, 3)


def hits(co, nr, bvh, dist=0.06):
    out = np.zeros(len(co), dtype=np.int8)
    for i in range(len(co)):
        nn = Vector(nr[i])
        if nn.length < 0.5:
            continue
        p = Vector(co[i]) + nn * 0.0015
        t1 = nn.orthogonal().normalized()
        t2 = nn.cross(t1)
        h = 0
        for d in (nn, nn + t1 * 0.6, nn - t1 * 0.6, nn + t2 * 0.6, nn - t2 * 0.6):
            if bvh.ray_cast(p, d.normalized(), dist)[0] is not None:
                h += 1
        out[i] = h
    return out


def main():
    a = args()
    cls = a[0]
    clips = DEFAULT[KIND[cls]]
    outfits = []
    i = 1
    while i < len(a):
        if a[i] == "--clips":
            clips = a[i + 1]
            i += 2
        else:
            outfits.append(parse_outfit(a[i]))
            i += 1
    reset_scene()
    acts = load_actions()
    poses = [(None, 0.0)]
    for part in clips.split(";"):
        nm, ts = part.split(":")
        if nm not in acts:
            log("no clip", nm)
            continue
        poses += [(nm, float(t)) for t in ts.split(",")]
    report = {}
    for label, outfit in outfits:
        arm, body = load_glb(os.path.join(OUT_CHARS, "hero_%s_base.glb" % cls))
        layers = []                       # (name, slot or 'body', [objects])
        hidden = set()
        for slot, look in outfit.items():
            pid = "arm_%s_%s_%s" % (cls, slot, look)
            path = os.path.join(OUT_ARMOR, pid + ".glb")
            if not os.path.isfile(path):
                continue
            a2, ms = load_glb(path)
            skin, gear = [], []
            for o in ms:
                rebind(o, arm)
                (skin if o.name.split(".")[0].endswith("_skin") else gear).append(o)
            bpy.data.objects.remove(a2, do_unlink=True)
            layers.append((slot, gear, skin))
            hidden.update(hides_of(path))
        vis_body = []
        for o in body:
            n = o.name.split(".")[0]
            n = n[5:] if n.startswith("body_") else n
            if any(n.startswith(h) for h in hidden) or n in ("hair", "beard", "brows"):
                continue
            vis_body.append(o)
        inner = [("body", vis_body + [o for _, _, sk in layers for o in sk])] + [(s, g) for s, g, _ in layers]
        outer = [(s, g) for s, g, _ in layers]
        rest = {}
        res = {}
        for (act, t) in poses:
            set_pose(arm, acts[act] if act else None, t)
            dg = bpy.context.evaluated_depsgraph_get()
            data = {}
            for nm, objs in inner + outer:
                if (nm, "d") in data or not objs:
                    continue
                cs, ns, ts_, off = [], [], [], 0
                for o in objs:
                    co, nr, tr = posed(o, dg)
                    cs.append(co)
                    ns.append(nr)
                    ts_.append(tr + off)
                    off += len(co)
                data[(nm, "d")] = (np.concatenate(cs), np.concatenate(ns), np.concatenate(ts_))
            bv = {}
            for nm, objs in outer:
                if not objs:
                    continue
                co, nr, tr = data[(nm, "d")]
                bv[nm] = BVHTree.FromPolygons([tuple(map(float, v)) for v in co], [tuple(map(int, f)) for f in tr], all_triangles=True)
            key = "%s@%.2f" % (act or "rest", t)
            worst = []
            for inm, iobjs in inner:
                if not iobjs:
                    continue
                co, nr, _ = data[(inm, "d")]
                for onm in bv:
                    if onm == inm:
                        continue
                    if act is None:
                        rest[(inm, onm)] = hits(co, nr, bv[onm]) >= 4
                        continue
                    cov = rest.get((inm, onm))
                    if cov is None or not cov.any():
                        continue
                    idx = np.nonzero(cov)[0]
                    h = hits(co[idx], nr[idx], bv[onm])
                    cand = idx[h <= 1]
                    out = 0
                    if len(cand):
                        # really through: the cover now lies BEHIND the vertex (rays back along the normal hit it close by)
                        out = int((hits(co[cand], -nr[cand], bv[onm], 0.03) >= 3).sum())
                    if out:
                        worst.append((out, "%s>%s" % (inm, onm), round(100.0 * out / len(idx), 1)))
            if act is not None:
                worst.sort(reverse=True)
                res[key] = worst
                tot = sum(w[0] for w in worst)
                log("%-6s %-26s out=%-5d %s" % (label, key, tot, "  ".join("%s:%d(%.1f%%)" % (w[1], w[0], w[2]) for w in worst[:5])))
        report[label] = {k: [list(w) for w in v] for k, v in res.items()}
        tot = sum(w[0] for v in res.values() for w in v)
        mx = max([sum(w[0] for w in v) for v in res.values()] or [0])
        log("POKE %s %s: total %d vertex-poses out, worst pose %d" % (cls, label, tot, mx))
        for o in list(bpy.data.objects):
            if o.type in ('MESH', 'ARMATURE'):
                bpy.data.objects.remove(o, do_unlink=True)
    p = os.path.join(WORK, "poke_%s.json" % cls)
    old = {}
    if os.path.isfile(p):
        with open(p) as f:
            old = json.load(f)
    old.update(report)
    with open(p, "w") as f:
        json.dump(old, f, indent=1)


main()
