"""Head of Osaros v2, part 2: the lipless stapled mouth and its teeth, the crown that grew through the scalp (forge nails,
slumped candle stubs and their tallow), the broken sun-disc, and the dense head itself: hollowed behind its left side,
broken open, cracked, the mouth cut.  Everything is seated on the sculpt by ray casts (nothing floats)."""
import math
import numpy as np
import bpy, bmesh
import monlib as M
import ov2_sculpt as SC
import ov2_head as HD
from monlib import V, tube, ellipsoid, box, join, horn, revolve, loft, lerp, unit


# ====================================================================================================== mouth
def mouth_def(bvh, n=19, half=0.033):
    """the lipless mouth: columns across the face. Returns dict of arrays (x, ys = y of the skin, zm = the line where the
    teeth meet, hu / hl = height of the upper / lower opening) measured on the sculpt"""
    zs = np.linspace(1.555, 1.615, 61)
    prof = []
    for zz in zs:
        hit, _ = SC.ray(bvh, (0.0, -0.4, zz), (0, 1, 0))
        prof.append(hit[1] if hit is not None else -1.0)
    prof = np.array(prof)
    seg = (zs > 1.566) & (zs < 1.598)
    zl = float(zs[seg][np.argmax(prof[seg])])                                          # the lip line = deepest point between the lips
    xs = np.linspace(-half, half, n)
    u = xs / half
    zm = zl + 0.0010 - 0.0046 * np.abs(u) ** 2.2                                        # corners turned down: contempt, not a grin
    hu = 0.0122 * (1 - np.abs(u) ** 2.0) ** 0.6 + 0.0010
    hl = 0.0102 * (1 - np.abs(u) ** 2.0) ** 0.6 + 0.0008
    ys = []
    for x, z in zip(xs, zm):
        hit, _ = SC.ray(bvh, (x, -0.4, z), (0, 1, 0))
        ys.append(hit[1] if hit is not None else -0.09)
    ys = np.array(ys)
    for _ in range(2):                                                                  # a smooth dental arch
        ys[1:-1] = 0.25 * ys[:-2] + 0.5 * ys[1:-1] + 0.25 * ys[2:]
    M.log("mouth: lip line z=%.4f, skin y at centre %.4f, at the corners %.4f" % (zl, ys[n // 2], ys[0]))
    return dict(x=xs, ys=ys, zm=zm, hu=hu, hl=hl, zl=zl, half=half)


def mouth_slot(md, depth=0.014):
    """the block that is cut out of the face between the jaws (closed); its edges wander a little, like torn flesh"""
    rings = []
    for k, (x, y, z, a, b) in enumerate(zip(md['x'], md['ys'], md['zm'], md['hu'], md['hl'])):
        ja = 0.0011 * math.sin(k * 2.3 + 0.7) + 0.0007 * math.sin(k * 5.1)
        jb = 0.0010 * math.sin(k * 1.9 + 2.1) + 0.0006 * math.sin(k * 4.3 + 1.0)
        rings.append([(x, y - 0.03, z - b - jb), (x, y + depth, z - b - jb), (x, y + depth, z + a + ja), (x, y - 0.03, z + a + ja)])
    return loft(np.array(rings), cap0=True, cap1=True).recalc()


def _arch(md, x):
    """skin y, outward normal and tangent of the dental arch at x"""
    xs = md['x']
    y = float(np.interp(x, xs, md['ys']))
    dy = float(np.interp(x + 0.002, xs, md['ys']) - np.interp(x - 0.002, xs, md['ys'])) / 0.004
    t = unit([1.0, dy, 0.0])
    nrm = np.array([t[1], -t[0], 0.0])
    return y, nrm, t


def teeth(md, seed=3):
    """two rows of long teeth standing in the cut jaws, clenched: uneven, leaning, some snapped, one gone. Roots are sunk
    3 mm into the gums, the front faces sit ~1 mm behind the skin. Returns one Part."""
    rng = np.random.default_rng(seed)
    out = []
    widths = [0.0084, 0.0066, 0.0076, 0.0064, 0.0064, 0.0086]
    kinds = ['inc', 'inc', 'can', 'pre', 'pre', 'mol']
    broken = {(1, -1, 1), (1, 1, 2), (-1, -1, 3), (-1, 1, 0)}                           # (row, side, index): snapped off short
    for row in (1, -1):
        for side in (1, -1):
            s = 0.0003
            for k, (w, kd) in enumerate(zip(widths, kinds)):
                w = w * (0.94 if row < 0 else 1.0) * rng.uniform(0.9, 1.08)
                x = side * s                                                           # arc length -> x (the arch turns away towards the corners)
                for _ in range(4):
                    y0, n0, t0 = _arch(md, x)
                    x = side * (s + w / 2) * abs(t0[0])
                s += w + rng.uniform(0.0003, 0.0013)
                if abs(x) + w * 0.3 > md['half'] - 0.0015:
                    break
                if row > 0 and side > 0 and k == 4:
                    continue                                                           # one tooth long gone on the broken side
                y, nrm, tg = _arch(md, x)
                zm = float(np.interp(x, md['x'], md['zm']))
                h = float(np.interp(x, md['x'], md['hu' if row > 0 else 'hl']))
                Ln = h + 0.003 + (0.0008 if row > 0 else 0.0)                          # gum (sunk root) to the biting edge
                th = 0.0052 if kd in ('inc', 'can') else 0.0066
                tip_w = {'inc': 0.92, 'can': 0.34, 'pre': 0.72, 'mol': 0.9}[kd]
                tip_t = {'inc': 0.34, 'can': 0.34, 'pre': 0.6, 'mol': 0.7}[kd]
                if kd == 'can':
                    Ln += 0.0022
                Ln += rng.uniform(-0.0012, 0.0010)
                snap = (row, side, k) in broken
                rg = M.ring2d(6, 3.0, math.pi / 6)
                if snap:
                    Ln *= rng.uniform(0.45, 0.62)
                    prof = [(0.0, 0.80, 0.80), (0.3, 1.0, 1.0), (0.8, 1.03, 0.98), (1.0, 0.9, 0.9)]
                else:
                    prof = [(0.0, 0.80, 0.80), (0.22, 1.0, 1.0), (0.62, 1.04, 0.96), (0.9, 0.5 + 0.5 * tip_w, 0.5 + 0.5 * tip_t), (1.0, tip_w, tip_t)]
                rings = []
                for (tt, kw, kt) in prof:
                    zz = -row * tt * Ln                                                # local: root at 0, growing towards the bite
                    rings.append(np.stack([rg[:, 0] * w / 2 * kw, rg[:, 1] * th / 2 * kt, np.full(6, zz)], axis=1))
                R = np.array(rings)
                if snap:                                                               # the break: a slanted, splintered face
                    R[-1, :, 2] += -row * (rg[:, 0] * rng.uniform(-1, 1) + rg[:, 1] * rng.uniform(-1, 1)) * 0.0022 + rng.uniform(-0.0008, 0.0008, 6)
                t = loft(R, cap0=True, cap1=True).recalc()
                t.rot('y', rng.uniform(-9, 9)).rot('x', rng.uniform(-6, 4))
                Mx = np.stack([tg, nrm, np.array([0, 0, 1.0])], axis=1)                # local X -> arch tangent, Y -> outward, Z up
                t.v = t.v @ Mx.T
                recess = 0.0036 + (0.0011 if row < 0 else 0.0) + rng.uniform(-0.0004, 0.0008)   # the lower row closes behind the upper
                t.move(np.array([x, y, zm + row * (h + 0.003)]) - nrm * recess)
                out.append(t)
    tt = join(out)
    tt.smooth = 50.0
    return tt


def mouth_glow(md, back=0.0105):
    """the furnace light behind the clenched teeth: a curved strip standing inside the cut"""
    G = []
    for x, y, z, a, b in zip(md['x'], md['ys'], md['zm'], md['hu'], md['hl']):
        x = x * 0.78                                                                   # stays behind the teeth: never seen through the corners
        y = float(np.interp(x, md['x'], md['ys']))
        G.append([(x, y + back, z + a + 0.003), (x, y + back, z), (x, y + back, z - b - 0.003)])
    s = M.sheet(np.array(G).transpose(1, 0, 2))
    s.smooth = 180.0
    return s


STAPLES = (-0.0200, 0.0010, 0.0205)


def staple_anchors(md, bvh, xs=STAPLES):
    """where each staple enters the face above and below the mouth: [(top point, top normal, bottom point, bottom normal, x mid)]"""
    out = []
    for k, x in enumerate(xs):
        zm = float(np.interp(x, md['x'], md['zm']))
        zt = zm + float(np.interp(x, md["x"], md["hu"])) + 0.0060 + 0.0012 * math.sin(k * 2.1)
        zb = zm - float(np.interp(x, md["x"], md["hl"])) - 0.0058 - 0.0012 * math.cos(k * 1.7)
        x2 = x + (0.0036 if k % 2 else -0.0040)
        a, na = SC.ray(bvh, (x, -0.4, zt), (0, 1, 0))
        b, nb = SC.ray(bvh, (x2, -0.4, zb), (0, 1, 0))
        out.append((a, unit(na), b, unit(nb), (x + x2) / 2))
    return out


def lip_rolls(h, md):
    """what is left of the lips: two shrunken, burnt rolls of flesh drawn tight along the cut (dense mesh, in place)"""
    for row in (1, -1):
        pts = []
        for x, y, z, a, b in zip(md['x'], md['ys'], md['zm'], md['hu'], md['hl']):
            pts.append((x, y - 0.002, z + row * ((a if row > 0 else b) + 0.0042)))
        pts = np.array(pts)
        u = np.abs(md['x']) / md['half']
        SC.line(h, pts, 0.0062, 1.0, kind='round', vec=(0, -0.0034 if row > 0 else -0.0030, 0.0), taper=0.18)
    return h


def staple_puckers(h, anchors, md):
    """the flesh gathered and drawn up round every staple leg: a raised, creased knot of lip, no hole (dense mesh, in place)"""
    for (a, na, b, nb, xm) in anchors:
        zl = float(np.interp(xm, md['x'], md['zm']))
        for p in (a, b):
            SC.puff(h, p, 0.0075, 0.0024)
            SC.grab(h, p, 0.0105, (0.0, -0.0006, 0.0018 if p[2] < zl else -0.0018))   # flesh dragged towards the bridge
            for k in range(5):                                                          # radial pucker creases round the leg
                ang = k * 2 * math.pi / 5 + 0.4
                d = np.array([math.cos(ang), 0.0, math.sin(ang)])
                SC.line(h, [p + d * 0.0032, p + d * 0.0085], 0.0013, -0.0007, taper=0.4)
    return h


def staples(md, bvh, anchors):
    """forge staples driven through both jaws, the way the Order seals its own mouths: square iron, legs sunk in the face"""
    out = []
    for k, (a, na, b, nb, xm) in enumerate(anchors):
        zm = float(np.interp(xm, md['x'], md['zm']))
        y_mid, n_mid, _ = _arch(md, xm)
        mid = np.array([xm, y_mid, zm]) + n_mid * 0.0052                              # the bridge lies flat across the lips
        ta = a + na * 0.0034; tb = b + nb * 0.0034
        path = [a - na * 0.008, ta, lerp(ta, mid, 0.85) + (ta - tb) * 0.08, lerp(tb, mid, 0.85) + (tb - ta) * 0.08, tb, b - nb * 0.008]
        st = tube(np.array(path), 0.0038, n=4, r2=0.0032, up=(1, 0, 0), phase=math.pi / 4)
        st.smooth = 28.0
        out.append(st)
    s = join(out)
    s.smooth = 28.0
    return s


# ====================================================================================================== crown
# (azimuth deg (-90 = front, 0 = his left), elevation deg, length m, lean deg, base radius)
NAILS = [(-90, 45, 0.134, 0, 0.0062), (-63, 41, 0.096, 6, 0.0056), (-118, 42, 0.108, -5, 0.0058), (-33, 37, 0.070, 18, 0.0052), (-150, 39, 0.122, -8, 0.006),
         (9, 36, 0.090, 12, 0.0056), (171, 38, 0.086, -10, 0.0056), (47, 42, 0.108, 4, 0.0058), (133, 43, 0.100, -3, 0.0058),
         (-76, 58, 0.062, -7, 0.0046), (-104, 60, 0.074, 9, 0.0048), (-135, 26, 0.052, -14, 0.0044), (90, 60, 0.082, 3, 0.005), (-48, 24, 0.040, 22, 0.0042)]
# (azimuth, elevation, height, radius)
CANDLES = [(-97, 70, 0.034, 0.0135), (-164, 55, 0.042, 0.0125), (22, 62, 0.030, 0.013), (-40, 56, 0.026, 0.0115), (128, 60, 0.032, 0.012)]
HEAD_C = np.array([0.0, -0.012, 1.705])


def crown_point(bvh, az, el):
    d = np.array([math.cos(math.radians(az)) * math.cos(math.radians(el)), math.sin(math.radians(az)) * math.cos(math.radians(el)),
                  math.sin(math.radians(el))])
    hit, nrm = SC.ray(bvh, HEAD_C + d * 0.4, -d)
    return hit, unit(nrm), d


def crown_welts(h, bvh):
    """scar tissue where the nails and the candle stubs grew through the scalp (dense mesh, in place)"""
    for (az, el, ln, lean, r0) in NAILS:
        p, n, d = crown_point(bvh, az, el)
        SC.puff(h, p, 0.0135 * r0 / 0.0056, 0.0030)
        SC.puff(h, p, 0.0065 * r0 / 0.0056, -0.0016, kind='round')
    for (az, el, ht, r) in CANDLES:
        p, n, d = crown_point(bvh, az, el)
        SC.puff(h, p, 0.024, 0.0022)
    return h


def nails(bvh, seed=5):
    """forge nails standing out of the skull: square shanks hammered to a point, some bent. Returns (Part, info)"""
    rng = np.random.default_rng(seed)
    out = []; info = []
    for (az, el, ln, lean, r0) in NAILS:
        p, n, d = crown_point(bvh, az, el)
        side = unit(np.cross(n, (0, 0, 1)))
        ax = unit(n * 0.55 + np.array([0, 0, 1.0]) * 0.62 + side * math.sin(math.radians(lean)) * 0.5)
        k = 7
        t = np.linspace(0, 1, k)
        bend = side * 0.014 * ln / 0.1 * math.sin(math.radians(lean) * 2)
        path = np.array([p - ax * 0.010 + ax * (ln + 0.010) * tt + bend * tt ** 2 for tt in t])
        r = r0 * (1 - t) ** 0.8 + 0.0007
        r[0] = r0 * 1.04; r[1] = max(r[1], r0 * 0.97)
        nl = tube(path, r, n=4, up=side, phase=math.pi / 4 + rng.uniform(-0.3, 0.3), twist=rng.uniform(-0.5, 0.5))
        nl.smooth = 25.0
        out.append(nl)
        info.append((p, ax, ln))
    q = join(out)
    q.smooth = 25.0
    return q, info


def candles(bvh, seed=11):
    """burnt-out stubs slumped on the scalp: melted lumps with a sunken crown, a black wick, tallow running down the skull.
    Returns (wax Part, wick Part)"""
    rng = np.random.default_rng(seed)
    wax = []; wick = []; emb = []
    for k, (az, el, ht, r) in enumerate(CANDLES):
        p, n, d = crown_point(bvh, az, el)
        up = unit(n * 0.6 + np.array([0, 0, 1.0]))
        prof = [(2.3 * r, -0.004), (1.9 * r, 0.10 * ht), (1.35 * r, 0.26 * ht), (1.05 * r, 0.55 * ht), (1.0 * r, 0.80 * ht), (1.16 * r, 0.96 * ht),
                (1.10 * r, 1.04 * ht), (0.72 * r, 0.97 * ht), (0.30 * r, 0.84 * ht), (0.0, 0.80 * ht)]
        c = revolve(prof, 11, cap=False)
        a = np.arctan2(c.v[:, 1], c.v[:, 0])
        ph = rng.uniform(0, 6.28, 3)
        zt = np.clip(c.v[:, 2] / ht, 0, 1)
        lobes = 1.0 + 0.16 * np.sin(3 * a + ph[0]) * (1 - zt) + 0.10 * np.sin(5 * a + ph[1])     # runs standing out of the wall as ribs
        c.v[:, 0] *= lobes; c.v[:, 1] *= lobes
        slump = M.smoothstep(0.45, 1.0, zt) * (0.5 + 0.5 * np.cos(a - ph[2]))                       # one side melted lower
        c.v[:, 2] -= slump * 0.34 * ht
        c.v[:, 0] += 0.22 * r * zt * math.cos(ph[2]); c.v[:, 1] += 0.22 * r * zt * math.sin(ph[2])  # leaning
        c.rough(r * 0.07, 90.0, seed=float(seed + k), octaves=2)
        wk = tube([(0, 0, 0.74 * ht), (r * 0.12, 0.0, 0.92 * ht), (r * 0.42, r * 0.1, 1.04 * ht)], [r * 0.13, r * 0.11, r * 0.07], n=4)
        Rm = M.rot_to((0, 0, 1), up)
        for q, lst in ((c, wax), (wk, wick)):
            q.tf(Rm).move(p - up * 0.003)
            lst.append(q)
        if k != 1:                                                                     # most wicks still hold a coal
            tip = Rm @ np.array([r * 0.42, r * 0.1, 1.04 * ht]) + p - up * 0.003
            emb.append(ellipsoid(0.0026, 0.0026, 0.0034, n=8, m=5).move(tip))
        nrun = 4 if k == 0 else 3                                                      # tallow that ran down the skull and set
        for j in range(nrun):
            a0 = rng.uniform(0, 6.28) if j else rng.uniform(-0.6, 0.6) + math.pi * 1.5
            e1 = unit(np.cross(n, (0, 0, 1))); e2 = np.cross(n, e1)
            st = p + (e1 * math.cos(a0) + e2 * math.sin(a0)) * r * 1.7
            Ln = rng.uniform(0.018, 0.045) * (1.9 if (k == 0 and j == 0) else 1.0)
            pts, nrs = SC.walk(bvh, st, (0, 0, -1), Ln, step=0.007, wander=0.4, seed=seed * 7 + k * 5 + j, pull=(0, 0, -1), pull_w=0.7)
            if len(pts) < 3:
                continue
            m = len(pts)
            rr = np.linspace(0.0040, 0.0024, m) * rng.uniform(0.8, 1.15)
            rr[-1] = rr[-2] * 1.55; rr[-2] *= 1.25                                     # the drop it ends in
            path = pts + nrs * (rr * 0.35)[:, None]
            dr = tube(path, rr * 0.8, n=5, r2=rr * 1.15, up=nrs[0])
            dr.smooth = 80.0
            wax.append(dr)
    a = join(wax); a.smooth = 75.0
    b = join(wick); b.smooth = 40.0
    e = join(emb); e.smooth = 180.0
    return a, b, e


HALO_C = np.array([0.0, 0.082, 1.752])
HALO_TILT = -13.0          # degrees about X: the top leans back
HALO_ARCS = [(34.0, 113.0), (127.0, 177.0)]          # what is left of the disc: two pieces, a wedge knocked out between them


def _halo_pt(a_deg, r, off=0.0):
    a = math.radians(a_deg)
    p = np.array([r * math.cos(a), off, r * math.sin(a)])
    return M.rot_axis('x', HALO_TILT) @ p + HALO_C


def _disc(c, axis, r, th):
    d = revolve([(r, -th / 2), (r, th / 2)], 8)
    d.tf(M.rot_to((0, 0, 1), axis)).move(c)
    return d


def halo(bvh, seed=2, R0=0.124, R1=0.176, th=0.0095):
    """the broken sun-disc behind his head: a thick iron annulus with long and short rays; its lower part is gone, a wedge
    is knocked out of the arc and the breaks are jagged; struts hold each piece to the back of the skull.
    Returns (Part, list of strut root points)"""
    rng = np.random.default_rng(seed)
    P = []
    ydir = M.rot_axis('x', HALO_TILT) @ np.array([0, 1.0, 0])
    kray = 0
    for (A0, A1) in HALO_ARCS:
        segs = max(8, int((A1 - A0) / 3.2))
        aa = np.linspace(A0, A1, segs)
        rings = []
        for i, a in enumerate(aa):
            e = 0.0; sk = 0.0
            if i < 3:                                                                  # the breaks: stepped, splintered
                e = (3 - i) * 0.013 * (1.0 if i % 2 else 0.45); sk = (3 - i) * 1.6
            if i > segs - 4:
                e = (i - (segs - 4)) * 0.012 * (1.0 if i % 2 else 0.5); sk = -(i - (segs - 4)) * 1.6
            ro = R1 - e * 0.55; ri = R0 + e * 0.55
            b = 0.0035
            rings.append([_halo_pt(a + sk * 0.4, ri, -th / 2), _halo_pt(a + sk, ro, -th / 2), _halo_pt(a + sk, ro + b, 0.0), _halo_pt(a - sk * 0.3, ro, th / 2),
                          _halo_pt(a, ri, th / 2), _halo_pt(a + sk * 0.2, ri - b, 0.0)])
        P.append(loft(np.array(rings), cap0=True, cap1=True).recalc())
        # an inner bead, so the plate reads as forged work
        bead = tube(np.array([_halo_pt(a, R0 + 0.011, -th / 2 - 0.0005) for a in aa[2:-2]]), 0.0032, n=4, phase=math.pi / 4)
        P.append(bead)
        for a in np.arange(A0 + 7.5, A1 - 5, 10.4):
            long_ = (kray % 2 == 0)
            Ln = (0.122 if long_ else 0.064) * rng.uniform(0.86, 1.08)
            if kray in (3, 9):
                Ln *= 0.42                                                             # rays snapped short
            w = 0.023 if long_ else 0.016
            aj = a + rng.uniform(-1.2, 1.2)
            base = R1 - 0.014
            rg = []
            for t in (0.0, 0.18, 0.55, 0.85, 1.0):
                r = base + (Ln + 0.014) * t
                hw = w / 2 * (1 - t) ** 0.85 + 0.0009
                ht = th * 0.42 * (1 - 0.75 * t) + 0.0005
                c = _halo_pt(aj, r, 0.0)
                tdir = unit(_halo_pt(aj + 90, 1.0, 0.0) - HALO_C)
                rg.append([c - tdir * hw, c - ydir * ht * 1.4, c + tdir * hw, c + ydir * ht * 1.4])
            P.append(loft(np.array(rg), cap0=True, cap1=True).recalc())
            P.append(_disc(_halo_pt(aj, R1 - 0.02, -th / 2), ydir, 0.0048, 0.004))      # the rivet it is forged on with
            kray += 1
    roots = []
    for a in (56, 100, 150):                                                          # struts into the back of the skull
        q = _halo_pt(a, (R0 + R1) / 2, -th * 0.2)
        d = unit(HEAD_C + np.array([0, 0.03, 0.03]) - q)
        hit, nrm = SC.ray(bvh, q, d)
        if hit is None:
            continue
        P.append(tube([q, hit + d * 0.007], 0.0056, n=4, phase=math.pi / 4))
        P.append(_disc(q, d, 0.012, 0.005))
        roots.append(hit)
    h = join(P)
    h.smooth = 24.0
    return h, roots


# ====================================================================================================== the dense head
# the breach: the left cheekbone, temple and outer orbit are gone. One ragged hole (an irregular outline punched through
# the shell along the surface normal) and two small ones beside it.   (centre, (radius along the head, radius up), seed, points)
BREACH = [((0.067, -0.075, 1.668), (0.046, 0.056), 1, 27)]


def breach_cutter(bvh, centre, R, seed, k=27, depth=0.036):
    rng = np.random.default_rng(seed)
    p, n, _ = SC.nearest(bvh, centre)
    n = unit(n)
    e2 = unit(np.array([0, 0, 1.0]) - n * n[2])
    e1 = np.cross(e2, n)
    if e1[0] + e1[1] < 0:                       # +u = towards the back of the head, -u = towards the eye
        e1 = -e1
    u = rng.uniform(0, 1, k)
    for _ in range(2):                                                                 # neighbouring points belong together: lobes, not a star
        u = 0.25 * np.roll(u, 1) + 0.5 * u + 0.25 * np.roll(u, -1)
    u = (u - u.min()) / max(u.max() - u.min(), 1e-6)
    out = []
    for i in range(k):
        a = 2 * math.pi * (i + rng.uniform(-0.33, 0.33)) / k
        r = 1.0 / math.sqrt((math.cos(a) / R[0]) ** 2 + (math.sin(a) / R[1]) ** 2)
        r *= 0.66 + 0.40 * u[i]
        q = rng.uniform()
        if q < 0.22:
            r *= rng.uniform(0.60, 0.78)                                               # a bite
        elif q > 0.86:
            r *= rng.uniform(1.08, 1.2)                                                # a shard left standing
        out.append((max(r * math.cos(a), -0.0225), r * math.sin(a)))                    # never as far as the ember in the orbit
    pr = M.prism(out, -depth, 0.02)
    Mx = np.stack([e1, e2, n], axis=1)
    pr.v = pr.v @ Mx.T + p
    if np.linalg.det(Mx) < 0:
        pr.flip()
    return pr.recalc()
# cracks leaving the breach: (start, direction, length, width, depth, wander, seed)
CRACKS = [((0.036, -0.112, 1.712), (-1.0, -0.1, 0.35), 0.100, 0.0042, 0.012, 0.45, 1),     # across the forehead, over the right brow
          ((0.058, -0.078, 1.728), (-0.25, 0.3, 1.0), 0.115, 0.0040, 0.012, 0.5, 2),      # up over the cranium
          ((0.046, -0.096, 1.622), (-0.35, -0.2, -1.0), 0.075, 0.0036, 0.010, 0.5, 3),    # down the cheek to the jaw
          ((0.030, -0.112, 1.648), (-1.0, -0.4, -0.25), 0.045, 0.0030, 0.009, 0.4, 4),    # over the bridge of the nose
          ((0.090, 0.004, 1.672), (0.2, 1.0, 0.1), 0.085, 0.0038, 0.011, 0.5, 5),         # round behind the ear
          ((0.074, -0.034, 1.726), (0.3, 0.8, 0.7), 0.075, 0.0034, 0.010, 0.5, 6),
          ((0.066, -0.068, 1.604), (0.2, 0.3, -1.0), 0.055, 0.0032, 0.009, 0.5, 7)]
BRANCH = [(0, 0.45, (-0.3, 0.2, 1.0), 0.05, 11), (1, 0.5, (-1.0, 0.1, 0.2), 0.045, 12), (2, 0.5, (-1.0, 0.0, -0.2), 0.03, 13),
          (4, 0.5, (0.2, 0.4, 1.0), 0.04, 14), (0, 0.8, (-0.4, 0.0, -1.0), 0.028, 15)]
EYES = ((1, 0.0050), (-1, 0.0038))               # (side, radius): the ember on the broken side is the larger


def cavity(bvh, wall=0.0118):
    """the hollow behind the left side of the face and under the cranium: a blob kept `wall` inside the skin"""
    c = ellipsoid(0.064, 0.080, 0.068, n=22, m=13).move(0.016, -0.012, 1.702)
    for it in range(3):
        for i, p in enumerate(c.v):
            q, n, d = SC.nearest(bvh, p)
            n = unit(n)
            inside = np.dot(p - q, n) < 0
            w = wall * (1.0 + 0.3 * M.fbm(p, 30.0, 4.0, 2))
            if (not inside) or d < w:
                c.v[i] = q - n * w
        M.smooth_verts(c, 1, 0.35)
    for i, p in enumerate(c.v):                                                        # final guarantee
        q, n, d = SC.nearest(bvh, p)
        n = unit(n)
        if np.dot(p - q, n) > 0 or d < wall * 0.6:
            c.v[i] = q - n * wall * 0.7
    c.recalc()
    return c


def eye_points(bvh):
    """(side, radius, centre of the ember, the point of the orbit's back wall it sits on)"""
    out = []
    for sx, r in EYES:
        hit, _ = SC.ray(bvh, (sx * 0.0315, -0.4, HD.EYE_Z), (0, 1, 0))
        out.append((sx, r, hit + np.array([0, r * 0.35, 0]), hit))
    return out


def build_dense(levels=2):
    """everything of the head at sculpt density. Returns dict:
       skin = dense closed head (hollowed, broken, cracked, mouth cut), fo = per-face class of skin ('ash' | 'core' | 'maw'),
       bvh0 = BVH of the unbroken sculpt, md = mouth definition, + the rigid parts"""
    h = HD.base_head(levels)
    HD.sculpt(h)
    HD.wrinkles(h)
    bvh_a = SC.bvh_of(h)
    crown_welts(h, bvh_a)
    md = mouth_def(bvh_a)
    lip_rolls(h, md)
    bvh_a = SC.bvh_of(h)
    anchors = staple_anchors(md, bvh_a)
    staple_puckers(h, anchors, md)
    h.rough(0.00055, 55.0, seed=3.0, octaves=2)
    h.recalc()
    sculpt0 = h.copy()
    bvh0 = SC.bvh_of(sculpt0)
    D = dict(bvh0=bvh0, md=md, sculpt0=sculpt0)
    # ---- rigid parts seated on the unbroken sculpt
    anchors = staple_anchors(md, bvh0)
    D['teeth'] = teeth(md)
    D['glow'] = mouth_glow(md)
    D['staples'] = staples(md, bvh0, anchors)
    D['nails'], D['nail_info'] = nails(bvh0)
    D['wax'], D['wick'], D['wick_ember'] = candles(bvh0)
    D['halo'], D['halo_roots'] = halo(bvh0)
    # ---- hollow, break, crack, cut the mouth
    cav = cavity(bvh0)
    hollow = join([h, cav.copy().flip()])
    cutters = [mouth_slot(md)]
    for (c, R, sd, k) in BREACH:
        cutters.append(breach_cutter(bvh0, c, R, sd, k))
    paths = []
    for (st, d, ln, w, dp, wd, sd) in CRACKS:
        pts, nrs = SC.walk(bvh0, st, d, ln, step=0.0045, wander=wd, seed=sd)
        paths.append((pts, nrs, w, dp))
    for (ci, t, d, ln, sd) in BRANCH:
        pts0 = paths[ci][0]
        st = pts0[int(t * (len(pts0) - 1))]
        pts, nrs = SC.walk(bvh0, st, d, ln, step=0.0045, wander=0.5, seed=sd)
        paths.append((pts, nrs, paths[ci][2] * 0.7, paths[ci][3] * 0.8))
    for (pts, nrs, w, dp) in paths:
        if len(pts) >= 3:
            cutters.append(SC.wedge(pts, nrs, w, min(dp, 0.0078)))          # never through the ~1 cm shell (no stray holes)
    D['crack_paths'] = [p[0] for p in paths]
    skin = SC.boolean_multi(hollow, cutters)
    skin.recalc()
    # ---- classify the faces: skin / inner + fracture / mouth cut
    fc = skin.face_centers()
    cls = []
    for i, c in enumerate(fc):
        q, n, d = SC.nearest(bvh0, c)
        if d < 0.0006:
            cls.append('ash')
        elif abs(c[0]) < md['half'] + 0.004 and abs(c[2] - md['zl']) < 0.022 and c[1] < float(np.interp(c[0], md['x'], md['ys'])) + 0.02:
            cls.append('maw')
        else:
            cls.append('core')
    D['skin'] = skin
    D['fo'] = cls
    D['cav'] = cav
    M.log("dense head: %d tris; faces ash %d, core %d, maw %d" % (skin.ntri, cls.count('ash'), cls.count('core'), cls.count('maw')))
    # ---- eyes: an ember deep in each orbit (the one in the broken side is larger)
    D['eye_pts'] = eye_points(bvh0)
    eyes = [ellipsoid(r, r * 0.85, r, n=10, m=7).move(c) for (sx, r, c, hit) in D['eye_pts']]
    D['eyes'] = join(eyes)
    D['eyes'].smooth = 180.0
    # ---- clay preview groups
    sk_a = skin.sub(np.array([c == 'ash' for c in cls])); sk_c = skin.sub(np.array([c != 'ash' for c in cls]))
    sk_a.smooth = 50.0; sk_c.smooth = 30.0
    D['all'] = [(sk_a, (0.5, 0.48, 0.45)), (sk_c, (0.9, 0.3, 0.05)), (D['teeth'], (0.6, 0.52, 0.36)), (D['glow'], (1.0, 0.4, 0.05)),
                (D['staples'], (0.08, 0.08, 0.08)), (D['nails'], (0.08, 0.08, 0.08)), (D['wax'], (0.75, 0.7, 0.55)), (D['wick'], (0.02, 0.02, 0.02)),
                (D['halo'], (0.1, 0.1, 0.1)), (D['eyes'], (1.0, 0.5, 0.1))]
    return D
