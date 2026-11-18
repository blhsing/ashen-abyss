"""Shared helpers of helper H2's quadruped specs (spec_h2.py): small geometry builders, the dragging-chain solver."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P


def std_meta(C, family, scale, speed, **kw):
    C.meta.update(family=family, rig='B', scale=scale, game_speed=speed)
    C.meta.update(kw)


# ------------------------------------------------------------------------------------------------ geometry
def surf_top(bvh, x, y, z0=3.0, default=0.0):
    """height of the host surface under (x, y), looking down from z0"""
    q, n = P.surf(bvh, (x, y, z0), (0, 0, -1), dist=z0 + 1.0)
    return default if q is None else float(q[2])


def surf_bottom(bvh, x, y, z0=-1.0, default=0.0):
    """height of the underside of the host over (x, y), looking up from z0"""
    q, n = P.surf(bvh, (x, y, z0), (0, 0, 1), dist=4.0)
    return default if q is None else float(q[2])


def hooves(C, J, look, rf=(0.05, 0.06), rh=(0.045, 0.055), h=0.05, cloven=True, fwd=0.02, legs=('f', 'h')):
    """hooves standing flat on the ground under the toe joints (rigid to the toe bones): r = (half width, half
    length), cloven = two toes with a cleft"""
    for pre in legs:
        r = rf if pre == 'f' else rh
        for s, sg in (('l', 1.0), ('r', -1.0)):
            c = J[pre][3] * (sg, 1, 1)
            cy = c[1] - fwd
            parts = []
            for k in ((-1, 1) if cloven else (0,)):
                rx_ = r[0] * (0.52 if cloven else 1.0)
                cx = c[0] + k * r[0] * 0.5
                secs = [(0.002, cx, cy - r[1] * 0.08, rx_, r[1], 2.6), (h * 0.45, cx, cy, rx_ * 0.95, r[1] * 0.95, 2.4),
                        (h, cx, cy + r[1] * 0.18, rx_ * 0.78, r[1] * 0.74, 2.2)]
                parts.append(M.loft_z(secs, n=8))
            q = M.join(parts)
            q.smooth = 50.0
            C.rigid(q, '%s_toe_%s' % (pre, s), look)


def surf_side(bvh, y, z, sg=1.0, x0=2.0, default=0.0):
    """x of the host surface at (y, z), looking inwards from the side sg"""
    q, n = P.surf(bvh, (sg * x0, y, z), (-sg, 0, 0), dist=x0 + 1.0)
    return default if q is None else float(q[0])


def seat_rock(C, bvh, xy, r, look, bone, seed=0, sink=0.4, n=14, jit=0.25, z=None):
    """a faceted chunk sitting on top of the host at (x, y), sunk into it by `sink` of its height"""
    zt = surf_top(bvh, xy[0], xy[1]) if z is None else z
    p = M.rock(r[0], r[1], r[2], seed=seed, n=n, jit=jit)
    p.move(xy[0], xy[1], zt + r[2] * (1.0 - 2.0 * sink))
    return C.rigid(p, bone, look, smooth=0)


def clean_mesh(part, dist=1e-5):
    """triangulate and remove degenerate faces / doubles (boolean results): the glTF exporter rejects invalid meshes"""
    import bmesh
    bm = part.to_bm()
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=dist)
    bmesh.ops.dissolve_degenerate(bm, dist=dist, edges=bm.edges[:])
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    look, obj, sm_ = part.look, part.obj, part.smooth
    part.from_bm(bm)
    part.look, part.obj, part.smooth = look, obj, sm_
    return part


def block(sx, sy, sz, c, seed=0, jit=0.012, bevel=0.018):
    """a faceted chunk with KNOWN extents (a bevelled, jittered box): use it where other pieces must really sit inside
    it (monlib.rock is a random hull: its reach is not predictable)"""
    p = M.box(sx, sy, sz, bevel=bevel)
    p.jitter(jit, seed=seed)
    p.move(c[0], c[1], c[2])
    p.smooth = 0.0
    return p


def bar(a, b, w, t=None, up=(0, 0, 1)):
    """straight rectangular bar a -> b: w = full width across `up`, t = full thickness along `up`"""
    t = w if t is None else t
    a = np.asarray(a, dtype=float); b = np.asarray(b, dtype=float)
    d = unit(b - a)
    if abs(float(d @ unit(up))) > 0.95:
        up = (0, 1, 0)
    p = M.tube(np.array([a, b]), t * 0.7071, n=4, r2=w * 0.7071, up=up, phase=math.pi / 4)
    p.smooth = 25.0
    return p


def hoop(c, R, Rz, r, n=12, m=4):
    """ring standing in the XZ plane (its hole along Y): R = half width, Rz = half height"""
    t = M.torus(R, r, n=n, m=m, ry=Rz)
    t.rot('x', 90)
    t.move(c[0], c[1], c[2])
    t.smooth = 60.0
    return t


def flat_horn(path, w0, t0, m=9, n=8, power=0.8, tip=0.003, up=(0, 0, 1)):
    """flat blade-like tusk / horn along a path: w0 = half width (sideways), t0 = half thickness (along up)"""
    pts = M.polyline(np.asarray(path, dtype=float), m)
    return M.horn(pts, t0, n=n, r_tip=tip, up=up, r2=w0, power=power)


def round_horn(path, r0, m=10, n=7, power=0.9, tip=0.003, up=None):
    pts = M.polyline(np.asarray(path, dtype=float), m)
    return M.horn(pts, r0, n=n, r_tip=tip, up=up, power=power)


def sail_plate(base, width, height, th=0.008, lean=0.25, notch=0.3, seed=0):
    """one thin upright plate of a back sail in the YZ plane: `base` = middle of its foot, width along Y, the top
    leans back (+Y) and the trailing edge has a notch"""
    rng = np.random.default_rng(100 + seed)
    b = np.asarray(base, dtype=float)
    w = width * 0.5
    k = 1.0 + rng.uniform(-0.08, 0.08)
    h = height * k
    pts = [(-w, 0.0), (w, 0.0), (w * 1.05 + lean * h * 0.35, h * (0.55 - notch * 0.5)), (w * 0.55 + lean * h * 0.55, h * (0.62 - notch * 0.2)),
           (w * 0.9 + lean * h, h), (-w * 0.2 + lean * h * 0.55, h * 0.8), (-w * 1.05 + lean * h * 0.1, h * 0.42)]
    v = [(b[0], b[1] + y, b[2] + z) for y, z in pts]
    s = M.Part(np.array(v), [tuple(range(len(v)))])
    s.solidify(th)
    s.v[:, 0] -= s.v[:, 0].mean() - b[0]
    s.smooth = 30.0
    return s


def tri_ear(base, tip, width, cup=0.02, th=0.006, rows=5, cols=5, sink=0.03, face=(0, -1, 0)):
    """triangular (cat) ear: widest at its base, pointed; `face` = the direction its hollow looks"""
    base = V(base); tip = V(tip)
    up = unit(tip - base)
    Ln = float(np.linalg.norm(tip - base))
    f = V(face) - up * float(V(face) @ up)
    f = unit(f)
    side = np.cross(up, f)
    G = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        h = -sink + (Ln + sink) * t
        w = width * max(0.03, (1 - t) ** 0.85)
        for j in range(cols):
            sx = -1 + 2 * j / (cols - 1)
            G[i, j] = base + up * h + side * (sx * w) - f * (cup * (1 - sx * sx) * (1 - t))
    return P.sheet_solid(G, th)


def chain_on(C, bones, look, link=0.05, r=0.007, w=0.62, n=6, m=4):
    """chain links along a bone chain, each run rigid to its bone (as rig_f.chain_parts)"""
    rg = C.rg
    for b in bones:
        ch = M.chain([rg.H(b), rg.T(b)], link=link, r=r, w=w, n=n, m=m)
        C.rigid(ch, b, look, smooth=60)


def trap_jaw(hinge, tip, half_w, tdir, nteeth=9, tooth=0.05, bar_w=0.03, bar_t=0.014, seed=0):
    """one jaw of an iron spring trap: a flat U-shaped bar from the hinge line (through `hinge`, along X) round the
    `tip`, with triangular teeth pointing along tdir"""
    Hh = np.asarray(hinge, dtype=float); T = np.asarray(tip, dtype=float)
    side = []
    for t, k in ((0.0, 1.0), (0.3, 1.1), (0.62, 1.08), (0.88, 0.74), (1.0, 0.0)):
        q = Hh + (T - Hh) * t
        side.append(V(half_w * k, q[1], q[2]))
    pts = [q * (-1, 1, 1) for q in side[:-1]] + [side[-1]] + [q for q in reversed(side[:-1])]
    path = M.polyline(np.array(pts), 17)
    nrm = unit(np.cross(V(1, 0, 0), unit(T - Hh)))
    if float(nrm @ unit(tdir)) < 0:
        nrm = -nrm
    fr = M.tube(path, bar_t * 0.7071, n=4, r2=bar_w * 0.7071, up=nrm, phase=math.pi / 4)
    parts = [fr]
    rng = np.random.default_rng(40 + seed)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))]
    for i in range(nteeth):
        s = d[-1] * (0.2 + 0.6 * (i + 0.5) / nteeth)
        j = int(np.clip(np.searchsorted(d, s) - 1, 0, len(path) - 2))
        q = path[j] + (path[j + 1] - path[j]) * ((s - d[j]) / max(d[j + 1] - d[j], 1e-9))
        ln = tooth * rng.uniform(0.8, 1.15)
        parts.append(P.cone(q - nrm * 0.004, nrm + unit(tdir) * 0.25, ln, 0.012, n=3))
    p = M.join(parts)
    p.smooth = 30.0
    return p


def split_by_bone(y, table, default):
    """bone for a station y from [(y_max, bone), ...] sorted ascending"""
    for ym, b in table:
        if y < ym:
            return b
    return default


def keep_bones(part, bones, fallback):
    """drop every weight that is not on `bones` (ropes of a coat must not swing with the legs)"""
    R = M.RIG
    keep = np.zeros(R.nb, dtype=bool)
    keep[[R.idx[b] for b in bones]] = True
    W = part.W.copy()
    W[:, ~keep] = 0
    s = W.sum(1)
    W[s < 1e-5, R.idx[fallback]] = 1.0
    part.W = (W / W.sum(1, keepdims=True)).astype(np.float32)
    return part


# ------------------------------------------------------------------------------------------------ motion
class DragChain:
    """a chain that hangs from a point on `parent` and drags on the ground: every link bone is aimed in world space
    so that its far end lies on the ground when it can reach it, otherwise it hangs"""

    def __init__(self, C, bones, parent):
        rg = C.rg
        self.b = list(bones)
        self.parent = parent
        self.h0 = rg.H(bones[0])
        self.d0 = [unit(rg.T(b) - rg.H(b)) for b in bones]
        self.l = [float(np.linalg.norm(rg.T(b) - rg.H(b))) for b in bones]
        self.zg = [float(rg.T(b)[2]) for b in bones]
        self.dh = [unit(V(d[0], d[1], 0.0)) for d in self.d0]

    def pose(self, p, u=0.0, wave=0.0, n=1, trail=None):
        h = p.pt(self.parent, self.h0)
        for i, b in enumerate(self.b):
            dh = self.dh[i] if trail is None else unit(V(trail[0], trail[1], 0.0))
            if wave and i > 0:
                dh = rz(wave * osc(u, n, -0.17 * i)) @ dh
            l = self.l[i]
            dz = h[2] - self.zg[i]
            if dz <= 1e-4:
                d = unit(V(dh[0], dh[1], min(0.6, -dz / l)))
            elif dz < l * 0.985:
                k = math.sqrt(l * l - dz * dz)
                d = V(dh[0] * k, dh[1] * k, -dz) / l
            else:
                d = unit(V(dh[0] * 0.18, dh[1] * 0.18, -1.0))
            p.aim_world(b, M.rot_to(self.d0[i], d))
            h = h + d * l


# ------------------------------------------------------------------------------------------------ more parts (yak, mule, maw, toad)
def rope(root, tip, r0, r1=None, sides=5, segs=3, bow=0.0, seed=0, t0=0.0, t1=1.0):
    """a matted lock / rope hanging from root to tip (tapered tube with a slight bow); t0..t1 = the stretch of it
    to build (so that the upper and the icy lower part can get different looks)"""
    rng = np.random.default_rng(seed)
    a = np.asarray(root, dtype=float); b = np.asarray(tip, dtype=float)
    r1 = r0 * 0.4 if r1 is None else r1
    d = b - a
    ud = unit(d)
    side = unit(np.cross(ud, (0, 1, 0)) if abs(ud[1]) < 0.9 else np.cross(ud, (1, 0, 0)))
    wob = rng.uniform(-1, 1, (segs + 1, 3)) * 0.008
    pts = []; rr = []
    for i in range(segs + 1):
        t = t0 + (t1 - t0) * i / segs
        pts.append(a + d * t + side * (bow * math.sin(math.pi * t)) + wob[i] * (1.0 if 0.02 < t < 0.98 else 0.0))
        rr.append(r0 * (1 - t ** 1.6) + r1 * t ** 1.6)
    p = M.tube(np.array(pts), np.array(rr), n=sides, cap0=True, cap1=True)
    p.smooth = 60.0
    return p


def keg(c, R, Ln, staves=12, missing=(3, 8), th=0.02, seed=0):
    """a cracked keg lying along Y at c -> (staves + end boards, hoops, glowing core): two staves are missing and the
    others have sprung, so the core shows"""
    rng = np.random.default_rng(seed)
    c = np.asarray(c, dtype=float)
    st = []
    wd = 2 * math.pi * R / staves * 0.9
    for i in range(staves):
        if i in missing:
            continue
        a = 2 * math.pi * (i + 0.5) / staves
        b = M.box(wd, Ln * rng.uniform(0.94, 1.0), th)
        b.rot('y', math.degrees(a))
        rr = R * rng.uniform(0.985, 1.03)
        b.move(c[0] + rr * math.sin(a), c[1] + rng.uniform(-0.01, 0.01), c[2] + rr * math.cos(a))
        st.append(b)
    for sg in (1, -1):
        st.append(M.tube(np.array([c + V(0, sg * Ln * 0.43, 0), c + V(0, sg * Ln * 0.46, 0)]), R * 0.95, n=12, up=(0, 0, 1)))
    wood = M.join(st)
    wood.smooth = 30.0
    hp = M.join([hoop((c[0], c[1] + k * Ln, c[2]), R + th * 0.75, R + th * 0.75, 0.009, n=14, m=4) for k in (-0.34, 0.0, 0.34)])
    hp.smooth = 60.0
    core = M.tube(np.array([c + V(0, -Ln * 0.44, 0), c + V(0, Ln * 0.44, 0)]), R - th * 0.9, n=12, up=(0, 0, 1))
    core.smooth = 60.0
    return wood, hp, core


def spear(base, top, r=0.011, head=0.17, hw=0.026):
    """a spear standing from base to top: shaft + leaf blade -> (shaft, blade)"""
    a = np.asarray(base, dtype=float); b = np.asarray(top, dtype=float)
    d = unit(b - a)
    Ln = float(np.linalg.norm(b - a))
    sh = M.tube(np.array([a, a + d * (Ln - head * 0.8)]), r, n=5, up=(0, 1, 0))
    sh.smooth = 60.0
    bl = M.blade([(0.0, hw * 0.45, hw * 0.45, 0.012), (head * 0.3, hw, hw, 0.012), (head, 0.001, 0.001, 0.003)])
    bl.orient(d, zhint=(0, 1, 0), origin=a + d * (Ln - head))
    return sh, bl


def strap_over(bvh, y, x0, x1, w=0.03, th=0.007, n=13, lift=0.004, along='x', other=0.0):
    """a flat band lying on top of the host: across the body at y (along='x', from x0 to x1), or along the body at
    x = other (along='y', from y = x0 to x1). Returns None when it does not find the surface"""
    pts = []
    for t in np.linspace(x0, x1, n):
        x, yy = (t, y) if along == 'x' else (other, t)
        q, nrm = P.surf(bvh, (x, yy, 3.0), (0, 0, -1), dist=4.0)
        if q is not None:
            pts.append((x, yy, float(q[2]) + lift))
    if len(pts) < 3:
        return None
    p = M.tube(np.array(pts), th * 0.7071, n=4, r2=w * 0.7071, up=(0, 0, 1), phase=math.pi / 4)
    p.smooth = 30.0
    return p


def pot(c, s=1.0, n=14):
    """round-bellied clay pot with an open mouth, standing at c"""
    prof = [(0.085, 0.0), (0.13, 0.04), (0.155, 0.10), (0.15, 0.15), (0.115, 0.19), (0.105, 0.205), (0.125, 0.22), (0.11, 0.226),
            (0.09, 0.20), (0.085, 0.12)]
    p = M.revolve([(r * s, h * s) for r, h in prof], n=n, cap=True)
    p.move(c[0], c[1], c[2])
    p.smooth = 50.0
    return p


def k_crackwood(N, p):
    """old staves whose cracks (and a halo round them) leak the light of what is inside (as bparts.k_cglass, on wood)"""
    r = L.KINDS['wood'](N, p)
    e = p.get('emit', (0.55, 0.8, 1.0))
    cw = p.get('crack_w', 0.03)
    cr = N.voro(p.get('crack', 16.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.08, 6))
    line = N.mapr(cr, 0.0, cw, 1.0, 0.0)
    halo = N.mapr(cr, 0.0, cw * 5, 1.0, 0.0)
    amt = N.mapr(N.noise(p.get('patch', 4.0), 3, 0.6, vec=N.mapping(N.coord(), loc=(3.3, 1.7, 6.1))), 0.35, 0.6)
    em = N.math('MULTIPLY', N.math('ADD', line, N.math('MULTIPLY', N.math('POWER', halo, 2.0), p.get('halo', 0.3))), amt, clamp=True)
    r['color'] = N.mix(N.math('MULTIPLY', em, 0.7), r['color'], tuple(x * 0.6 for x in e))
    r['emit'] = (e, p.get('strength', 2.0))
    r['emask'] = em
    return r


L.KINDS.update(crackwood=k_crackwood)
L.GRIME.update(crackwood=L.GRIME['wood'])


def report_low(C, limit=-0.02):
    """build-log help: for every clip (but Spawn) whose mesh goes below `limit`, say which part is the lowest and when"""
    V0, W, comp, tris = C._collect()
    for name, c in C.clips.items():
        if name == 'Spawn' and c.get('under_ok'):
            continue
        worst = (0.0, None, 0)
        for f in range(c['n'] + 1):
            z = C.posed(V0, W, c['G'][f])[:, 2]
            i = int(np.argmin(z))
            if z[i] < worst[0]:
                worst = (float(z[i]), int(comp[i]), f)
        if worst[1] is not None and worst[0] < limit:
            p = C.parts[worst[1]]
            B.log("NOTE %s %s: lowest point %.3f at frame %d/%d on part look=%s bone=%s (ntri %d)" % (
                C.id, name, worst[0], worst[2], c['n'], p.look, p.bone, p.ntri))


def host_bone(part, pt, among=None):
    """name of the bone with the largest weight at the vertex of the skinned Part nearest to pt (the bone a rigid
    thing seated at pt should ride on); among = restrict the choice to these bones"""
    i = int(np.argmin(np.linalg.norm(part.v - np.asarray(pt, dtype=float), axis=1)))
    w = part.W[i]
    if among:
        ids = [M.RIG.idx[b] for b in among]
        return M.RIG.names[ids[int(np.argmax(w[ids]))]]
    return M.RIG.names[int(np.argmax(w))]


def k_wfur(N, p):
    """white winter pelt: bparts.k_bfur lifted towards `white` by `lift` (the pelt kinds are built for dark coats:
    their partings and clots turn a white animal grey)"""
    r = L.KINDS['bfur'](N, p)
    r['color'] = N.mix(p.get('lift', 0.5), r['color'], p.get('white', (0.90, 0.90, 0.88)))
    return r


L.KINDS.update(wfur=k_wfur)
L.GRIME.update(wfur=((0.20, 0.20, 0.21), 0.35))
