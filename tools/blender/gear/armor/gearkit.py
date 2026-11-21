"""gearkit -- parametric garments for the t1..t3 armour looks (thin wrappers round the hero toolkit).

Conventions (see game/assets/gear/armor/REGIONS.md):
  * every part is built inside `with K.slot("<slot>"):` and is tagged with that slot;
  * layer offsets from the skin: L1 under layer / tight sleeves, L2 pants + glove cuffs, L3 boots + chest shells in the
    overlap zones, lofts and plates 20 mm or more;
  * the waist zone (belts, sashes, skirts, tabards) belongs to the chest piece; pants end at or below z = 1.03;
  * left side is modelled (x > 0) and mirrored.
"""
from contextlib import contextmanager
from gearlib import *

L1, L2, L3 = 0.0055, 0.0095, 0.0145
ARM_Y, ARM_Z = 0.068, 1.438


def hang_w(co):
    """weights of something that hangs from the torso past the waist (tabard, apron, long strap)"""
    z, x = co[:, 2], co[:, 0]
    a = sstep(z, 1.20, 1.27)
    b = (1 - a) * sstep(z, 1.07, 1.12)
    cc = (1 - a - b) * sstep(z, 1.00, 1.05)
    rest = np.clip(1 - a - b - cc, 0, 1)
    t = sstep(1.0 - z, 0.0, 0.30) * 0.86
    l = sstep(x, -0.05, 0.05)
    return {"spine_03": a, "spine_02": b, "spine_01": cc, "pelvis": rest * (1 - t), "thigh_l": rest * t * l, "thigh_r": rest * t * (1 - l)}


def solid(o, t, offset=1.0):
    """give a sheet thickness (the game draws characters with back-face culling, so every hanging cloth needs an inside).
    Plain offset, not 'even thickness': that one shoots spikes out of sharply folded or torn sheets."""
    if not t:
        return o
    m = o.modifiers.new("solid", 'SOLIDIFY')
    m.thickness = t
    m.offset = offset
    m.use_even_offset = False
    m.use_quality_normals = True
    am = next(q for q in o.modifiers if q.type == 'ARMATURE')
    while o.modifiers[-1] != am:
        o.modifiers.move(list(o.modifiers).index(am), len(o.modifiers) - 1)
    return o


HOOD_ROUND = [(1.455, 0.025, 0.215, 0.150, 0.175), (1.50, 0.018, 0.160, 0.128, 0.150), (1.55, 0.004, 0.106, 0.116, 0.112),
              (1.61, -0.015, 0.112, 0.134, 0.118), (1.69, -0.024, 0.124, 0.146, 0.136), (1.75, -0.024, 0.116, 0.138, 0.132),
              (1.80, -0.018, 0.094, 0.112, 0.112), (1.84, -0.008, 0.058, 0.070, 0.074), (1.862, 0.0, 0.024, 0.030, 0.032),
              (1.868, 0.0, 0.0, 0.0, 0.0)]
HOOD_POINT = [(1.455, 0.030, 0.165, 0.122, 0.150), (1.52, 0.020, 0.112, 0.098, 0.122), (1.56, 0.004, 0.098, 0.106, 0.108),
              (1.62, -0.012, 0.106, 0.128, 0.124), (1.69, -0.012, 0.115, 0.142, 0.140), (1.75, -0.006, 0.108, 0.136, 0.146),
              (1.80, 0.008, 0.088, 0.112, 0.144), (1.84, 0.034, 0.060, 0.076, 0.122), (1.872, 0.074, 0.030, 0.038, 0.076),
              (1.892, 0.125, 0.0, 0.0, 0.0)]


class Kit:
    def __init__(self, c, cls, look):
        self.c, self.B, self.cls, self.look = c, c.B, cls, look
        self.vr = vert_regions(c.B)
        self.female = c.kind == "female"
        self.seed = CLASSES.index(cls) * 11 + int(look[1]) * 5
        self.opts = {}              # slot -> keywords for gearpiece.hides_and_carry (ray, force_hide, never_hide)
        self.decimate = {}          # slot -> ratio
        B = self.B
        self.L = B.x > 0
        # forearm / calf thickness of this body (bulked heroes need wider rings)
        fa = (B.w("lowerarm") > 0.5) & self.L & (np.abs(B.x - 0.55) < 0.03)
        self.forearm_r = float(np.sqrt((B.y[fa] - ARM_Y) ** 2 + (B.z[fa] - ARM_Z) ** 2).max()) if fa.any() else 0.045
        ua = (B.w("upperarm") > 0.5) & self.L & (np.abs(B.x - 0.34) < 0.03)
        self.upperarm_r = float(np.sqrt((B.y[ua] - ARM_Y) ** 2 + (B.z[ua] - ARM_Z) ** 2).max()) if ua.any() else 0.055
        log("kit %s %s: female=%s forearm r %.3f upper arm r %.3f" % (cls, look, self.female, self.forearm_r, self.upperarm_r))

    # ------------------------------------------------------------------ bookkeeping
    @contextmanager
    def slot(self, name):
        assert name in SLOTS, name
        n0 = len(mk.PARTS)
        seen = set(mk.PARTS)
        yield self
        for o in mk.PARTS:
            if o not in seen:
                o["slot"] = name

    def rs(self):
        self.seed += 1
        return self.seed

    def reg(self, *names):
        return np.isin(self.vr, [SKIN_REGIONS.index(n) for n in names])

    # ------------------------------------------------------------------ measurements
    def ring(self, z, gap=0.0):
        for dz in (0.02, 0.035, 0.06):
            s = self.B.slice(z, dz)
            if s is not None:
                return (0.0, s[1], s[2] + gap, s[3] + gap, s[4] + gap)
        raise RuntimeError("no body slice at z=%.2f" % z)

    def stations(self, zs, gaps, plumb=False):
        """[(z, 0, cy, rx, ryf, ryb)] round the body at the heights zs (any order), grown by gaps (scalar or per z).
        plumb: going down from the widest station nothing gets narrower (cloth hangs, it does not cling to the legs)"""
        zs = list(zs)
        gaps = np.broadcast_to(np.asarray(gaps, float), (len(zs),))
        st = []
        for z, g in sorted(zip(zs, gaps), key=lambda t: -t[0]):
            r = self.ring(z, g)
            st.append([z, 0.0, r[1], r[2], r[3], r[4]])
        if plumb:
            for i in range(1, len(st)):
                if st[i][0] < 1.0:
                    for k in (3, 4, 5):
                        st[i][k] = max(st[i][k], st[i - 1][k])
                    st[i][2] = st[i - 1][2] * 0.6 + st[i][2] * 0.4
        return [tuple(s) for s in sorted(st)]

    # ------------------------------------------------------------------ shells (deform exactly like the skin)
    def shell(self, name, mask, src, cls="cloth", off=0.012, smooth=2, hem=None, chew=0, chew_p=0.4, keep=None, wrinkle=0.0,
              waxis="z", wfreq=26.0, wmask=None, **kw):
        """body shell. hem = (z_lo, z_hi, min offset): the offset rises to at least `min offset` below z_hi.
        chew = rounds of ragged-edge erosion, wrinkle = amplitude (m) of compression folds across `waxis`."""
        if hem:
            zlo, zhi, mo = hem
            base = off
            offv = lambda co: base + max(mo - base, 0.0) * (1 - sstep(co[:, 2], zlo, zhi))
        else:
            offv = off
        o = shell(self.B, mask, offv, name, cls, src, smooth=smooth, sharp=180, **kw)
        if chew:
            chew_edge(o, rounds=int(chew), p=chew_p, seed=self.rs(), keep=keep)
        if wrinkle:
            wrinkle_shell(o, wrinkle, wfreq, waxis, self.rs(), mask=wmask)
        return o

    def torso_mask(self, lo=1.0, hi=1.50, sleeve=0.215, vneck=0.0, vwidth=0.075, one_shoulder=None, back_lo=None):
        """torso between lo and hi and the arms out to |x| < sleeve. vneck = depth of a V opening at the front (m);
        one_shoulder = 'l' / 'r': a diagonal cut that leaves the other shoulder bare."""
        B = self.B
        m = m_torso(B, lo, hi, sleeve)
        if back_lo is not None:
            m &= ~((B.y > 0.02) & (B.z < back_lo) & (np.abs(B.x) < 0.20))
        if vneck:
            z0 = hi - vneck
            w = vwidth * np.clip((B.z - z0) / max(vneck, 1e-3), 0, 1.3)
            m &= ~((B.y < 0.01) & (B.z > z0) & (np.abs(B.x) < w))
        if one_shoulder:
            s = 1.0 if one_shoulder == "l" else -1.0
            cut = 1.50 - 0.95 * (0.19 + s * B.x)          # line from the kept shoulder down under the other arm
            m &= ~((B.z > cut) & (s * B.x < 0.06)) & ~((s * B.x < -0.17) & (B.z > 1.30))
        return m

    def tunic(self, name, src, lo=1.0, hi=1.50, sleeve=0.215, off=0.012, cls="cloth", vneck=0.0, vwidth=0.075, one_shoulder=None,
              back_lo=None, **kw):
        m = self.torso_mask(lo, hi, sleeve, vneck, vwidth, one_shoulder=one_shoulder, back_lo=back_lo)
        hem = (lo, lo + 0.07, 0.0165) if lo < 1.06 else None
        kw.setdefault("smooth", 3)
        return self.shell(name, m, src, cls, off, hem=hem, **kw)

    def sleeves(self, name, src, x0=0.20, x1=0.70, off=L1, cls="cloth", sides="lr", **kw):
        """tight sleeves (or arm wraps / bracers when x0 is past the elbow)"""
        B = self.B
        m = (B.w("upperarm", "lowerarm", "clavicle") > 0.5) & (np.abs(B.x) > x0) & (np.abs(B.x) < x1) & (B.w(*BONES_HAND) < 0.35)
        if sides == "l":
            m &= B.x > 0
        elif sides == "r":
            m &= B.x < 0
        kw.setdefault("smooth", 2)
        return self.shell(name, m, src, cls, off, **kw)

    def gloves(self, name, src, cuff=0.70, off=0.0045, cls="leather", fingerless=False, sides="lr", **kw):
        B = self.B
        m = m_hands(B, cuff)
        if fingerless:
            tips = B.w("index_02", "index_03", "index_04", "middle_02", "middle_03", "middle_04", "ring_02", "ring_03", "ring_04",
                       "pinky_02", "pinky_03", "pinky_04", "thumb_03", "thumb_04") > 0.5
            m &= ~tips
        if sides == "l":
            m &= B.x > 0
        elif sides == "r":
            m &= B.x < 0
        kw.setdefault("smooth", 0 if not fingerless else 1)
        return self.shell(name, m, src, cls, off, **kw)

    def palm_wrap(self, name, src, off=0.004, cls="cloth", **kw):
        """bandage round the palm and the back of the hand (fingers and thumb free)"""
        B = self.B
        m = (B.w("hand") > 0.55) & (np.abs(B.x) > 0.745)
        kw.setdefault("smooth", 1)
        return self.shell(name, m, src, cls, off, **kw)

    def pants(self, name, src, top=1.02, bottom=0.10, off=L2, cls="cloth", baggy=0.0, **kw):
        """leg shell from `bottom` up to `top` (at most 1.03: the chest hem goes over it). baggy = extra room (m) round
        thigh and knee."""
        B = self.B
        top = min(top, 1.03)
        m = m_legs(B, bottom, top)
        if baggy:
            base = off
            b0 = bottom
            offv = lambda co: base + baggy * sstep(co[:, 2], b0 + 0.02, b0 + 0.16) * (1 - sstep(co[:, 2], 0.86, 0.99))
            o = shell(B, m, offv, name, cls, src, smooth=kw.pop("smooth", 4), sharp=180)
            if kw.get("chew"):
                chew_edge(o, rounds=int(kw["chew"]), p=0.4, seed=self.rs())
            if kw.get("wrinkle"):
                wrinkle_shell(o, kw["wrinkle"], kw.get("wfreq", 22.0), "z", self.rs())
            return o
        kw.setdefault("smooth", 3)
        return self.shell(name, m, src, cls, off, **kw)

    def boots(self, name, src, top=0.30, off=L3, cls="leather", cuff=0.0, toe=True, toe_h=0.050, toe_grow=0.010, **kw):
        o = boots(self.c, src, top=top, offset=off, cls=cls, name=name, smooth=kw.pop("smooth", 3), cuff=cuff, toe_cap=False)
        if toe:
            toe_box(o, height=toe_h, length=0.108, grow=toe_grow)
        if kw.get("chew"):
            chew_edge(o, rounds=int(kw["chew"]), p=0.4, seed=self.rs(), keep=lambda co: co.z < 0.12)
        if kw.get("wrinkle"):
            wrinkle_shell(o, kw["wrinkle"], kw.get("wfreq", 30.0), "z", self.rs(), mask=lambda co: sstep(co[:, 2], 0.10, 0.16))
        return o

    def leg_band(self, name, src, z0, z1, off=L2 + 0.003, cls="leather", **kw):
        """shell band round both legs between two heights (garter, knee band, shin wrap, thigh strap)"""
        B = self.B
        m = (B.w(*BONES_LEG, *BONES_FOOT) > 0.5) & (B.z > z0) & (B.z < z1)
        kw.setdefault("smooth", 2)
        return self.shell(name, m, src, cls, off, **kw)

    def capelet(self, name, src, zmin=1.36, arm_x=0.36, off=0.027, cls="cloth", thick=0.006, chew=0, slope=0.55, back_drop=0.0, **kw):
        """short mantle lying on the shoulders (shell, so it follows the arms)"""
        B = self.B
        arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < arm_x) & (B.z > 1.40)
        zlim = zmin - slope * B.y * (1 if not back_drop else 1) - back_drop * sstep(B.y, 0.0, 0.10)
        core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zlim) & (B.z < 1.545) & (np.abs(B.x) < 0.20)
        mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
        o = shell(B, mm, off, name, cls, src, smooth=4, sharp=180, solidify=thick, **kw)
        if chew:
            chew_edge(o, rounds=int(chew), p=0.36, seed=self.rs(), keep=lambda co: co.z > 1.47 and abs(co.x) < 0.13)
        return o

    # ------------------------------------------------------------------ lofts (hang from the body)
    def skirt(self, name, src, z_top=1.06, z_bot=0.55, gap_top=0.020, gap_bot=0.045, cls="cloth", nu=30, nv=10, thick=0.005,
              folds=((7, 0.028, 0.15), (13, 0.010, 0.6)), tails=0, tail_depth=0.16, ragged=0.0, u0=0.0, u1=1.0, close=True,
              calf=0.5, **kw):
        """skirt / tunic tail / robe bottom. u runs 0 = left side, .25 = back, .5 = right, .75 = front."""
        n = max(4, int(round((z_top - z_bot) / 0.14)) + 2)
        zs = np.linspace(z_bot, z_top, n)
        gaps = gap_bot + (gap_top - gap_bot) * (zs - z_bot) / (z_top - z_bot)
        st = self.stations(zs, gaps, plumb=True)
        cy = st[-1][2]
        if tails:
            mod = tails_mod(k=tails, depth=tail_depth, seed=self.rs(), extra=fold_mod(list(folds), cy=cy) if folds else None)
        else:
            mod = fold_mod(list(folds), cy=cy) if folds else None
        o = robe(self.c, src, st, nu=nu, nv=nv, name=name, cls=cls, thick=0.0, ragged=ragged, u0=u0, u1=u1, close=close,
                 calf_to_thigh=calf, mod=mod, seed=self.rs(), **kw)
        return solid(o, thick)

    def panel(self, name, src, x0, x1, z_top, z_bot, front=True, gap=0.022, cls="cloth", flare=0.12, point=0.0, nu=9, nv=10,
              thick=0.005, folds=((2.0, 0.006, 0.2),), zmod=None):
        """hanging cloth panel (tabard half, apron, loin flap, stole) in front of / behind the body"""
        n = max(3, int(round((z_top - z_bot) / 0.12)) + 2)
        zs = np.linspace(z_bot, z_top, n)
        st = self.stations(zs, gap, plumb=True)
        yfn = front_y(st, 0.0) if front else back_y(st, 0.0)
        o = drape(name, x0, x1, z_top, z_bot, yfn, cls, src, nu=nu, nv=nv, thick=thick, out=-1.0 if front else 1.0, folds=folds,
                  flare=flare, point=point, zmod=zmod)
        w_func(o, hang_w)
        return o

    def cloak(self, name, src, z_top=1.46, z_bot=0.60, width=0.22, arc=0.44, cls="cloth", ragged=0.0, **kw):
        ck = cloak(self.c, src, z_top=z_top, z_bot=z_bot, width=width, depth_top=0.165, depth_bot=0.24, arc=arc, cls=cls, name=name,
                   ragged=ragged, seed=self.rs(), nu=22, nv=12, **kw)

        def legs(W, col, co):
            z, x = co[:, 2], co[:, 0]
            t = sstep(0.95 - z, 0.0, 0.3) * 0.35
            l = sstep(x, -0.06, 0.06)
            W = W * (1 - t)[:, None]
            W[:, col["thigh_l"]] += t * l
            W[:, col["thigh_r"]] += t * (1 - l)
            return W
        w_mix(ck, legs)
        return ck

    # ------------------------------------------------------------------ plates
    def cuirass(self, name, src, z0=1.19, z1=1.515, gap=0.024, cls="metal", ridge=0.011, thick=0.006, bone="spine_03", nu=28, nv=9):
        zs = [z for z in (1.06, 1.13, 1.19, 1.25, 1.32, 1.39, 1.45, 1.495, 1.52, 1.55) if z0 - 1e-6 <= z <= z1 + 1e-6]
        if zs[0] > z0 + 0.01:
            zs = [z0] + zs
        st = self.stations(zs, gap)

        def mod(U, V, x, y, z):
            return x, y - ridge * np.exp(-((U - 0.75) / 0.035) ** 2), z
        return add(name, g_stations(st, nu=nu, nv=nv, p=2.3, mod=mod if ridge else None), cls, src, solidify=thick, bone=bone, sharp=50,
                   bevel=0.002)

    def band_plate(self, name, src, z0, z1, gap0=0.03, gap1=0.024, bone="spine_01", cls="metal", thick=0.006, nu=28, u0=0.0, u1=1.0):
        """one lame round the body between z0 and z1 (fauld, plackart, wide belt plate); u0..u1 limits it to an arc"""
        st = self.stations([z0, z1], [gap0, gap1])
        g = g_stations(st, nu=nu, nv=3, p=2.3, close=(u1 - u0) > 0.999, u0=u0, u1=u1)
        return add(name, g, cls, src, solidify=thick, bone=bone, sharp=50, bevel=0.002)

    def pauldron(self, name, src, size=1.0, trim=None, cls="metal", tilt=18.0, sides="lr", lift=0.0, rows=None, th_out=98.0,
                 th_in=50.0, thick=0.006, wts=(0.58, 0.42)):
        SH = np.array((0.212, 0.066, 1.474 + lift))
        k = size * max(1.0, self.upperarm_r / 0.060) ** 0.6
        gp = g_pauldron(0.122 * k, 0.118 * k, 0.070 * k, th_out=th_out, th_in=th_in, th_fb=84.0, nu=16, rows=rows or PAULDRON_ROWS)
        Mp = T(*SH) @ R('Y', tilt)
        out = []
        p1 = add(name + "_l", gxf(gp, Mp), cls, src, solidify=thick, sharp=40, bevel=0.002 if cls == "metal" else 0.0)
        weld(p1)
        out.append(p1)
        if trim:
            out.append(add(name + "_trim_l", g_tube(xf(rim_of(gp, 16), Mp), 0.0065, n=5, closed=True), "metal", trim, sharp=60))
        res = []
        for o in out:
            w_func(o, lambda co: {"clavicle_l": wts[0], "upperarm_l": wts[1]})
            if sides in ("lr", "r"):
                r = mirrored(o)
                res.append(r)
            if sides == "r":
                remove_part(o)
            else:
                res.append(o)
        return res

    def arm_lames(self, name, src, xs=((0.272, 0.368), (0.346, 0.432)), grow=0.018, cls="metal", sides="lr"):
        """overlapping lames over the upper arm (rigid to it)"""
        out = []
        for i, (x0, x1) in enumerate(xs):
            r0 = self.upperarm_r + grow - 0.004 * i
            o = add("%s%d_l" % (name, i), arm_lame(x0, x1, r0, r0 - 0.007, arc=0.72, n=11), cls, src, solidify=0.005, bone="upperarm_l",
                    sharp=50, bevel=0.002)
            out.append(o)
            if sides == "lr":
                out.append(mirrored(o))
        return out

    def couter(self, name, src, cls="metal", wing=None, sides="lr"):
        r = self.forearm_r + 0.012
        o = add(name + "_l", gxf(g_sphere(1.0, 10, 6), T(0.470, 0.080, ARM_Z - 0.002) @ S(r, r * 1.15, r * 1.08)), cls, src, sharp=60)
        w_func(o, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
        out = [o]
        if wing:
            w = add(name + "_wing_l", ydome([(0.0, 0.012), (0.020, 0.010), (0.036, 0.0), (0.036, -0.006), (0.0, -0.006)], n=8, x0=0.470,
                                            y0=0.080 + r * 1.12, z0=ARM_Z - 0.002), "metal", wing, sharp=50)
            w_func(w, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
            out.append(w)
        if sides == "lr":
            out += [mirrored(q) for q in list(out)]
        return out

    def vambrace(self, name, src, x0=0.505, x1=0.700, off=0.016, cls="metal", sides="lr", top_only=False):
        """rigid forearm plate (shell of the forearm, rigid to lowerarm)"""
        B = self.B
        m = self.L & (B.w("lowerarm") > 0.5) & (B.x > x0) & (B.x < x1)
        if top_only:
            m &= (B.z > ARM_Z - 0.012) | (B.y > ARM_Y + 0.01)
        o = shell(B, m, off, name + "_l", cls, src, smooth=4, sharp=60)
        w_rigid(o, "lowerarm_l")
        return [o, mirrored(o)] if sides == "lr" else [o]

    def cuff(self, name, src, x=0.640, length=0.078, flare=0.018, cls="metal", trim=None, sides="lr"):
        """flared gauntlet cuff (rigid to the forearm)"""
        r = self.forearm_r
        out = []
        o = add(name + "_l", gxf(g_lathe([(r + flare, 0.0), (r + flare * 0.45, length * 0.5), (r + 0.004, length)], n=12),
                                 frame((x, ARM_Y, ARM_Z - 0.001), (1, 0, 0))), cls, src, solidify=0.005, bone="lowerarm_l", sharp=50, bevel=0.0015)
        out.append(o)
        if trim:
            t = add(name + "_trim_l", gxf(g_lathe([(r + flare + 0.003, -0.004), (r + flare + 0.006, 0.004), (r + flare + 0.002, 0.012)], n=12),
                                          frame((x, ARM_Y, ARM_Z - 0.001), (1, 0, 0))), "metal", trim, bone="lowerarm_l", sharp=60)
            out.append(t)
        if sides == "lr":
            out += [mirrored(q) for q in list(out)]
        return out

    def arm_rings(self, name, src, xs=(0.53, 0.60, 0.67), width=0.022, grow=0.014, cls="metal", sides="lr", bone="lowerarm"):
        """bands round the arm (bracer bands, shackles, arm rings); rigid"""
        gs = []
        for x in xs:
            r = (self.forearm_r if x > 0.47 else self.upperarm_r) + grow
            gs.append(gxf(g_lathe([(r, 0.0), (r + 0.003, width * 0.5), (r, width)], n=12), frame((x, ARM_Y, ARM_Z - 0.001), (1, 0, 0))))
        o = add(name + "_l", gjoin(*gs), cls, src, solidify=0.004, bone=bone + "_l", sharp=50)
        return [o, mirrored(o)] if sides == "lr" else [o]

    def kneecop(self, name, src, cls="metal", wing=None, size=1.0):
        o = add(name + "_l", gxf(g_sphere(1.0, 10, 6), T(0.085, -0.021, 0.536) @ S(0.062 * size, 0.056 * size, 0.072 * size)), cls, src, sharp=60)
        w_func(o, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
        out = [o, mirrored(o)]
        if wing:
            w = add(name + "_wing_l", gxf(g_prism([(0.0, -0.045), (0.050, -0.028), (0.058, 0.018), (0.0, 0.046)], 0.007),
                                          T(0.147 * size, -0.012, 0.536) @ R('Z', 90)), "metal", wing, bevel=0.002, sharp=40)
            w_func(w, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
            out += [w, mirrored(w)]
        return out

    def leg_plate(self, name, src, bone="thigh", z0=0.60, z1=0.86, off=0.017, cls="metal", front_only=True, y_max=0.035):
        """rigid plate on the thigh (cuisse) or shin (greave)"""
        B = self.B
        m = self.L & (B.w(bone) > 0.5) & (B.z > z0) & (B.z < z1)
        if front_only:
            m &= B.y < y_max
        o = shell(B, m, off, name + "_l", cls, src, smooth=4, sharp=60)
        w_rigid(o, bone + "_l")
        return [o, mirrored(o)]

    # ------------------------------------------------------------------ belts, straps, trims
    def belt(self, name, src, z=1.05, width=0.05, gap=0.022, cls="leather", buckle=None, thick=0.009, tilt=0.0):
        r = self.ring(z, gap)
        return belt(self.c, z, src, width=width, thick=thick, cls=cls, name=name, buckle=buckle, tilt=tilt, rx=r[2], ry=max(r[3], r[4]))

    def rope(self, name, src, z=1.04, gap=0.022, r=0.008, cls="leather", turns=1, knot=True, tilt=0.0):
        """rope / cord belt (round), optionally doubled, with a hanging knot at the front left"""
        out = []
        for t in range(turns):
            rg = self.ring(z + t * r * 1.9, gap)
            pts = [(rg[2] * math.cos(a), rg[1] + (rg[4] if math.sin(a) > 0 else rg[3]) * math.sin(a), z + t * r * 1.9 + tilt * rg[2] * math.cos(a))
                   for a in np.linspace(0, TAU, 28, endpoint=False)]
            o = add("%s_%d" % (name, t), g_tube(pts, r, n=6, closed=True), cls, src, sharp=180)
            w_transfer(o, self.c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
            out.append(o)
        if knot:
            rg = self.ring(z, gap)
            kp = np.array((rg[2] * 0.45, rg[1] - rg[3] * 0.92, z))
            k = add(name + "_knot", gjoin(gxf(g_sphere(1.0, 7, 5), T(*kp) @ S(r * 2.0, r * 1.7, r * 1.9)),
                                          g_tube([kp, kp + np.array((0.012, -0.010, -0.07)), kp + np.array((0.018, -0.006, -0.15))], [r, r * 0.9, r * 1.2], n=5),
                                          g_tube([kp, kp + np.array((-0.014, -0.008, -0.06)), kp + np.array((-0.02, -0.004, -0.11))], [r, r * 0.9, r * 1.2], n=5)),
                    cls, src, sharp=180)
            w_func(k, lambda co: {"spine_01": 0.5, "pelvis": 0.5})
            out.append(k)
        return out

    def sash(self, name, src, z=1.06, width=0.10, gap=0.020, tails=(0.34, 0.26), side=1, cls="cloth"):
        """cloth sash wound round the waist with a knot and hanging ends on one hip"""
        r0, r1 = self.ring(z - width / 2, gap), self.ring(z + width / 2, gap)
        st = [(z - width / 2, 0, r0[1], r0[2], r0[3], r0[4]), (z + width / 2, 0, r1[1], r1[2], r1[3], r1[4])]
        o = add(name, g_stations(st, nu=28, nv=4, p=2.3), cls, src, solidify=0.007, sharp=180, double_sided=True)
        w_transfer(o, self.c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
        out = [o]
        kp = np.array((side * r0[2] * 0.86, r0[1] - r0[3] * 0.50, z))
        k = add(name + "_knot", gxf(g_sphere(1.0, 8, 5), T(*kp) @ S(0.032, 0.030, 0.040)), cls, src, sharp=180)
        w_func(k, lambda co: {"pelvis": 0.5, "spine_01": 0.5})
        out.append(k)
        for i, ln in enumerate(tails):
            d = np.array((side * (0.012 + 0.02 * i), -0.012 + 0.02 * i, -1.0))
            path = [kp + d * ln * t + np.array((side * 0.01 * math.sin(3 * t), 0, 0)) for t in (0.0, 0.3, 0.65, 1.0)]
            tl = add("%s_tail_%d" % (name, i), g_tube(path, [0.8, 1.0, 1.15, 1.3], n=6, sx=0.006, sy=0.034, up=(side, 0, 0)), cls, src, sharp=180)
            w_func(tl, lambda co: dict(zip(("pelvis", "thigh_l" if side > 0 else "thigh_r"),
                                           (1 - 0.6 * sstep(1.0 - co[:, 2], 0.0, 0.3), 0.6 * sstep(1.0 - co[:, 2], 0.0, 0.3)))))
            out.append(tl)
        return out

    def strap(self, name, src, pts, width=0.035, thick=0.007, off=0.020, cls="leather", closed=True, only=None, host=None):
        o, Q, N = ribbon(self.c, pts, src, width=width, thick=thick, offset=off, name=name, cls=cls, closed=closed, only=only, host=host)
        return o

    def bandolier(self, name, src, width=0.04, off=0.022, cls="leather", flip=False):
        """strap from one shoulder to the opposite hip, round the back"""
        s = -1.0 if flip else 1.0
        pts = [(s * 0.135, -0.10, 1.455), (s * 0.03, -0.15, 1.30), (-s * 0.14, -0.11, 1.12), (-s * 0.19, 0.03, 1.07),
               (-s * 0.10, 0.15, 1.16), (s * 0.05, 0.16, 1.34), (s * 0.14, 0.09, 1.46)]
        return self.strap(name, src, pts, width=width, off=off, cls=cls, only=["spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"])

    def limb_straps(self, name, src, bone, at, width=0.022, off=0.016, cls="leather", sides="lr", thick=0.005):
        """strap rings round a limb at the given stations (x along the arm for bone = upperarm / lowerarm, height z for
        thigh / calf); flat ribbons that follow the limb's skin weights"""
        arm = bone in ("upperarm", "lowerarm")
        out = []
        for side in ((1, -1) if sides == "lr" else ((1,) if sides == "l" else (-1,))):
            sfx = "l" if side > 0 else "r"
            for i, a in enumerate(at):
                path = self.arm_ring_path(a, 0.03, 12, side) if arm else self.leg_ring_path(a, 0.03, 14, side)
                only = ["upperarm_" + sfx, "lowerarm_" + sfx] if arm else ["thigh_" + sfx, "calf_" + sfx]
                o, Q, N = ribbon(self.c, path, src, width=width, thick=thick, offset=off, name="%s_%d_%s" % (name, i, sfx), cls=cls,
                                 closed=True, step=0.018, only=only, smooth=1)
                out.append(o)
        return out

    def pouch(self, name, src, side=1, z=0.985, size=1.0, cls="leather", flap=None, y=-0.06):
        r = self.ring(z, 0.03)
        p = np.array((side * r[2] * 0.82, r[1] + y, z))
        g = gxf(g_box(0.085 * size, 0.045 * size, 0.095 * size), T(*p) @ R('Z', side * 28))
        o = add(name, g, cls, src, bevel=0.008, sharp=40, bone="pelvis")
        if flap:
            f = add(name + "_flap", gxf(g_box(0.09 * size, 0.05 * size, 0.035 * size), T(p[0], p[1], p[2] + 0.034 * size) @ R('Z', side * 28)), cls, flap,
                    bevel=0.006, sharp=40, bone="pelvis")
        return o

    def rivets(self, name, src, pts, r=0.006, bone=None, host=None, cls="metal"):
        """dome rivets at the given rest-space points, standing on the nearest surface of `host`"""
        h = host or self.c.ref
        if any(m.type != 'ARMATURE' for m in h.modifiers):
            apply_mods(h)
        gs = []
        for p in pts:
            ok, loc, nrm, fi = h.closest_point_on_mesh(Vector(p))
            gs.append(gxf(g_lathe([(0.0, r * 0.75), (r * 0.7, r * 0.5), (r, 0.0)], n=6), frame(np.array(loc) - np.array(nrm) * 0.0005, np.array(nrm))))
        o = add(name, gjoin(*gs), cls, src, sharp=60)
        if bone:
            w_rigid(o, bone)
        else:
            w_transfer(o, h, smooth=0)
        return o

    def trim(self, name, src, path, r=0.006, closed=True, bone=None, host=None, cls="metal", n=5):
        o = add(name, g_tube(path, r, n=n, closed=closed), cls, src, sharp=60)
        if bone:
            w_rigid(o, bone)
        else:
            w_transfer(o, host or self.c.ref, smooth=1)
        return o


    # ------------------------------------------------------------------ sleeves / fur / rings (lofted, rigid or transferred)
    def bell_sleeves(self, name, src, x0=0.165, x1=0.60, cuff=0.105, drop=0.030, cls="cloth", thick=0.006, slim=0.0):
        """lofted sleeves (both arms) that widen to an open cuff at x1; slim = how tight they are at the elbow (m)"""
        r = self.upperarm_r + 0.020
        xm1, xm2 = x0 + (x1 - x0) * 0.33, x0 + (x1 - x0) * 0.68
        st = [(x0, 0.0, 0.0, r + 0.004, r - 0.002, r - 0.002), (xm1, 0.004, 0.0, r - 0.004 - slim, r - 0.008 - slim, r - 0.008 - slim),
              (xm2, drop * 0.4, 0.0, (r + cuff) * 0.5 - slim, (r + cuff * 0.78) * 0.5 - slim, (r + cuff * 0.78) * 0.5 - slim),
              (x1, drop, 0.0, cuff, cuff * 0.78, cuff * 0.78)]
        return sleeve(self.c, src, st, n=14, nv=8, name=name + "_l", cls=cls, thick=thick)

    def arm_ring_path(self, x, grow=0.012, n=14, side=1):
        r = (self.forearm_r if x > 0.47 else self.upperarm_r) + grow
        return [(side * x, ARM_Y + r * math.cos(a), ARM_Z + r * math.sin(a)) for a in np.linspace(0, TAU, n, endpoint=False)]

    def leg_ring_path(self, z, grow=0.014, n=16, side=1):
        """closed path round one leg at height z (measured on the body), `grow` outside the skin"""
        B = self.B
        m = (B.x * side > 0) & (np.abs(B.z - z) < 0.02) & (B.w(*BONES_LEG, *BONES_FOOT) > 0.5)
        cx, cy = float(B.x[m].mean()), float(B.y[m].mean())
        rx = float(np.abs(B.x[m] - cx).max()) + grow
        ry = float(np.abs(B.y[m] - cy).max()) + grow
        return [(cx + rx * math.cos(a), cy + ry * math.sin(a), z) for a in np.linspace(0, TAU, n, endpoint=False)]

    def fur_collar(self, name, src, radius=0.050, z=1.545, wide=0.190, cls="cloth", bone="spine_03"):
        """rolled fur collar lying on the shoulders round the neck"""
        path, rad = [], []
        for i in range(20):
            a = TAU * i / 20
            s_, c_ = math.sin(a), math.cos(a)
            zz = z + (0.012 * s_ if s_ > 0 else 0.085 * s_)
            path.append((wide * c_, 0.03 + (0.155 * s_ if s_ > 0 else 0.150 * s_), zz))
            rad.append(radius if s_ > -0.2 else radius * 0.72)
        return fur_roll(path, rad, src, name=name, cls=cls, bone=bone, noise=0.016, n=10)

    def fur_cuffs(self, name, src, where="boots", at=0.40, radius=0.022, cls="cloth"):
        """fur trim rings: round the boot tops (where='boots', at = height) or the wrists (where='arms', at = x)"""
        out = []
        for side in (1, -1):
            if where == "boots":
                path = self.leg_ring_path(at, 0.016, 14, side)
                bone = "calf_l" if side > 0 else "calf_r"
            else:
                path = self.arm_ring_path(at, 0.012, 12, side)
                bone = ("lowerarm_l" if at > 0.47 else "upperarm_l") if side > 0 else ("lowerarm_r" if at > 0.47 else "upperarm_r")
            out.append(fur_roll(path, radius, src, name="%s_%s" % (name, "l" if side > 0 else "r"), cls=cls, bone=bone, noise=0.010, n=8))
        return out

    def head_ornament(self, name, g, src, cls="bone", at=(0.0, -0.02, 1.80), rot=None):
        """any geometry placed in head space and rigid to the head (horn, antler, feather, crest, skull)"""
        M = T(*at) @ (rot if rot is not None else np.eye(4))
        return add(name, gxf(g, M), cls, src, bone="Head", sharp=60)

    # ------------------------------------------------------------------ head
    def hood(self, name, src, style="round", cls="cloth", thick=0.008, wide=1.0, peak=0.03, open_deg=60.0, chin=1.575, brow=1.748, collar=1.0):
        st = [list(s) for s in (HOOD_POINT if style == "point" else HOOD_ROUND)]
        for s in st:
            s[2] *= wide
            if s[0] < 1.53:
                s[2] *= collar
                s[3] *= collar
                s[4] *= collar
        hd, gh = cowl(self.c, src, [tuple(s) for s in st], name=name, cls=cls, thick=thick, chin=chin, brow=brow, open_deg=open_deg, peak=peak,
                      nu=28, nv=18)
        return hd, gh

    def cap(self, name, src, z_brow=1.715, ears=True, off=0.008, cls="leather", nape=1.60, **kw):
        """close cap / coif of cloth or leather: shell of the scalp (and ears / nape), rigid enough with the head weights"""
        B = self.B
        h = B.w("Head") > 0.5
        m = h & ((B.z > z_brow) | ((B.y > 0.005) & (B.z > nape)))
        if ears:
            m |= h & (np.abs(B.x) > 0.058) & (B.z > 1.62) & (B.y > -0.045)
        kw.setdefault("smooth", 3)
        return self.shell(name, m, src, cls, off, **kw)

    def coif(self, name, src, off=0.007, cls="metal", z_lo=1.50, face_w=0.064, **kw):
        """mail / cloth coif: head and neck except the face"""
        B = self.B
        face = m_head(B) & (B.y < -0.040) & (B.z > 1.585) & (B.z < 1.735) & (np.abs(B.x) < face_w)
        m = (B.w("Head", "neck_01") > 0.5) & ~face & (B.z > z_lo)
        kw.setdefault("smooth", 2)
        return self.shell(name, m, src, cls, off, **kw)

    def face_mask(self, name, src, z_top=1.648, z_bot=1.545, off=0.007, cls="cloth", **kw):
        B = self.B
        m = (B.w("Head", "neck_01") > 0.5) & (B.z < z_top) & (B.z > z_bot) & (B.y < -0.005)
        kw.setdefault("smooth", 2)
        return self.shell(name, m, src, cls, off, **kw)

    def helm(self, name, src, style="barbute", cls="metal", thick=0.005, trim=None, grow=1.0):
        """steel skull, rigid to the head. style: barbute (Y slot) | sallet (open face, long tail) | skull (cap that ends
        above the ears) | open (bascinet: face open, cheeks covered)"""
        kw = dict(barbute=dict(eye_deg=42.0), sallet=dict(eye_deg=78.0, chin_deg=78.0, z_rim=1.615, tail=0.06, z_eye0=1.62, z_eye1=1.64),
                  skull=dict(eye_deg=80.0, chin_deg=80.0, z_rim=1.690, tail=0.03, z_eye0=1.69, z_eye1=1.70, brow=1.722),
                  open=dict(eye_deg=64.0, chin_deg=58.0, z_rim=1.56, tail=0.03, z_eye0=1.60, z_eye1=1.66))[style]
        gh = g_barbute(rx=0.108 * grow, ryf=0.127 * grow, ryb=0.124 * grow, **kw)
        o = add(name, gh, cls, src, bone="Head", sharp=50, solidify=thick)
        weld(o)
        out = [o]
        if trim:
            rows = grid_rows(gh, 30)
            rim = [rows[0, i] for i in range(0, 30)]
            out.append(add(name + "_rim", g_tube(rim, 0.006, n=5, closed=False), "metal", trim, bone="Head", sharp=60))
        return out, gh

    def kettle(self, name, src, brim=0.185, crown=0.112, height=0.125, z=1.742, cls="metal", droop=0.022, dent=0.0, sy=1.10):
        """kettle hat / wide iron hat: dome with a brim, rigid to the head"""
        prof = [(brim, -droop), (brim - 0.004, -droop + 0.006), (crown * 1.10, 0.004), (crown, 0.016), (crown * 0.96, height * 0.45),
                (crown * 0.80, height * 0.78), (crown * 0.48, height * 0.95), (0.0, height)]
        g = g_lathe(prof, n=20, sx=0.98, sy=sy)
        if dent:
            v = g["v"].copy()
            rng = np.random.RandomState(self.rs())
            for _ in range(3):
                cpt = v[rng.randint(len(v))]
                d = np.linalg.norm(v - cpt, axis=1)
                v[:, 2] -= dent * np.exp(-(d / 0.045) ** 2)
            g["v"] = v
        o = add(name, gxf(g, T(0.0, -0.010, z)), cls, src, bone="Head", sharp=50, solidify=0.004, solid_offset=-1.0)
        return o

    def nasal(self, name, src, z0=1.735, z1=1.652, y=-0.138, cls="metal", width=0.022):
        o = add(name, g_tube([(0, y + 0.006, z0 + 0.012), (0, y, z0 - 0.02), (0, y - 0.004, (z0 + z1) / 2), (0, y + 0.002, z1)],
                             [1.0, 1.0, 0.95, 0.8], n=6, sx=0.0045, sy=width / 2, up=(0, -1, 0)), cls, src, bone="Head", sharp=50)
        return o

    def circlet(self, name, src, z=1.752, arc=102.0, r=0.0085, cls="metal", flat=1.35, lift=0.007, rise=0.014):
        """band round the brow (open at the back when arc < 180); returns (object, path)"""
        bp = [(0.135 * math.sin(a), -0.02 - 0.145 * math.cos(a), z + rise * abs(math.sin(a))) for a in np.radians(np.linspace(-arc, arc, 17 if arc < 179 else 28))]
        if arc >= 179:
            bp = bp[:-1]
        P = wrap_path(self.c, bp, lift)
        o = add(name, g_tube(P, r, n=6, sx=flat, sy=0.6, up=(0, 0, 1), closed=arc >= 179), cls, src, bone="Head", sharp=50)
        return o, P

    def headband(self, name, src, z=1.738, width=0.036, cls="cloth", tails=(0.22, 0.16), off=0.006, tilt=0.012):
        pts = [(0.105 * math.sin(a), -0.020 - 0.115 * math.cos(a), z + tilt * math.cos(a)) for a in np.linspace(0, TAU, 24, endpoint=False)]
        o, Q, N = ribbon(self.c, pts, src, width=width, thick=0.005, offset=off, name=name, cls=cls, closed=True, step=0.02, only=["Head"], sharp=180)
        w_rigid(o, "Head")
        out = [o]
        kb = np.array((0.0, 0.105, z - 0.004))
        ok, loc, nrm, fi = self.c.ref.closest_point_on_mesh(Vector(kb))
        kb = np.array(loc) + np.array(nrm) * 0.014
        if tails:
            k = add(name + "_knot", gxf(g_sphere(1.0, 7, 5), T(*kb) @ S(0.020, 0.014, 0.018)), cls, src, sharp=180, bone="Head")
            out.append(k)
            for i, ln in enumerate(tails):
                sx = 0.03 if i == 0 else -0.034
                path = [kb, kb + np.array((sx * 0.4, 0.02, -ln * 0.3)), kb + np.array((sx, 0.03, -ln * 0.7)), kb + np.array((sx * 1.2, 0.02, -ln))]
                t = add("%s_tail_%d" % (name, i), g_tube(path, [0.9, 1.0, 1.1, 1.25], n=5, sx=0.004, sy=width * 0.42, up=(0, 1, 0)), cls, src, sharp=180)

                def wf(co):
                    d = sstep(kb[2] - co[:, 2], 0.03, 0.20)
                    return {"Head": 1 - d * 0.75, "neck_01": d * 0.35, "spine_03": d * 0.40}
                w_func(t, wf)
                out.append(t)
        return out
