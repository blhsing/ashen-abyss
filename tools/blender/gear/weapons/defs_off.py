"""Off-hand items: shield (forearm), focus, totem, grimoire, lantern (held in hand_l).
Hand items use the grip frame of charlib (origin = centre of the grip, +Z out of the thumb side of the fist, +X towards
the knuckles, +Y palm normal). With the arm hanging, +X points down: grimoire and lantern hang towards +X.
Shield frame: origin = centre of the forearm strap ON the forearm axis, +X elbow -> hand, +Y = face (outward), +Z up."""
from gearparts import *
from defs import base

OFF = dict(mount="hand_l", grip="l", hands=1)
SHIELD = dict(mount="forearm_l", grip="l", hands=1,
              hint="origin = forearm strap centre on the forearm axis; +X elbow->hand, +Y face (outward), +Z up; hand bar at x=+0.258 y=-0.031")
S_BACK, S_OUT = 0.07, 0.088          # kit_twn board space -> shield space is T(0.085, S_OUT, up)


def shield_parts(w, parts, M):
    """add kit_twn style part tuples [(geo, cls, src, kw)] (board space) through M"""
    for (g_, cls, src, kw) in parts:
        w(gxf(g_, M), src, **kw)


def face_rivets(pts, yfn, r=0.007):
    return gjoin(*[ydome([(0.0, r * 0.7), (r * 0.7, r * 0.5), (r, 0.0)], n=6, x0=x, y0=float(yfn(x, z)), z0=z) for (x, z) in pts])


# =============================================================================================== shield 盾
@base("shield", info=SHIELD, texel=0.0016, sheet=dict(quarter=-150.0, icon_roll=0.0))
def shield(w, v):
    if v == 2:
        # soldier's heater: steel face over a wooden core, brass rim and rivets, ember crest in relief, enarmes and arm pad
        M = T(0.085, S_OUT, 0.0)
        W_, ZT, ZB = 0.222, 0.255, -0.395
        wf, yf = heater_w(W_, straight=0.34), shield_y(W_, 0.034, 0.65, -0.07)
        w(gxf(g_shield_board(wf, yf, ZB, ZT, nu=11, nv=14), M), "steel", recalc=False, solidify=0.004, solid_offset=-1.0, sharp=40)
        yb = lambda x, z: yf(x, z) - 0.0042
        wb = lambda V: heater_w(W_, straight=0.34)(V) - 0.004
        w(gxf(g_shield_board(wb, yb, ZB + 0.004, ZT - 0.003, nu=11, nv=14), M), "woodbox", recalc=False, solidify=0.010, solid_offset=-1.0, sharp=40)
        w(gxf(g_tube(shield_outline(wf, yf, ZB, ZT, nv=16), 1.0, n=6, closed=True, sx=0.0105, sy=0.0125, up=(0, 1, 0)), M), "brass", sharp=50)
        crest = [conform(gxf(g_prism([(-0.060, -0.030), (0.0, 0.036), (0.060, -0.030), (0.042, -0.030), (0.0, 0.014), (-0.042, -0.030)], 0.008),
                             T(0, 0.004, 0.120)), yf),
                 conform(gxf(g_prism(rhomb(0.032, 0.062), 0.010), T(0, 0.005, 0.012)), yf),
                 conform(gxf(g_prism(rhomb(0.014, 0.028), 0.014), T(0, 0.007, 0.012)), yf),
                 conform(g_box(0.026, 0.006, 0.215, (0, 0.003, -0.205)), yf),
                 conform(gxf(g_prism(bar(0.058, 0.010), 0.008), T(0, 0.004, -0.085)), yf)]
        w(gxf(gjoin(*crest), M), "brass", bevel=0.0015, sharp=40)
        rv = [(-0.172, 0.200), (0.172, 0.200), (-0.180, 0.060), (0.180, 0.060), (-0.150, -0.110), (0.150, -0.110),
              (-0.086, -0.255), (0.086, -0.255), (-0.090, 0.215), (0.090, 0.215), (0.0, 0.222)]
        w(gxf(face_rivets(rv, yf, 0.0068), M), "brass", sharp=60)
        shield_parts(w, shield_back(yf, S_BACK, S_OUT, 0.0, thick=0.0145), M)
    w.set(mount="forearm_l")


# =============================================================================================== focus 法器
@base("focus", info=OFF, texel=0.0008)
def focus(w, v):
    if v == 2:
        # scrying orb: a sphere of fitted blue crystal plates lit by a core within, three bronze talons over a cup, leather grip
        w(tube_w([(0, 0, -0.056), (0, 0, 0.056)], 0.0118, 10), "grip_black", sharp=60)
        w(grip_wrap(-0.046, 0.046, 0.0118, turns=7.5, width=0.0060, thick=0.0020, oval=1.0), "grip_black", sharp=70)
        w(rings((-0.052, 0.052), 0.0130, h=0.008, n=12, bulge=1.14), "bronze", sharp=50)
        w(g_lathe([(0.0118, -0.056), (0.0165, -0.066), (0.0150, -0.080), (0.0070, -0.094), (0.0, -0.098)], n=10), "bronze", sharp=40)
        w(g_lathe([(0.0118, 0.056), (0.0105, 0.068), (0.0170, 0.078), (0.0260, 0.086), (0.0240, 0.094), (0.0120, 0.092), (0.0, 0.090)], n=12),
          "bronze", sharp=40)
        oz, orr = 0.142, 0.0450
        claw = quillon([(0.020, 0, 0.088), (0.044, 0, 0.104), (0.0525, 0, oz), (0.0445, 0, oz + 0.032), (0.0270, 0, oz + 0.046)],
                       [0.0062, 0.0058, 0.0050, 0.0040, 0.0012], n=6, flat=0.8)
        w(radial(claw, 3, 30), "bronze", sharp=50)
        Mo = T(0, 0, oz) @ R('Z', 12) @ R('X', 8)
        add_tones(w, crystal_plates(orr, level=1, shrink=0.895, depth=0.0030, seed=4), "crystal_blue", M=Mo)
        cv, cf = icosphere(1)
        w(gxf(geo(cv * (orr - 0.0012), cf), Mo), "star", sharp=8, heat=0.36)
        w(g_lathe([(0.0, oz + orr * 0.93), (0.0090, oz + orr * 0.95), (0.0070, oz + orr + 0.006), (0.0, oz + orr + 0.014)], n=8), "bronze", sharp=40)


# =============================================================================================== totem 圖騰
def totem_face(z, wx=0.062, dy=0.027, s=1.0, eye="oldbone", dark="ebonybox", mouth=None, style="ring"):
    """carved mask on the +Y side of a totem block: returns [(geo, src, kw)].
    style "ring": round sockets rimmed with bone (v3 lights them); "slit": narrow slanted eye slits cut under a heavy brow,
    with knife-cut tear lines below them."""
    out = []
    if style == "slit":
        sl = []
        for sx_ in (1, -1):
            eye_g = g_prism([(-0.0140 * s, 0.0), (-0.0020 * s, 0.0052 * s), (0.0120 * s, 0.0030 * s), (0.0145 * s, -0.0008 * s), (0.0010 * s, -0.0046 * s)], 0.0050)
            sl.append(gxf(eye_g, T(sx_ * 0.0160 * s, dy - 0.0030, z + 0.0225 * s) @ S(sx_, 1, 1) @ R('Y', 14)))
            sl.append(g_box(0.0036 * s, 0.0040, 0.0220 * s, (sx_ * 0.0185 * s, dy - 0.0026, z + 0.0030 * s)))
            sl.append(g_box(0.0030 * s, 0.0040, 0.0150 * s, (sx_ * 0.0250 * s, dy - 0.0026, z + 0.0060 * s)))
        out.append((gjoin(*sl), dark, dict(sharp=40)))
        lids = [gxf(g_box(0.0300 * s, 0.0085, 0.0050 * s), T(sx_ * 0.0165 * s, dy + 0.0005, z + 0.0315 * s) @ R('Y', -14 * sx_)) for sx_ in (1, -1)]
        out.append((gjoin(*lids), "woodbox", dict(bevel=0.0018, sharp=40)))
    else:
        ey = [ydome([(0.0125 * s, 0.0), (0.0125 * s, 0.0045), (0.0085 * s, 0.0050), (0.0080 * s, 0.0010)], n=10, x0=sx_ * 0.0170 * s, y0=dy,
                    z0=z + 0.022 * s) for sx_ in (1, -1)]
        out.append((gjoin(*ey), eye, dict(sharp=40)))
        pu = [ydome([(0.0, 0.0012), (0.0082 * s, 0.0012)], n=10, x0=sx_ * 0.0170 * s, y0=dy, z0=z + 0.022 * s) for sx_ in (1, -1)]
        out.append((gjoin(*pu), dark, dict(sharp=40)))
    beak = gxf(g_lathe([(0.0, 0.0), (0.0125 * s, 0.0), (0.0090 * s, 0.014 * s), (0.0, 0.034 * s)], n=4, sx=0.8, sy=1.5), T(0, dy - 0.002, z - 0.004 * s) @ R('X', -90) @ R('X', -22))
    out.append((beak, "woodbox", dict(sharp=30)))
    brow = g_box(wx * 0.98, 0.010, 0.010 * s, (0, dy - 0.001, z + 0.040 * s))
    out.append((brow, "woodbox", dict(bevel=0.003, sharp=40)))
    mouth_g = g_box(0.030 * s, 0.006, 0.006 * s, (0, dy - 0.001, z - 0.030 * s))
    out.append((mouth_g, mouth or dark, dict(sharp=40)))
    return out


@base("totem", info=OFF, texel=0.0008)
def totem(w, v):
    if v == 2:
        # spirit totem: a two-faced idol with slit eyes carved from one block, antler crown, hide-bound handle, feathers and bone beads
        w(haft(-0.090, 0.085, 0.0150, 0.0165, n=10, seg=5, bulge=[(0, 1.12), (0.15, 1.0), (1, 1.0)]), "wood", sharp=60)
        w(grip_wrap(-0.060, 0.062, 0.0152, turns=6.5, width=0.0090, thick=0.0026, oval=1.0, gap=0.5), "hide", sharp=70)
        w(g_box(0.064, 0.052, 0.150, (0, 0, 0.165)), "woodbox", bevel=0.010, bevel_seg=2, sharp=40)
        w(g_box(0.074, 0.060, 0.022, (0, 0, 0.246)), "woodbox", bevel=0.007, sharp=40)
        w(g_lathe([(0.030, 0.257), (0.022, 0.272), (0.026, 0.286), (0.014, 0.300), (0.0, 0.304)], n=8, sy=0.8), "woodbox", sharp=40)
        for sgn in (1, -1):
            for (g_, src, kw) in totem_face(0.170, style="slit"):
                w(gxf(g_, S(1, sgn, 1)), src, **kw)
        w(gjoin(*[gxf(g_tooth(0.040, 0.011, 0.012, 5), T(sx_ * 0.026, 0, 0.240) @ R('Z', 90 - 90 * sx_) @ R('Y', 24)) for sx_ in (1, -1)]), "woodbox", sharp=40)
        ant = gxf(g_antler(0.40, 3, 6), T(0.016, 0, 0.288) @ R('Z', -18))
        w(gjoin(ant, mirror_x_g(ant)), "oldbone", sharp=50)
        w(helix(0.0180, 0.082, 0.100, 3.2, 0.0050, 0.0044, n=6, spt=10), "rope", sharp=80)
        for sx_ in (1, -1):
            f1 = gxf(g_feather(0.115, 0.030, 0.0028), T(sx_ * 0.026, 0.004, 0.090) @ R('Y', 180 + sx_ * 16) @ R('Z', 12 * sx_))
            f2 = gxf(g_feather(0.090, 0.024, 0.0028), T(sx_ * 0.030, -0.006, 0.086) @ R('Y', 180 + sx_ * 30) @ R('Z', -20 * sx_))
            w(gjoin(f1, f2), "feather" if sx_ > 0 else "feather_dark", sharp=40)
            bp = [(sx_ * (0.022 + 0.004 * i), 0.012, 0.086 - 0.017 * i) for i in range(5)]
            w(tube(bp, 0.0012, n=4), "cord_dark", sharp=80)
            w(g_beads(bp[1:], r=0.0062, nu=6, nv=4), "bone", sharp=60)


# =============================================================================================== grimoire 魔典
BOOK_X0 = 0.050


def book_bail(w, t, src="iron", grip_src="grip", z=0.086):
    """carrying bail on the spine: the fist holds its middle (along Z at the origin), the book hangs towards +X"""
    X0 = BOOK_X0
    w(tube([(X0 + 0.004, 0, -z), (0.020, 0, -z + 0.004), (0.0, 0, -0.052), (0.0, 0, 0.052), (0.020, 0, z - 0.004), (X0 + 0.004, 0, z)],
           0.0074, n=8), src, sharp=60)
    w(tube_w([(0, 0, -0.046), (0, 0, 0.046)], 0.0108, 10), grip_src, sharp=60)
    w(grip_wrap(-0.042, 0.042, 0.0108, turns=7.0, width=0.0058, thick=0.0020, oval=1.0), grip_src, sharp=70)
    w(gjoin(g_box(0.016, t + 0.010, 0.032, (X0 + 0.004, 0, -z)), g_box(0.016, t + 0.010, 0.032, (X0 + 0.004, 0, z))), src, bevel=0.003, sharp=40)


def book_body(w, bw, bh, t, cover, pages="paper", ct=0.0075, bevel=0.0035):
    X0 = BOOK_X0
    cv, pg = g_book(bw, bh, t, ct)
    MB = T(X0 + bw / 2, 0, 0)
    w(gxf(cv, MB), cover, bevel=bevel, sharp=40)
    w(gxf(pg, MB), pages, sharp=40)
    return MB


def book_corners(w, bw, bh, t, src, s=0.038):
    X0 = BOOK_X0
    cs = []
    for sy_ in (1, -1):
        yy = sy_ * (t / 2 + 0.0015)
        for (cx, cz, ry) in ((X0 + bw, -bh / 2, -90), (X0 + bw, bh / 2, 180), (X0, -bh / 2, 0), (X0, bh / 2, 90)):
            cs.append(gxf(g_corner(s), T(cx, yy, cz) @ R('Y', ry)))
    w(gjoin(*cs), src, sharp=30)


@base("grimoire", info=OFF, texel=0.0010, sheet=dict(icon_roll=0.0))
def grimoire(w, v):
    if v == 2:
        # iron-bound tome: oxblood cover with raised spine bands, iron corners, two clasped straps, a brass seal on each board
        bw, bh, t = 0.180, 0.250, 0.058
        X0 = BOOK_X0
        book_body(w, bw, bh, t, "tome")
        book_bail(w, t)
        book_corners(w, bw, bh, t, "iron", 0.040)
        w(gjoin(*[g_box(0.010, t + 0.006, 0.012, (X0 + 0.003, 0, z)) for z in (-0.070, -0.024, 0.024, 0.070)]), "tome", bevel=0.003, sharp=40)
        for z in (-0.072, 0.072):
            w(g_box(bw * 0.56, t + 0.007, 0.024, (X0 + bw * 0.74, 0, z)), "strap", bevel=0.002, sharp=40)
            w(g_box(0.016, t + 0.016, 0.034, (X0 + bw + 0.002, 0, z)), "iron", bevel=0.003, sharp=40)
            w(g_box(0.006, 0.020, 0.018, (X0 + bw + 0.011, 0, z)), "brass", bevel=0.002, sharp=40)
        for sy_ in (1, -1):
            yy = sy_ * (t / 2 + 0.0015)
            Mc = T(X0 + bw * 0.5, yy, 0) @ R('X', -90 * sy_)
            w(gxf(g_lathe([(0.032, 0.0), (0.048, 0.0), (0.048, 0.0036), (0.040, 0.0052), (0.032, 0.0036)], n=16), Mc), "brass", sharp=40)
            w(gxf(g_prism(star4(0.030, 0.30), 0.0070), T(X0 + bw * 0.5, yy, 0)), "brass", bevel=0.0010, sharp=30)
            w(gxf(g_gem(0.0085, 0.010, 8, 0.7), T(X0 + bw * 0.5, yy + sy_ * 0.0030, 0) @ R('X', -90 * sy_)), "crystal_red", sharp=20)


# =============================================================================================== lantern 燈
HANG = R('Y', -70)      # lantern space (-Z down) -> hanging towards +X, 20 degrees to the little-finger side


def lantern_yoke(w, src, H, grip_src="grip"):
    """handle bar in the fist (along Z) with two arms meeting at the hang point H"""
    w(tube_w([(0, 0, -0.058), (0, 0, 0.058)], 0.0112, 10), grip_src, sharp=60)
    w(grip_wrap(-0.046, 0.046, 0.0112, turns=7.5, width=0.0060, thick=0.0020, oval=1.0), grip_src, sharp=70)
    w(rings((-0.054, 0.054), 0.0124, h=0.007, n=10, bulge=1.12), src, sharp=50)
    for s in (1, -1):
        w(quillon([(0.0, 0, s * 0.058), (0.012, 0, s * 0.070), (H[0] * 0.55, 0, H[2] + s * 0.044), (H[0] * 0.92, 0, H[2] + s * 0.010), tuple(H)],
                  [0.0056, 0.0056, 0.0050, 0.0046, 0.0044], n=6, flat=1.0), src, sharp=50)


def lantern_body(w, frame_src, r=0.046, h=0.140, n=6, roof=1.0, flame=1.0, M=None, wax="wax", fl="flame", ring=0.012, panes=None,
                 finial=True):
    """caged lantern in lantern space (hang point at the origin, body towards -Z)"""
    M = np.eye(4) if M is None else M
    P = []
    P.append((gxf(torus(ring, ring * 0.24, 12, 5), T(0, 0, -ring) @ R('X', 90)), frame_src, dict(sharp=60)))
    zc = -2 * ring
    rh = 0.052 * roof * (r / 0.045)
    P.append((g_lathe([(0.0, zc + 0.004), (r * 0.16, zc + 0.002), (r * 0.22, zc - rh * 0.22), (r * 0.44, zc - rh * 0.34), (r * 0.50, zc - rh * 0.56),
                       (r * 1.22, zc - rh), (r * 1.22, zc - rh - 0.006), (0.0, zc - rh - 0.006)], n=n), frame_src, dict(sharp=30)))
    zt = zc - rh - 0.006
    zb = zt - h
    posts, hoops = [], []
    for i in range(n):
        a = TAU * (i + 0.5) / n
        x, y = r * math.cos(a), r * math.sin(a)
        posts.append(g_tube([(x, y, zt + 0.002), (x * 1.04, y * 1.04, (zt + zb) / 2), (x, y, zb - 0.002)], 0.0040 * (r / 0.045) ** 0.5, n=4))
    P.append((gjoin(*posts), frame_src, dict(sharp=60)))
    for zz in (zt - h * 0.30, zt - h * 0.70):
        hoops.append(g_tube([(r * 1.03 * math.cos(TAU * (i + 0.5) / n), r * 1.03 * math.sin(TAU * (i + 0.5) / n), zz) for i in range(n)], 0.0030, n=4, closed=True))
    P.append((gjoin(*hoops), frame_src, dict(sharp=60)))
    P.append((g_lathe([(0.0, zb + 0.004), (r * 1.18, zb + 0.004), (r * 1.22, zb - 0.004), (r * 0.84, zb - 0.016), (r * 0.40, zb - 0.022), (0.0, zb - 0.022)], n=n),
              frame_src, dict(sharp=30)))
    if finial:
        P.append((g_lathe([(0.0, zb - 0.020), (r * 0.20, zb - 0.026), (r * 0.10, zb - 0.036), (0.0, zb - 0.046)], n=6), frame_src, dict(sharp=40)))
    if wax:
        P.append((g_lathe([(0.0, zb + 0.004), (r * 0.32, zb + 0.004), (r * 0.30, zb + h * 0.22), (r * 0.12, zb + h * 0.24), (0.0, zb + h * 0.235)], n=8), wax, dict(sharp=50)))
    if flame > 0:
        f0 = zb + h * 0.24
        fr, fh = r * 0.46 * flame, h * 0.56 * flame
        P.append((g_lathe([(0.0, f0 - 0.002), (fr * 0.70, f0 + fh * 0.14), (fr, f0 + fh * 0.34), (fr * 0.60, f0 + fh * 0.68), (0.0, f0 + fh)], n=8), fl, dict(sharp=180)))
    if panes:
        pn = []
        for i in range(n):
            if i % 2:
                continue
            a0, a1 = TAU * (i + 0.5) / n, TAU * (i + 1.5) / n
            p0 = np.array((r * 0.97 * math.cos(a0), r * 0.97 * math.sin(a0)))
            p1 = np.array((r * 0.97 * math.cos(a1), r * 0.97 * math.sin(a1)))
            c = (p0 + p1) / 2
            d = c / np.linalg.norm(c)
            Mx = frame((c[0], c[1], (zt + zb) / 2), (0, 0, 1), (d[0], d[1], 0))
            pn.append(gxf(g_box(float(np.linalg.norm(p1 - p0)) * 0.92, 0.0024, h * 0.96), Mx))
        P.append((gjoin(*pn), panes, dict(sharp=40)))
    for (g_, src, kw) in P:
        w(gxf(g_, M), src, **kw)
    return zb


@base("lantern", info=dict(OFF, hint="hangs towards +X (down when the arm hangs), tilted 20 deg to the little-finger side"), texel=0.0008,
      sheet=dict(pre_rot=(0, 70, 0), icon_roll=0.0))
def lantern(w, v):
    if v == 2:
        # watchman's lantern: six-sided open brass cage, vented roof, candle and flame, leather-bound yoke
        H = np.array((0.094, 0.0, -0.034))
        lantern_yoke(w, "brass", H)
        lantern_body(w, "brass", r=0.046, h=0.138, n=6, M=T(*H) @ HANG)
