r"""Blender entry point: models and renders the mouse cursors of Ashen Abyss with the UI art kit.

   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\render.ps1 [-Kinds default,attack|all] [-Builds hi,lo] [-Samples 96] [-Scale 4]

Every cursor is a small 3D model built with Y:\tools\uiart\kit.py (same forged-iron / gold / ember materials, same sun
from the top-left, same studio world, orthographic camera straight down). A model is authored UPRIGHT in design px
(1 design px = 1 px of the 64 px cursor), y down, then turned, scaled to fill the 64 px canvas minus the room the
outline needs, and rendered at Scale x (4 -> a 256 px master). The hotspot is a point of the model (`hot`, authoring
px) and is written out with the picture.

Two builds of every model: `hi` (64 px and larger) and `lo` (the small sizes: fewer, bolder parts and smoother metal).
Output: Y:\assets_src\cursor_gen\render\<kind>[_lo].png (16-bit RGBA, no outline: post.py adds it) + <kind>[_lo].json.
"""
import sys, os, math, json, time, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
UIART = os.path.join(os.path.dirname(HERE), "uiart")
for p in (HERE, UIART):
    if p not in sys.path: sys.path.insert(0, p)
import bpy
import kit, styles

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

CANVAS = 64            # design px
MARGIN = 6.0           # room for the dark outline + ember rim that post.py draws (about 5 design px at the 32 px size)
PX = kit.PX
D = 'c_dark'
# material roles of the build in progress (set_detail)
LO = False
I, G, ST, STUD, GOLD = 'c_iron', 'trim', 'c_steel', 'stud', 'c_gold'


def set_detail(lo):
    """LO = the small-size build: no rivets, studs or hairlines, a larger stone, bolder parts, smoother metal, so that
    a 32 px cursor is a clear shape and not a speckle."""
    global LO, I, G, ST, STUD, GOLD
    LO = lo
    I, G, ST, STUD, GOLD = ('c_iron_lo', 'trim_lo', 'c_steel_lo', 'c_iron_lo', 'c_gold_lo') if lo else ('c_iron', 'trim', 'c_steel', 'stud', 'c_gold')


# ───────────────────────────── outline helpers (authoring px, y down) ─────────────────────────────
def prof(z0, h, r, steps=4):
    """Cross-section of a rounded slab: a wall, then a quarter-round of radius r (squashed when r > h) up to z0 + h."""
    zr = min(h, r)
    out = [(0.0, z0)]
    if h - zr > 1e-6: out.append((0.0, z0 + h - zr))
    for i in range(1, steps + 1):
        a = math.pi * 0.5 * i / steps
        out.append((r * (1.0 - math.cos(a)), z0 + (h - zr) + zr * math.sin(a)))
    return out


def seg(c0, c1, w0, w1, cap0='flat', cap1='flat', b0=0.0, b1=0.0, n=4):
    """Outline of a tapering segment from c0 to c1 (half widths w0, w1); caps: flat | round | point (b = how far it bulges)."""
    dx, dy = c1[0] - c0[0], c1[1] - c0[1]
    L = math.hypot(dx, dy) or 1e-9
    dx /= L; dy /= L
    nx, ny = -dy, dx
    pts = [(c0[0] + nx * w0, c0[1] + ny * w0), (c1[0] + nx * w1, c1[1] + ny * w1)]
    if cap1 == 'round':
        for i in range(1, n):
            a = math.pi * i / n
            pts.append((c1[0] + nx * w1 * math.cos(a) + dx * b1 * math.sin(a), c1[1] + ny * w1 * math.cos(a) + dy * b1 * math.sin(a)))
    elif cap1 == 'point':
        pts.append((c1[0] + dx * b1, c1[1] + dy * b1))
    pts += [(c1[0] - nx * w1, c1[1] - ny * w1), (c0[0] - nx * w0, c0[1] - ny * w0)]
    if cap0 == 'round':
        for i in range(1, n):
            a = math.pi * i / n
            pts.append((c0[0] - nx * w0 * math.cos(a) - dx * b0 * math.sin(a), c0[1] - ny * w0 * math.cos(a) - dy * b0 * math.sin(a)))
    elif cap0 == 'point':
        pts.append((c0[0] - dx * b0, c0[1] - dy * b0))
    return pts


def arc(cx, cy, r, a0, a1, n=5):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def up(deg):
    """Unit vector `deg` degrees clockwise from straight up (y down)."""
    a = math.radians(deg)
    return (math.sin(a), -math.cos(a))


def along(p, d, t):
    return (p[0] + d[0] * t, p[1] + d[1] * t)


def jewel(k, cx, cy, rx, ry, z, bezel=None, stone='stone', h=None):
    """Ember stone in a raised bezel (z = the surface it sits on)."""
    k.solid(k.rhombus(cx, cy, rx + 2.4, ry + 2.4), [(0, z - 1.0), (0, z + 1.2), (1.0, z + 2.1), (2.2, z + 2.1), (2.9, z + 1.0)], bezel or G, cap=True, name="bezel")
    k.gem(k.rhombus(cx, cy, rx, ry), h if h is not None else min(rx, ry) * 0.62, stone, z=z + 1.3, table=0.42)


# ───────────────────────────── models ─────────────────────────────
def m_point(k, dim=False):
    """Forged gauntlet, index finger out: lames ending in a talon (the hotspot), a fist of three curled fingers,
    thumb, knuckle plate with a gold hairline and an ember stone, flared cuff with a gold rim.
    dim: the same hand gone cold (sooty iron, dull trim, dead stone) for the forbidden cursor."""
    iron, gold, stud = ('c_iron_dim', 'trim_off', 'c_iron_dim') if dim else (I, G, STUD)
    stone = 'c_dead' if dim else 'stone'
    if LO:
        k.solid([(0, 0), (5.0, 13.0), (5.4, 17.2), (-5.4, 17.2), (-5.0, 13.0)], prof(0, 4.0, 3.0), iron, name="talon")
        k.solid(seg((0, 41.0), (0, 15.2), 7.0, 6.2, cap1='round', b1=2.6), prof(0, 5.4, 3.8), iron, name="idx")
        for vc, ue, hw, zt in ((10.6, 30.5, 4.3, 5.2), (18.4, 32.2, 4.1, 5.0), (25.4, 34.8, 3.6, 4.7)):
            k.solid(seg((vc, 42.0), (vc, ue + hw * 0.75), hw, hw * 0.96, cap1='round', b1=hw * 0.75), prof(0, zt, 3.0), iron, name="curl")
        k.solid(seg((-7.0, 53.0), (-13.4, 36.4), 4.8, 3.6, cap1='round', b1=3.0), prof(0, 4.8, 3.0), iron, name="thumb")
        back = [(-7.2, 38.0), (28.2, 38.0), (30.0, 41.4), (29.0, 54.4), (-8.2, 54.4), (-9.2, 41.4)]
        k.solid(back, prof(0, 7.2, 2.6), iron, name="back")
        jewel(k, 10.4, 46.4, 4.4, 5.0, 7.2, bezel=gold, stone=stone)
        k.solid([(-8.4, 52.6), (29.2, 52.6), (31.0, 61.5), (-10.4, 61.5)], prof(0, 6.0, 2.2), iron, name="cuff")
        k.solid([(-9.4, 56.5), (30.0, 56.5), (31.0, 61.5), (-10.4, 61.5)], prof(0, 7.4, 1.8), gold, name="cuffband")
        return
    # index finger: each lame nearer the hand rides over the one before it, so every joint shows a lit step
    k.solid([(0, 0), (4.2, 13.5), (4.4, 17.6), (-4.4, 17.6), (-4.2, 13.5)], prof(0, 3.6, 2.6), iron, name="talon")
    k.solid(seg((0, 28.2), (0, 16.4), 5.5, 4.9, cap1='round', b1=2.2), prof(0, 4.6, 3.1), iron, name="idx2")
    k.solid(seg((0, 41.0), (0, 27.0), 6.2, 5.7, cap1='round', b1=2.4), prof(0, 5.6, 3.5), iron, name="idx3")
    # the three curled fingers: short barrels that end where the finger folds under
    for vc, ue, hw, zt in ((9.5, 31.0, 3.7, 5.2), (16.5, 32.6, 3.5, 4.9), (22.7, 35.0, 3.0, 4.5)):
        k.solid(seg((vc, 42.0), (vc, ue + hw * 0.75), hw, hw * 0.96, cap1='round', b1=hw * 0.75), prof(0, zt, 2.7), iron, name="curl")
    # thumb along the side of the fist
    k.solid(seg((-6.8, 53.0), (-10.6, 42.6), 4.5, 3.9, cap1='round', b1=2.2), prof(0, 5.0, 2.9), iron, name="thumb1")
    k.solid(seg((-10.2, 44.4), (-13.8, 35.0), 3.6, 2.7, cap1='round', b1=2.6), prof(0, 4.1, 2.5), iron, name="thumb2")
    # knuckle plate over the back of the hand
    back = [(-6.5, 38.2), (25.2, 38.2), (27.0, 41.4), (26.2, 54.6), (-7.5, 54.6), (-8.3, 41.4)]
    k.solid(back, prof(0, 7.2, 2.3), iron, name="back")
    k.solid(k.offset(back, 2.7), [(0, 6.9), (0.45, 7.9), (1.5, 7.9), (1.95, 6.9)], gold, cap=False, name="hair")
    for v in (0.0, 9.5, 16.5, 22.7):
        k.pyramid(v, 42.0, 1.9, 1.4, stud, z=7.2)
    jewel(k, 9.5, 48.6, 3.3, 3.9, 7.2, bezel=gold, stone=stone)
    # cuff
    k.solid([(-7.7, 53.2), (26.4, 53.2), (28.2, 62.4), (-9.8, 62.4)], prof(0, 6.0, 2.1), iron, name="cuff")
    k.solid([(-9.3, 59.0), (27.6, 59.0), (28.2, 62.4), (-9.8, 62.4)], prof(0, 7.3, 1.5), gold, name="cuffband")
    for v in (-3.2, 9.2, 21.6):
        k.dome(v, 56.2, 1.5, 1.1, stud, z=6.0)


def m_grab(k):
    """Pick up: the gauntlet as an open, reaching hand. Four fingers fan out from the palm and their talons curl in,
    the thumb stands off to the side, the wrist is narrower than the palm (parallel spikes on a band read as a crown).
    Returns the hotspot: the tip of the longest talon."""
    C = (0.0, 40.0)
    fw = 4.1 if LO else 3.6
    hot = None
    for phi, ln in ((-30.0, 17.0), (-10.0, 19.5), (9.0, 18.0), (27.0, 14.5)):
        d1 = up(phi); p0 = along(C, d1, 9.5); p1 = along(p0, d1, ln * 0.55)
        d2 = up(phi + max(-22.0, min(22.0, -phi * 0.9)))
        claw = ln * 0.45
        k.solid(seg(along(p1, d1, -1.5), along(p1, d2, claw - 6.0), fw * 0.86, fw * 0.64, cap1='point', b1=6.0), prof(0, 4.0 if LO else 3.7, 2.3), I, name="talon")
        k.solid(seg(p0, p1, fw, fw * 0.9, cap1='round', b1=1.9), prof(0, 5.0, 3.0), I, name="finger")
        if phi == -10.0: hot = along(p1, d2, claw)
    d1 = up(-60.0); p0 = along(C, up(-84.0), 8.0); p1 = along(p0, d1, 10.0); d2 = up(-28.0)
    k.solid(seg(along(p1, d1, -1.5), along(p1, d2, 3.4), fw * 0.9, fw * 0.66, cap1='point', b1=6.0), prof(0, 4.0 if LO else 3.7, 2.3), I, name="talon")
    k.solid(seg(p0, p1, fw * 1.1, fw * 0.95, cap1='round', b1=1.9), prof(0, 5.0, 3.0), I, name="thumb")
    back = [along(C, up(a), r) for a, r in ((-52.0, 13.4), (-24.0, 14.4), (0.0, 14.6), (20.0, 14.4), (46.0, 13.2), (96.0, 11.6))] + [(7.6, 52.8), (-7.6, 52.8)] + [along(C, up(-104.0), 11.4)]
    k.solid(back, prof(0, 7.2, 2.6 if LO else 2.3), I, name="back")
    if not LO:
        k.solid(k.offset(back, 2.7), [(0, 6.9), (0.45, 7.9), (1.5, 7.9), (1.95, 6.9)], G, cap=False, name="hair")
        for phi in (-30.0, -10.0, 9.0, 27.0):
            v = along(C, up(phi), 10.6)
            k.pyramid(v[0], v[1], 1.8, 1.3, STUD, z=7.2)
    jewel(k, 0.0, 41.4, 4.2 if LO else 3.3, 4.8 if LO else 3.9, 7.2)
    k.solid([(-8.4, 51.0), (8.4, 51.0), (10.6, 59.5), (-10.6, 59.5)], prof(0, 6.0, 2.1), I, name="cuff")
    k.solid([(-9.6, 55.2 if LO else 56.0), (9.6, 55.2 if LO else 56.0), (10.6, 59.5), (-10.6, 59.5)], prof(0, 7.3, 1.6), G, name="cuffband")
    return hot


def m_sword(k):
    """Attack: a short forge-hot blade. Steel with a glowing ember fuller, gold cross with an ember stone, wrapped grip."""
    if LO:
        k.solid([(0, 0), (4.4, 8.4), (5.6, 45.6), (-5.6, 45.6), (-4.4, 8.4)], [(0, 0), (0, 1.4), (3.2, 3.6)], ST, name="blade")
        k.solid(seg((0, 40.5), (0, 12.0), 1.7, 1.2, cap0='point', b0=1.8, cap1='point', b1=3.6), [(0, 3.45), (0.4, 4.1)], 'c_hot', name="fuller")
        guard = [(-17.0, 49.4), (-13.0, 45.0), (-5.0, 46.0), (0, 44.6), (5.0, 46.0), (13.0, 45.0), (17.0, 49.4), (13.0, 53.8), (5.0, 52.8), (-5.0, 52.8), (-13.0, 53.8)]
        k.solid(guard, prof(0, 5.6, 2.0), G, name="guard")
        k.gem(k.rhombus(0, 49.2, 3.4, 3.2), 2.0, 'stone', z=5.6, table=0.42)
        k.solid(seg((0, 52.4), (0, 63.6), 3.3, 3.0), prof(0, 4.2, 2.2), 'c_leather', name="grip")
        k.solid(seg((-3.8, 58.0), (3.8, 58.0), 1.0, 1.0), prof(0, 4.8, 0.7), G, name="wrap")
        k.solid(k.ngon(0, 67.4, 5.4, 6, rot=math.pi / 6), prof(0, 5.0, 2.0), G, name="pommel")
        return
    k.solid([(0, 0), (3.5, 7.6), (4.5, 46.6), (-4.5, 46.6), (-3.5, 7.6)], [(0, 0), (0, 1.3), (2.7, 3.3)], ST, name="blade")
    k.solid(seg((0, 41.5), (0, 11.0), 1.15, 0.75, cap0='point', b0=1.6, cap1='point', b1=3.2), [(0, 3.15), (0.3, 3.8)], 'c_hot', name="fuller")
    guard = [(-15.0, 49.2), (-11.8, 45.6), (-4.6, 46.5), (0, 45.2), (4.6, 46.5), (11.8, 45.6), (15.0, 49.2), (11.8, 52.8), (4.6, 51.9), (-4.6, 51.9), (-11.8, 52.8)]
    k.solid(guard, prof(0, 5.2, 1.6), G, name="guard")
    k.gem(k.rhombus(0, 48.9, 2.7, 2.5), 1.7, 'stone', z=5.2, table=0.42)
    k.solid(seg((0, 51.6), (0, 64.6), 2.6, 2.3), prof(0, 4.0, 1.9), 'c_leather', name="grip")
    for u in (55.2, 58.3, 61.4):
        k.solid(seg((-3.0, u), (3.0, u), 0.7, 0.7), prof(0, 4.5, 0.55), G, name="wrap")
    k.solid(k.ngon(0, 68.2, 4.5, 6, rot=math.pi / 6), prof(0, 4.8, 1.8), G, name="pommel")
    k.dome(0, 68.2, 1.5, 1.1, STUD, z=4.8)


def m_talk(k):
    """Talk: a speech plaque. Iron rim, warm gold face, three dark studs; the tail's point is the hotspot."""
    x0, y0, x1, y1, r = 7.0, 10.0, 54.0, 43.0, 9.0
    out = [(17.5, y0)] + arc(x1 - r, y0 + r, r, -90, 0) + arc(x1 - r, y1 - r, r, 0, 90) + arc(x0 + r, y1 - r, r, 90, 180) + [(x0, 21.0), (0, 0)]
    if LO:
        k.solid(out, [(0, 0), (0, 4.4), (1.6, 6.0), (3.0, 6.0), (4.1, 4.8)], I, cap=False, name="rim")
        k.solid(k.offset(out, 3.9), [(0, 4.4), (0.9, 5.2)], GOLD, cap=True, name="face")
        for x in (19.4, 30.5, 41.6):
            k.dome(x, 26.5, 3.6, 1.8, D, z=5.1)
        return
    k.solid(out, [(0, 0), (0, 4.4), (1.4, 5.8), (2.6, 5.8), (3.5, 4.7)], I, cap=False, name="rim")
    k.solid(k.offset(out, 3.3), [(0, 4.3), (0.9, 5.1)], GOLD, cap=True, name="face")
    for x in (19.6, 30.5, 41.4):
        k.dome(x, 26.5, 3.1, 1.7, D, z=5.0)


def m_ui(k, lit=False):
    """Interface pointer: a slim forged spearhead (steel, ridge down the middle), gold ferrule, ember stud."""
    band = 'trim_hi' if lit else G
    if LO:
        head = [(0, 0), (9.6, 25.0), (3.4, 21.2), (3.8, 35.5), (0, 40.0), (-3.8, 35.5), (-3.4, 21.2), (-9.6, 25.0)]
        k.solid(head, [(0, 0), (0, 1.4), (2.4, 3.4)], ST, name="spear")
        k.solid(seg((-4.8, 25.8), (4.8, 25.8), 2.2, 2.2), prof(0, 4.6, 1.2), band, name="ferrule")
        if lit:
            k.solid(seg((0, 20.0), (0, 8.6), 1.3, 0.8, cap1='point', b1=3.0), [(0, 3.3), (0.35, 3.9)], 'trim_hi', name="inlay")
        return
    head = [(0, 0), (8.6, 25.0), (2.7, 21.6), (3.0, 36.5), (0, 40.5), (-3.0, 36.5), (-2.7, 21.6), (-8.6, 25.0)]
    k.solid(head, [(0, 0), (0, 1.3), (2.0, 3.2)], ST, name="spear")
    k.solid(seg((-3.9, 25.6), (3.9, 25.6), 1.7, 1.7), prof(0, 4.3, 1.0), band, name="ferrule")
    k.dome(0, 34.6, 1.8, 1.5, 'ember_lit' if lit else 'stone', z=3.0)
    if lit:
        k.solid(seg((0, 20.5), (0, 8.0), 0.85, 0.5, cap1='point', b1=2.6), [(0, 3.1), (0.25, 3.6)], 'trim_hi', name="inlay")


def m_ui_hot(k):
    m_ui(k, True)


def m_caret(k):
    """Text caret: a thin iron I-beam with a gold band."""
    if LO:
        out = [(-8, 0.5), (-4, 0), (0, 2.6), (4, 0), (8, 0.5), (8, 4.2), (4.6, 3.8), (2.7, 6.2), (2.7, 43.8), (4.6, 46.2), (8, 45.8), (8, 49.5),
               (4, 50), (0, 47.4), (-4, 50), (-8, 49.5), (-8, 45.8), (-4.6, 46.2), (-2.7, 43.8), (-2.7, 6.2), (-4.6, 3.8), (-8, 4.2)]
        k.solid(out, [(0, 0), (0, 1.6), (1.2, 2.6)], ST, name="beam")
        k.solid(seg((-3.9, 25), (3.9, 25), 2.2, 2.2), prof(0, 3.6, 0.9), G, name="band")
        return
    out = [(-7, 0.5), (-3.5, 0), (0, 2.2), (3.5, 0), (7, 0.5), (7, 3.3), (3.8, 2.9), (1.9, 5.3), (1.9, 44.7), (3.8, 47.1), (7, 46.7), (7, 49.5),
           (3.5, 50), (0, 47.8), (-3.5, 50), (-7, 49.5), (-7, 46.7), (-3.8, 47.1), (-1.9, 44.7), (-1.9, 5.3), (-3.8, 2.9), (-7, 3.3)]
    k.solid(out, [(0, 0), (0, 1.5), (0.9, 2.4)], ST, name="beam")
    k.solid(seg((-2.9, 25), (2.9, 25), 1.7, 1.7), prof(0, 3.3, 0.8), G, name="band")


def m_busy(k):
    """Busy: the Sun-Forge crest, a rayed iron disc with an ember heart."""
    n = 8 if LO else 12
    for i in range(n):
        long = i % 2 == 0
        r0, r1 = 13.6, (25.0 if long else 20.0)
        hw = (3.7 if long else 3.0) if LO else (2.9 if long else 2.2)
        a = math.radians(i * 360.0 / n - 90.0)
        c, s = math.cos(a), math.sin(a)
        tri = [(r0 * c + hw * s, r0 * s - hw * c), (r1 * c, r1 * s), (r0 * c - hw * s, r0 * s + hw * c)]
        k.solid(tri, [(0, 0), (0, 1.7), (1.1, 3.0)], G if long else I, name="ray")
    k.solid(k.ngon(0, 0, 14.6, 32), [(0, 0), (0, 3.8), (1.2, 5.0), (2.8, 5.0), (4.0, 3.6)], I, cap=False, name="ring")
    k.floor(k.ngon(0, 0, 11.4, 32), D, z=2.2)
    k.solid(k.ngon(0, 0, 10.6, 32), [(0, 2.6), (0.4, 4.4), (1.3, 4.4), (1.7, 2.6)], G, cap=False, name="bezel")
    k.gem(k.ngon(0, 0, 8.6, 8, rot=math.pi / 8), 3.2, 'stone', z=2.4, table=0.5)


# ── badges: built in CANVAS px after the gauntlet has been placed, so the hand is identical in all three cursors ──
BADGE = (43.8, 44.0, 12.2)        # centre x, y, outer radius (design px)


def badge_gear(k):
    """Interact: a gold cog with a dark hub on the cuff."""
    cx, cy, R = BADGE
    k.solid(k.ngon(cx, cy, R + 1.7, 32), prof(0, 9.6, 1.0), D, name="back")
    n, root = (6, R * 0.66) if LO else (8, R * 0.70)
    step = 2 * math.pi / n
    pts = []
    for i in range(n):
        a0 = step * i + step * 0.5
        for da, r in ((-0.31 * step, root), (-0.20 * step, R), (0.20 * step, R), (0.31 * step, root)):
            pts.append((cx + r * math.cos(a0 + da), cy + r * math.sin(a0 + da)))
    k.solid(pts, prof(0, 12.2, 1.3), GOLD, name="gear")
    if not LO:
        k.solid(k.ngon(cx, cy, R * 0.50, 24), [(0, 12.0), (0.4, 13.2), (1.4, 13.2), (1.9, 12.0)], G, cap=False, name="collar")
    k.floor(k.ngon(cx, cy, R * (0.33 if LO else 0.36), 24), D, z=12.4)
    if not LO:
        k.dome(cx, cy, R * 0.20, 1.3, 'stone', z=12.4)


def badge_no(k):
    """Forbidden: a red ring, broken in two, with the bar through it."""
    cx, cy, R = BADGE
    ri = R * (0.56 if LO else 0.64)
    k.solid(k.ngon(cx, cy, R + 1.7, 32), prof(0, 9.6, 1.0), D, name="back")
    gap = 17.0 if LO else 15.0
    for a0, a1 in ((-45.0 + gap, 135.0 - gap), (135.0 + gap, 315.0 - gap)):
        outer = arc(cx, cy, R, a0, a1, 14)
        inner = arc(cx, cy, ri, a1, a0, 14)
        k.solid(outer + inner, prof(0, 12.2, 1.2), 'c_red', name="arc")
    d = (math.cos(math.radians(45.0)), math.sin(math.radians(45.0)))
    bw = 2.7 if LO else 2.1
    k.solid(seg((cx - d[0] * (ri + 0.6), cy - d[1] * (ri + 0.6)), (cx + d[0] * (ri + 0.6), cy + d[1] * (ri + 0.6)), bw, bw), prof(0, 12.2, 1.1), 'c_red', name="bar")


def m_interact(k): m_point(k)
def m_forbid(k): m_point(k, True)


# kind: model, turn (degrees counter-clockwise on screen), hotspot (authoring px), fit box (design px), alignment, badge
CURSORS = {
    'default':  dict(fn=m_point, ang=40.0, hot=(0, 0)),
    'interact': dict(fn=m_interact, ang=40.0, hot=(0, 0), badge=badge_gear),
    'forbid':   dict(fn=m_forbid, ang=40.0, hot=(0, 0), badge=badge_no),
    'attack':   dict(fn=m_sword, ang=32.0, hot=(0, 0)),
    'talk':     dict(fn=m_talk, ang=0.0, hot=(0, 0)),
    'pickup':   dict(fn=m_grab, ang=34.0),
    'ui':       dict(fn=m_ui, ang=27.0, hot=(0, 0), box=47.0),
    'ui_hot':   dict(fn=m_ui_hot, ang=27.0, hot=(0, 0), box=47.0),
    'caret':    dict(fn=m_caret, ang=0.0, hot=(0, 25), box=46.0, align='center'),
    'busy':     dict(fn=m_busy, ang=0.0, hot=(0, 0), align='center'),
}
ORDER = ['default', 'attack', 'talk', 'pickup', 'interact', 'forbid', 'ui', 'ui_hot', 'caret', 'busy']


def materials(k):
    """The UI kit's style A (forged iron, ember-gold inlay, ember stones) + the few looks only the cursors need."""
    S = styles.StyleA()
    k.MATS.clear()
    S.mats(k)
    S.finish(k)
    M = k.MATS
    # a cursor is tiny and lies over dark scenes: its iron is a shade lighter than the frames' and its edges rub brighter
    M['c_iron'] = k.mat_metal('c_iron', metal=0.9, base=(0.13, 0.125, 0.134), rough=(0.27, 0.50), edge=(0.95, 0.90, 0.80), edge_amt=1.1,
                              dark=(0.04, 0.035, 0.032), soot=0.65, hammer=0.5)
    M['c_steel'] = k.mat_metal('c_steel', metal=0.55, spec=0.6, base=(0.50, 0.50, 0.53), rough=(0.24, 0.40), edge=(1.0, 0.98, 0.94), edge_amt=0.8,
                               dark=(0.20, 0.20, 0.22), soot=0.4, hammer=0.15, bump=0.5)
    M['c_gold'] = k.mat_metal('c_gold', base=(0.90, 0.52, 0.15), rough=(0.24, 0.42), edge=(1.0, 0.88, 0.58), edge_amt=0.6, dark=(0.45, 0.22, 0.05),
                              soot=0.35, hammer=0.3, bump=0.5)
    M['c_dark'] = k.mat_metal('c_dark', metal=0.5, spec=0.2, base=(0.012, 0.011, 0.012), rough=(0.5, 0.72), edge=(0.2, 0.19, 0.18), edge_amt=0.3,
                              dark=(0.008, 0.007, 0.007), soot=0.8, hammer=0.3)
    M['c_iron_dim'] = k.mat_metal('c_iron_dim', metal=0.7, spec=0.3, base=(0.040, 0.038, 0.042), rough=(0.42, 0.66), edge=(0.42, 0.40, 0.37), edge_amt=0.7,
                                  dark=(0.018, 0.016, 0.015), soot=0.9, hammer=0.5)
    M['c_dead'] = k.mat_plain('c_dead', (0.05, 0.014, 0.01), 0.22)
    M['c_leather'] = k.mat_leather('c_leather', tint=(0.07, 0.035, 0.022))
    # the small-size build: the same metals with less hammering and grain, so a 32 px cursor shows planes, not speckle
    M['c_iron_lo'] = k.mat_metal('c_iron_lo', metal=0.88, base=(0.17, 0.165, 0.175), rough=(0.30, 0.48), edge=(1.0, 0.95, 0.86), edge_amt=1.2,
                                 dark=(0.055, 0.05, 0.046), soot=0.5, hammer=0.12, bump=0.35)
    M['trim_lo'] = k.mat_metal('trim_lo', base=(0.92, 0.54, 0.16), rough=(0.26, 0.42), edge=(1.0, 0.90, 0.60), edge_amt=0.5, dark=(0.50, 0.25, 0.06),
                               soot=0.3, hammer=0.1, bump=0.3)
    M['c_gold_lo'] = k.mat_metal('c_gold_lo', base=(0.92, 0.55, 0.17), rough=(0.28, 0.44), edge=(1.0, 0.90, 0.60), edge_amt=0.5, dark=(0.55, 0.28, 0.07),
                                 soot=0.25, hammer=0.1, bump=0.3)
    M['c_steel_lo'] = k.mat_metal('c_steel_lo', metal=0.55, spec=0.6, base=(0.52, 0.52, 0.55), rough=(0.26, 0.42), edge=(1.0, 0.98, 0.94), edge_amt=0.8,
                                  dark=(0.24, 0.24, 0.26), soot=0.3, hammer=0.05, bump=0.3)
    # glows are hue-safe: only the dominant channel goes above 1, so the Standard view does not clip them to yellow
    g = k.NT('c_hot')
    p = g.principled((0.5, 0.05, 0.01), 0.35, 0.0, None)
    g.put(p.inputs['Emission Color'], (1.0, 0.075, 0.008)); g.put(p.inputs['Emission Strength'], 1.7)
    M['c_hot'] = g.m
    g = k.NT('c_red')
    nrm, e = g.edge(1.4)
    p = g.principled((0.62, 0.02, 0.012), 0.32, 0.0, nrm)
    g.put(p.inputs['Emission Color'], (1.0, 0.03, 0.012)); g.put(p.inputs['Emission Strength'], 0.75)
    try:
        p.inputs['Coat Weight'].default_value = 0.6; p.inputs['Coat Roughness'].default_value = 0.12
    except KeyError:
        pass
    M['c_red'] = g.m


def world_pts():
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    pts = []
    for o in kit._objs:
        if o.type not in ('MESH', 'CURVE'): continue
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        mw = eo.matrix_world
        for v in me.vertices:
            w = mw @ v.co
            pts.append((w.x, w.y))
        eo.to_mesh_clear()
    return pts


def build(name, spec):
    """Models one cursor and places it on the canvas; returns (hotspot, bbox) in design px and the model scale."""
    kit.begin(CANVAS, CANVAS)
    model_hot = spec['fn'](kit)
    pts = world_pts()
    th = math.radians(spec.get('ang', 0.0))
    c, s = math.cos(th), math.sin(th)
    rot = [(x * c - y * s, x * s + y * c) for x, y in pts]
    minx = min(p[0] for p in rot); maxx = max(p[0] for p in rot)
    miny = min(p[1] for p in rot); maxy = max(p[1] for p in rot)
    half = CANVAS * 0.5
    box = spec.get('box', CANVAS - 2 * MARGIN)
    sc = box * PX / max(maxx - minx, maxy - miny)
    if spec.get('align', 'tl') == 'tl':
        tx = (-half + MARGIN) * PX - sc * minx
        ty = (half - MARGIN) * PX - sc * maxy
    else:
        tx = -sc * (minx + maxx) * 0.5
        ty = -sc * (miny + maxy) * 0.5
    rig = bpy.data.objects.new("rig", None)
    bpy.context.scene.collection.objects.link(rig)
    for o in list(kit._objs):
        o.parent = rig
    kit._objs.append(rig)
    rig.rotation_euler = (0.0, 0.0, th)
    rig.scale = (sc, sc, sc)
    rig.location = (tx, ty, 0.0)
    hx, hy = model_hot if model_hot is not None else spec.get('hot', (0, 0))
    lx, ly = (hx - half) * PX, (half - hy) * PX
    wx, wy = tx + sc * (lx * c - ly * s), ty + sc * (lx * s + ly * c)
    hot = (wx / PX + half, half - wy / PX)
    bbox = [(tx + sc * minx) / PX + half, half - (ty + sc * maxy) / PX, (tx + sc * maxx) / PX + half, half - (ty + sc * miny) / PX]
    if spec.get('badge'):
        spec['badge'](kit)
        bx, by, br = BADGE
        bbox = [min(bbox[0], bx - br - 1.7), min(bbox[1], by - br - 1.7), max(bbox[2], bx + br + 1.7), max(bbox[3], by + br + 1.7)]
    bpy.context.view_layer.update()
    return hot, bbox, sc


def main():
    which = str(opt('--kinds', 'all'))
    samples = int(opt('--samples', 96) or 96)
    scale = float(opt('--scale', 4.0))
    out_dir = str(opt('--out', r"Y:\assets_src\cursor_gen\render"))
    builds = [b for b in str(opt('--builds', 'hi,lo')).split(',') if b in ('hi', 'lo')]
    names = ORDER if which == 'all' else which.split(',')
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit.setup(samples)
    materials(kit)
    done = failed = 0
    t_all = time.time()
    for n in names:
        if n not in CURSORS:
            print("CURSOR unknown kind", n); failed += 1; continue
        for bld in builds:
            t0 = time.time()
            tag = n + ("_lo" if bld == 'lo' else "")
            try:
                set_detail(bld == 'lo')
                hot, bbox, sc = build(n, CURSORS[n])
                bpy.context.scene.render.resolution_percentage = int(round(100 * scale))
                kit.render(os.path.join(out_dir, tag + ".png"), samples)
                with open(os.path.join(out_dir, tag + ".json"), "w", encoding="utf-8") as f:
                    json.dump({"kind": n, "build": bld, "canvas": CANVAS, "scale": scale, "hot": [round(hot[0], 3), round(hot[1], 3)],
                               "bbox": [round(v, 2) for v in bbox], "model_scale": round(sc, 4)}, f)
                done += 1
                print("CURSOR ok %s hot=(%.2f, %.2f) bbox=(%.1f, %.1f, %.1f, %.1f) model x%.3f %.1fs" % (tag, hot[0], hot[1], bbox[0], bbox[1], bbox[2], bbox[3], sc, time.time() - t0))
            except Exception:
                traceback.print_exc()
                print("CURSOR FAIL", tag)
                failed += 1
            sys.stdout.flush()
    print("CURSOR done=%d failed=%d seconds=%.0f" % (done, failed, time.time() - t_all))


main()
