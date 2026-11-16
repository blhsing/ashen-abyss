"""Piece definitions of the UI art set (shared by render.py inside Blender and slice.py outside it).

Every piece has: name, kind, render size, slicing metadata and a build function `fn(k, S, m)` that models it with the
kit `k` in style `S`. Sizes are TEXTURE px of the 2x sheet (2 px = 1 design px at 1080p); keep everything even.

kinds
  nine    nine-slice frame. Rendered with TWO periods of every edge (so slice.py can make the edge tile seamless):
          render size = ml + 2*px + mr  by  mt + 2*py + mb; final size = ml + px + mr by mt + py + mb.
          margins include `pad` (room for the outer shadow, drawn outside the control rect) and a blend zone of
          plain moulding next to the tile. center=True keeps and tiles the middle (button faces).
  h3      three-slice horizontal strip (caps + tiling middle): ml, mr, px (two periods rendered), fixed height h.
  v3      three-slice vertical strip: mt, mb, py, fixed width w.
  sprite  fixed image; `pad` = transparent/shadow border around the logical box; `anchor` for ornaments.

post-processing keys (slice.py): shadow=(blur, dx, dy, opacity)  glow=(radius, strength)  backfill=(r, g, b, a)
  mip=True (the game builds mipmaps: pieces drawn much smaller than authored)
"""
import math
from styles import corners

PIECES = {}
ORDER = []


def piece(name, kind, **meta):
    def deco(fn):
        m = dict(name=name, kind=kind, fn=fn, **meta)
        m.setdefault('pad', 0)
        m.setdefault('samples', 96)
        m.setdefault('blend', 8)
        if kind == 'nine':
            m['w'] = m['ml'] + 2 * m['px'] + m['mr']
            m['h'] = m['mt'] + 2 * m['py'] + m['mb']
        elif kind == 'h3':
            m['w'] = m['ml'] + 2 * m['px'] + m['mr']
        elif kind == 'v3':
            m['h'] = m['mt'] + 2 * m['py'] + m['mb']
        for key in ('ml', 'mt', 'mr', 'mb', 'px', 'py', 'w', 'h', 'pad'):
            assert m.get(key, 0) % 2 == 0, (name, key)
        assert name not in PIECES, name
        PIECES[name] = m
        ORDER.append(name)
        return fn
    return deco


def variants(names, kind, **meta):
    """One build function for several states: fn(k, S, m, state)."""
    def deco(fn):
        for nm, state in names:
            piece(nm, kind, **meta)(lambda k, S, m, state=state: fn(k, S, m, state))
        return fn
    return deco


def box(m):
    """The logical rectangle (control rect) of a piece inside its render."""
    p = m['pad']
    return p, p, m['w'] - p, m['h'] - p


def inlay(k, poly, spec, mat='trim'):
    if not spec: return
    inset, w, z, h = spec
    k.solid(k.offset(poly, inset), [(0, z - 0.6), (0.7, z + h), (w - 0.7, z + h), (w, z - 0.6)], mat, cap=False, name="inlay")


def bar_width(profile):
    return profile[-1][0]


def edge_marks(m, lo, hi, start, period, step):
    """Positions along an edge, phase-locked to the tile start so both rendered periods are identical."""
    out = []
    i0 = int(math.floor((lo - start) / step)) - 1
    x = start + (i0 + 0.5) * step
    while x < hi:
        if x > lo: out.append(x)
        x += step
    return out


def frame(k, S, m, bar, inl, heavy, mat='bar', inl_mat='trim'):
    x0, y0, x1, y1 = box(m)
    R = k.rect(x0, y0, x1, y1)
    k.solid(R, bar, mat, cap=False, name="bar")
    inlay(k, R, inl, inl_mat)
    bw = bar_width(bar)
    for i, T in enumerate(corners(x0, y0, x1, y1)):
        S.corner(k, T, bw, heavy=heavy, idx=i)
    return R, bw


def plate_rim(k, out, mat, z=7.0, fz=4.8, rim=6.5, name="plate"):
    """A plate with a raised rim: the building block of buttons, plaques and cards."""
    k.solid(out, [(0, 0), (0, z - 2), (2, z), (rim - 1.5, z), (rim, z - 1.8), (rim + 1.5, fz)], mat, cap=True, name=name)


def hair(k, out, inset, z, mat='trim', w=2.4, h=1.4):
    k.solid(k.offset(out, inset), [(0, z - 0.4), (0.5, z + h - 0.4), (w - 0.5, z + h - 0.4), (w, z - 0.4)], mat, cap=False, name="hair")


def circle(cx, cy, r, n=72):
    return [(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def ring(k, cx, cy, r_out, profile, mat, n=72, name="ring"):
    """Round moulding: profile insets measured from the outer radius inward."""
    k.solid(circle(cx, cy, r_out, n), profile, mat, cap=False, name=name)


def spear(x0, x1, cy, hw, tip):
    """Horizontal bar from x0 to x1 with pointed ends."""
    return [(x0 + tip, cy - hw), (x1 - tip, cy - hw), (x1, cy), (x1 - tip, cy + hw), (x0 + tip, cy + hw), (x0, cy)]


def ray(k, c, a, r0, r1, wd, mat='plate', z=9.0):
    """One pointed ray of a sun disc centred on c, at angle a, from radius r0 to r1, half-width wd (radians) at the root."""
    p0 = (c[0] + r0 * math.cos(a - wd), c[1] + r0 * math.sin(a - wd))
    p1 = (c[0] + r1 * math.cos(a), c[1] + r1 * math.sin(a))
    p2 = (c[0] + r0 * math.cos(a + wd), c[1] + r0 * math.sin(a + wd))
    k.solid([p0, p1, p2], [(0, 0), (0, z), (2.0, z + 2.5)], mat, cap=True, name="ray")


# ═════════════════════════════════ frames ═════════════════════════════════
@piece('window', 'nine', ml=208, mt=208, mr=208, mb=208, px=256, py=256, pad=28, blend=24, shadow=(16, 5, 8, 0.6))
def window(k, S, m):
    R, bw = frame(k, S, m, S.win_bar, S.win_inlay, True)
    S.edge(k, m, R, bw, heavy=True)
    k.catcher()


@piece('crest', 'sprite', w=560, h=188, pad=0, anchor=(280, 100), shadow=(7, 3, 4, 0.55))
def crest(k, S, m):
    # anchor = middle of the window's outer top edge inside this sprite
    S.crest(k, m['anchor'][0], m['anchor'][1], bar_width(S.win_bar))
    k.catcher()


@piece('modal', 'nine', ml=100, mt=100, mr=100, mb=100, px=128, py=128, pad=28, blend=18, shadow=(16, 5, 8, 0.6))
def modal(k, S, m):
    frame(k, S, m, S.win_bar, S.win_inlay, False)
    k.catcher()


@piece('crest_small', 'sprite', w=220, h=100, pad=0, anchor=(110, 40), shadow=(6, 3, 4, 0.55))
def crest_small(k, S, m):
    S.crest_small(k, m['anchor'][0], m['anchor'][1], bar_width(S.win_bar))
    k.catcher()


@piece('panel', 'nine', ml=64, mt=64, mr=64, mb=64, px=128, py=128, pad=16, blend=12, shadow=(8, 2, 4, 0.5))
def panel(k, S, m):
    frame(k, S, m, S.sub_bar, S.sub_inlay, False)
    k.catcher()


@piece('well', 'nine', ml=24, mt=24, mr=24, mb=24, px=64, py=64, pad=0, blend=8)
def well(k, S, m):
    x0, y0, x1, y1 = box(m)
    R = k.rect(x0, y0, x1, y1)
    k.solid(R, [(0, 0), (0, 3.2), (1.6, 4.8), (4.2, 4.8), (5.6, 3.2), (7.5, 0.8)], 'bar', cap=False, name="rim")
    hair(k, R, 6.6, 1.2, 'trim_off', w=1.8, h=1.0)
    k.catcher()


@piece('tooltip', 'nine', ml=84, mt=96, mr=84, mb=84, px=128, py=128, pad=24, blend=16, shadow=(14, 4, 7, 0.7), band=(42, 12))
def tooltip(k, S, m):
    x0, y0, x1, y1 = box(m)
    R, bw = frame(k, S, m, S.tip_bar, S.tip_inlay, False)
    # trough for the rarity band under the top bar: two thin lips (the tinted band is its own layer)
    by, bh = m['band']
    k.solid(k.rect(x0 + bw - 1, y0 + bw - 2, x1 - bw + 1, y0 + bw + bh + 5), [(0, 0), (0, 2.6), (1.2, 3.6), (2.6, 3.6), (3.6, 1.0)], 'bar', cap=False, name="trough")
    k.catcher()


@piece('tooltip_band', 'h3', ml=20, mr=20, px=64, h=28, pad=6, blend=8)
def tooltip_band(k, S, m):
    # neutral enamel strip, tinted in the engine with the rarity colour
    x0, y0, x1, y1 = box(m)
    k.solid(k.chamfer_rect(x0, y0 + 2, x1, y1 - 2, 3), [(0, 0.5), (0.4, 2.8), (2.4, 4.2), (4.5, 4.4)], 'band', cap=True, name="band")


@piece('nameplate', 'h3', ml=64, mr=64, px=64, h=120, pad=12, blend=10, shadow=(6, 2, 4, 0.6))
def nameplate(k, S, m):
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 30)
    plate_rim(k, out, 'plate', z=11.0, fz=8.0, rim=7.5)
    hair(k, out, 11.5, 8.0)
    for x in (x0 + 34, x1 - 34):
        S.jewel(k, x, cy, 6.5, 6.5, 9.0, bezel=True, mat='trim', h=3.0)
    k.catcher()


@piece('plaque', 'nine', ml=52, mt=32, mr=52, mb=32, px=64, py=16, pad=12, blend=6, center=True, shadow=(6, 2, 3, 0.55))
def plaque(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 9)
    plate_rim(k, out, 'face_dn', z=6.0, fz=3.6, rim=5.0)
    hair(k, out, 8.0, 3.6, w=2.0, h=1.1)
    k.catcher()


@piece('tracker', 'nine', ml=40, mt=24, mr=40, mb=24, px=64, py=32, pad=8, blend=6, center=True, backfill=(0.016, 0.015, 0.02, 0.44), fill_inset=3)
def tracker(k, S, m):
    # quest-tracker backing: a translucent veil (added by slice.py) held by four small corner clips and two rules
    x0, y0, x1, y1 = box(m)
    for T in corners(x0, y0, x1, y1):
        k.solid(T([(0, 0), (22, 0), (22, 4), (4, 4), (4, 14), (0, 14)]), [(0, 0), (0, 2.6), (1.2, 3.8)], 'plate', cap=True, name="clip")
    for y in (y0 + 1.2, y1 - 3.2):
        k.solid(k.rect(x0 + 26, y, x1 - 26, y + 2.0), [(0, 0.2), (0.5, 1.4)], 'trim', cap=True, name="rule")


@piece('title_rule', 'sprite', w=1160, h=88, pad=16, anchor=(580, 44), glow=(5, 0.3))
def title_rule(k, S, m):
    # underline of the area title card: a long tapering gold line, an ember stone, two flame sprigs
    cx, cy = m['anchor']
    for s in (-1, 1):
        k.tube([(cx + s * 44, cy), (cx + s * 300, cy), (cx + s * 548, cy)], 2.6, 'trim', z=2.0, taper=[1.0, 0.7, 0.08])
        S.sprig(k, lambda p, s=s: (cx + s * p[0], p[1]), 34.0, cy, 120.0, 3.0)
        k.solid(k.rhombus(cx + s * 330, cy, 7, 7), [(0, 0.5), (2, 3.5)], 'trim', cap=True, name="bead")
    k.solid(k.rhombus(cx, cy, 30, 26), [(0, 0), (0, 5), (2.5, 7.5), (6, 7.5), (7.5, 5.5)], 'plate', cap=True, name="boss")
    S.jewel(k, cx, cy, 11, 13, 6.0)


# ═════════════════════════════════ buttons, tabs, small controls ═════════════════════════════════
@variants([('btn', 'normal'), ('btn_hover', 'hover'), ('btn_pressed', 'pressed'), ('btn_disabled', 'disabled')],
          'nine', ml=44, mt=40, mr=44, mb=40, px=64, py=32, pad=12, blend=8, center=True, shadow=(5, 2, 3, 0.5))
def btn(k, S, m, state):
    S.button(k, m, state)
    k.catcher()


@piece('btn_focus', 'nine', ml=44, mt=40, mr=44, mb=40, px=64, py=32, pad=12, blend=8, glow=(5, 0.8))
def btn_focus(k, S, m):
    p = m['pad']
    out = S.outline(p, p, m['w'] - p, m['h'] - p, S.btn_cut, k)
    k.solid(k.offset(out, 1.0), [(0, 6.6), (0.5, 8.0), (2.1, 8.0), (2.6, 6.6)], 'trim_hi', cap=False, name="focus")


def tab_outline(m, c=11):
    x0, y0, x1, y1 = box(m)
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1), (x0, y1), (x0, y0 + c)]


@variants([('tab', 'normal'), ('tab_hover', 'hover'), ('tab_on', 'on')],
          'nine', ml=44, mt=40, mr=44, mb=32, px=64, py=32, pad=12, blend=8, center=True, shadow=(5, 2, 3, 0.5))
def tab(k, S, m, state):
    out = tab_outline(m)
    face = {'normal': 'face_dn', 'hover': 'face', 'on': 'face_on'}[state]
    plate_rim(k, out, face, z=7.0, fz=4.8)
    x0, y0, x1, y1 = box(m)
    if state == 'on':
        hair(k, out, 9.2, 4.8, 'trim_hi')
        k.solid(k.rect(x0 + 16, y0 + 1.5, x1 - 16, y0 + 5.0), [(0, 6.6), (0.8, 8.2)], 'trim_hi', cap=True, name="lit")
    elif state == 'hover':
        hair(k, out, 9.2, 4.8, 'trim')
    else:
        hair(k, out, 9.2, 4.8, 'trim_off')
    k.catcher()


@variants([('close', 'normal'), ('close_hover', 'hover'), ('close_pressed', 'pressed')], 'sprite', w=88, h=88, pad=8, shadow=(4, 2, 3, 0.5))
def close(k, S, m, state):
    x0, y0, x1, y1 = box(m)
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    out = k.ngon(cx, cy, 36 / math.cos(math.pi / 8), 8, math.pi / 8)
    fz = 3.0 if state == 'pressed' else 5.0
    plate_rim(k, out, 'face_hi' if state == 'hover' else 'face', z=7.5, fz=fz, rim=6.0)
    hair(k, out, 9.0, fz, 'trim_hi' if state == 'hover' else 'trim', w=2.0, h=1.1)
    tm = 'trim_hi' if state == 'hover' else 'trim'
    for a, b in (((cx - 12, cy - 12), (cx + 12, cy + 12)), ((cx + 12, cy - 12), (cx - 12, cy + 12))):
        k.tube([a, ((a[0] + b[0]) * 0.5, (a[1] + b[1]) * 0.5), b], 3.0, tm, z=fz + 1.2, taper=[0.55, 1.0, 0.55])
    k.catcher()


@variants([('check_off', False), ('check_on', True)], 'sprite', w=56, h=56, pad=4)
def check(k, S, m, on):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0 + 2, y0 + 2, x1 - 2, y1 - 2, 6)
    k.solid(out, [(0, 0), (0, 4), (1.5, 5.5), (4, 5.5), (5.5, 3.5), (7.5, 1.5)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 7.0), [(0, 1.6)], 'floor', cap=True, name="floor")
    hair(k, out, 6.4, 1.6, 'trim' if on else 'trim_off', w=1.8, h=1.0)
    if on:
        cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        k.gem(k.rhombus(cx, cy, 10.5, 10.5), 4.5, 'stone', z=2.0, table=0.4)
    k.catcher()


@variants([('radio_off', False), ('radio_on', True)], 'sprite', w=56, h=56, pad=4)
def radio(k, S, m, on):
    x0, y0, x1, y1 = box(m)
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    ring(k, cx, cy, 22, [(0, 0), (0, 4), (1.5, 5.5), (4, 5.5), (5.5, 3.5), (7.5, 1.5)], 'bar', n=40)
    k.floor(circle(cx, cy, 15, 40), 'floor', z=1.6)
    ring(k, cx, cy, 15.6, [(0, 1.2), (0.5, 2.3), (1.4, 2.3), (1.9, 1.2)], 'trim' if on else 'trim_off', n=40, name="hair")
    if on:
        k.dome(cx, cy, 8.5, 5.0, 'stone', z=1.8)
    k.catcher()


@variants([('toggle_off', False), ('toggle_on', True)], 'sprite', w=96, h=56, pad=4)
def toggle(k, S, m, on):
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = k.round_rect(x0 + 2, y0 + 6, x1 - 2, y1 - 6, 17.5, 8)
    k.solid(out, [(0, 0), (0, 4), (1.5, 5.5), (3.6, 5.5), (5, 3.5), (6.5, 1.5)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 6.0), [(0, 1.6)], 'ember_lit' if on else 'floor', cap=True, name="floor")
    kx = x1 - 23 if on else x0 + 23
    k.dome(kx, cy, 12.5, 6.5, 'trim' if on else 'stud', z=1.6)
    k.catcher()


@piece('slider_track', 'h3', ml=16, mr=16, px=32, h=32, pad=6, blend=6)
def slider_track(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0 + 2, x1, y1 - 2, 4)
    k.solid(out, [(0, 0), (0, 2.6), (1.2, 3.8), (2.6, 3.8), (3.8, 2.2), (5.0, 1.0)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 4.6), [(0, 1.0)], 'floor', cap=True, name="floor")
    k.catcher()


@piece('slider_fill', 'h3', ml=12, mr=12, px=32, h=32, pad=6, blend=6, glow=(3, 0.35))
def slider_fill(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0 + 5, y0 + 7, x1 - 5, y1 - 7, 2)
    k.solid(out, [(0, 1.0), (0.4, 2.4), (1.6, 3.2)], 'trim_hi', cap=True, name="fill")


@variants([('slider_grab', False), ('slider_grab_hover', True)], 'sprite', w=60, h=60, pad=6, shadow=(3, 1, 2, 0.6))
def slider_grab(k, S, m, hot):
    x0, y0, x1, y1 = box(m)
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    k.solid(k.rhombus(cx, cy, 21, 21), [(0, 0), (0, 5), (2.2, 7.2), (5.2, 7.2), (6.6, 5.6)], 'plate', cap=True, name="knob")
    S.jewel(k, cx, cy, 7.5, 7.5, 6.0, bezel=True, mat='stone' if hot else 'trim', h=3.4)
    k.catcher()


@piece('scroll_track', 'v3', w=24, mt=16, mb=16, py=32, pad=0, blend=6)
def scroll_track(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0 + 4, y0 + 1, x1 - 4, y1 - 1, 3)
    k.solid(out, [(0, 0), (0, 1.8), (1.0, 2.8), (2.2, 2.8), (3.2, 1.6), (4.2, 0.8)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 3.8), [(0, 0.8)], 'floor', cap=True, name="floor")
    k.catcher()


@variants([('scroll_grab', False), ('scroll_grab_hover', True)], 'v3', w=24, mt=14, mb=14, py=16, pad=0, blend=4)
def scroll_grab(k, S, m, hot):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0 + 5, y0 + 2, x1 - 5, y1 - 2, 4)
    k.solid(out, [(0, 0.6), (0, 3.4), (1.6, 5.0), (3.0, 5.2)], 'trim' if hot else 'trim_off', cap=True, name="grab")
    k.catcher()


@variants([('input', False), ('input_focus', True)], 'nine', ml=28, mt=28, mr=28, mb=28, px=32, py=32, pad=4, blend=6, center=True)
def text_input(k, S, m, focus):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 5)
    k.solid(out, [(0, 0), (0, 3.4), (1.6, 5.0), (4.0, 5.0), (5.4, 3.2), (7.4, 1.2)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 7.0), [(0, 1.2)], 'floor', cap=True, name="floor")
    hair(k, out, 6.4, 1.3, 'trim_hi' if focus else 'trim_off', w=1.8, h=1.0)
    k.catcher()


@variants([('arrow_down', 0), ('arrow_right', 1)], 'sprite', w=36, h=36, pad=4)
def arrow(k, S, m, d):
    c = 18.0
    pts = [(c - 8, c - 4), (c, c + 5), (c + 8, c - 4)]
    if d == 1: pts = [(c - 4, c - 8), (c + 5, c), (c - 4, c + 8)]
    k.tube(pts, 2.2, 'trim', z=2.0, taper=[0.6, 1.0, 0.6])
    k.catcher()


@variants([('row', 'normal'), ('row_hover', 'hover'), ('row_on', 'on')], 'nine', ml=28, mt=16, mr=28, mb=16, px=32, py=16, pad=4, blend=4, center=True)
def row(k, S, m, state):
    # list row: a low plate; hover brightens its hairline, the chosen row is ember-lit with a stone at its head
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 4)
    face = {'normal': 'face_dn', 'hover': 'face', 'on': 'face_on'}[state]
    k.solid(out, [(0, 0), (0, 2.2), (1.4, 3.6), (3.0, 3.6), (4.0, 2.4)], face, cap=True, name="row")
    hair(k, out, 4.6, 2.4, {'normal': 'trim_off', 'hover': 'trim', 'on': 'trim_hi'}[state], w=1.8, h=1.0)
    k.catcher()


# ═════════════════════════════════ slots ═════════════════════════════════
@piece('slot', 'sprite', w=152, h=152, pad=12, shadow=(4, 2, 3, 0.45))
def slot(k, S, m):
    S.slot(k, m)
    k.catcher()


@piece('slot_rarity', 'sprite', w=152, h=152, pad=12, glow=(7, 0.9))
def slot_rarity(k, S, m):
    r = S.slot_inner(k, m)
    k.solid(r, [(0, 1.0), (0.5, 2.6), (2.6, 2.6), (3.2, 1.0)], 'band', cap=False, name="rar")


@piece('slot_rarity_hi', 'sprite', w=152, h=152, pad=12, glow=(9, 1.0))
def slot_rarity_hi(k, S, m):
    # legendary and above: the line plus four corner flares
    r = S.slot_inner(k, m)
    k.solid(r, [(0, 1.0), (0.5, 2.6), (3.0, 2.6), (3.6, 1.0)], 'band', cap=False, name="rar")
    x0, y0, x1, y1 = box(m)
    for T in corners(x0 + 9, y0 + 9, x1 - 9, y1 - 9):
        k.solid(T([(0, 0), (26, 0), (17, 5), (5, 5), (5, 17), (0, 26)]), [(0, 1.0), (0.6, 3.0)], 'band', cap=True, name="flare")


@variants([('slot_hover', False), ('slot_selected', True)], 'sprite', w=152, h=152, pad=12, glow=(6, 0.8))
def slot_hover(k, S, m, sel):
    p = m['pad']
    out = S.outline(p, p, m['w'] - p, m['h'] - p, S.slot_cut, k)
    k.solid(k.offset(out, 0.5 if sel else 1.5), [(0, 5.2), (0.5, 6.8), (3.2 if sel else 2.2, 6.8), (3.8 if sel else 2.7, 5.2)], 'trim_hi', cap=False, name="hot")
    if sel:
        x0, y0, x1, y1 = box(m)
        for T in corners(x0 - 2, y0 - 2, x1 + 2, y1 + 2):
            k.solid(T([(0, 0), (30, 0), (30, 5), (5, 5), (5, 30), (0, 30)]), [(0, 5.0), (0.8, 8.0), (2.0, 8.0)], 'trim_hi', cap=True, name="tick")


@variants([('skill_slot', 'normal'), ('skill_slot_pressed', 'pressed')], 'sprite', w=176, h=176, pad=16, shadow=(5, 2, 4, 0.5))
def skill_slot(k, S, m, state):
    x0, y0, x1, y1 = box(m)
    out = S.outline(x0, y0, x1, y1, 13, k)
    k.solid(out, [(0, 0), (0, 7), (2.5, 9.5), (6.5, 9.5), (8.5, 7), (10, 5.5), (12.5, 5.5), (14.5, 2.0)], 'bar', cap=False, name="rim")
    k.solid(k.offset(out, 14.0), [(0, 2.0)], 'floor', cap=True, name="floor")
    hair(k, out, 9.6, 5.5, 'ember_lit' if state == 'pressed' else 'trim', w=2.6, h=1.3)
    for T in corners(x0, y0, x1, y1):
        S.stud(k, *T((9.5, 9.5)), 3.4, 9.5)
    k.catcher()


@piece('skill_slot_off', 'sprite', w=176, h=176, pad=16)
def skill_slot_off(k, S, m):
    # tintable veil for a skill that cannot be cast: an inner frame line (dull red in the engine)
    x0, y0, x1, y1 = box(m)
    out = S.outline(x0, y0, x1, y1, 13, k)
    k.solid(k.offset(out, 13.0), [(0, 2.0), (0.5, 3.4), (3.0, 3.4), (3.6, 2.0)], 'enamel', cap=False, name="veil")


@piece('socket', 'sprite', w=60, h=60, pad=6, mip=True)
def socket(k, S, m):
    c = 30.0
    ring(k, c, c, 23, [(0, 0), (0, 4), (1.6, 5.6), (4, 5.6), (5.6, 3.6), (7.4, 1.4)], 'bar', n=40)
    k.floor(circle(c, c, 16, 40), 'floor', z=1.4)
    ring(k, c, c, 16.6, [(0, 1.0), (0.5, 2.2), (1.5, 2.2), (2.0, 1.0)], 'trim', n=40, name="hair")
    k.catcher()


@piece('gem_slot', 'sprite', w=96, h=96, pad=12, mip=True, shadow=(3, 1, 2, 0.5))
def gem_slot(k, S, m):
    c = 48.0
    k.solid(k.rhombus(c, c, 37, 37), [(0, 0), (0, 4.5), (2, 6.5), (5, 6.5), (6.5, 5)], 'plate', cap=True, name="seat")
    ring(k, c, c, 25, [(0, 5), (0, 8), (1.6, 9.6), (4, 9.6), (5.6, 7.6), (7.4, 5.4)], 'trim', n=48)
    k.floor(circle(c, c, 18, 48), 'floor', z=5.4)
    for a in range(4):
        x, y = c + 31 * math.cos(a * math.pi / 2), c + 31 * math.sin(a * math.pi / 2)
        S.stud(k, x, y, 3.0, 6.5)
    k.catcher()


# ═════════════════════════════════ paragon nodes (rim iron / rim lit / tintable fill) ═════════════════════════════════
def _pn_shape(k, kind, c, r):
    if kind == 'normal': return circle(c, c, r, 40)
    if kind in ('magic', 'rare'): return k.rhombus(c, c, r * 1.22, r * 1.22)
    if kind == 'legend':
        pts = []
        for i in range(16):
            rr = r * (1.28 if i % 2 == 0 else 0.98)
            a = math.pi / 8 * i
            pts.append((c + rr * math.cos(a), c + rr * math.sin(a)))
        return pts
    if kind == 'socket': return circle(c, c, r * 1.12, 48)
    if kind == 'gate': return k.chamfer_rect(c - r, c - r, c + r, c + r, r * 0.3)
    return circle(c, c, r * 1.0, 48)    # start


PN_KINDS = ('normal', 'magic', 'rare', 'legend', 'socket', 'gate', 'start')


def _pn(kind, part):
    def fn(k, S, m):
        c = m['w'] * 0.5
        r = 34.0
        out = _pn_shape(k, kind, c, r)
        if part == 'fill':
            # neutral enamel body, tinted with the node's colour in the engine
            k.solid(k.offset(out, 7.0), [(0, 2.0), (1.0, 4.4), (4.0, 6.0)], 'enamel', cap=True, name="fill")
            if kind == 'rare':
                k.solid(k.rhombus(c, c, r * 0.55, r * 0.55), [(0, 5.5), (2, 5.0)], 'floor', cap=True, name="core")
                k.solid(k.rhombus(c, c, r * 0.30, r * 0.30), [(0, 5.2), (1.5, 7.0)], 'enamel', cap=True, name="pip")
            return
        mat = 'trim' if part == 'lit' else 'bar'
        k.solid(out, [(0, 0), (0, 4.5), (2, 6.5), (5, 6.5), (6.6, 4.5), (8.5, 2.0)], mat, cap=False, name="rim")
        if kind == 'legend':
            ring(k, c, c, r * 0.66, [(0, 4.0), (0.8, 7.5), (3.2, 7.5), (4.0, 5.0)], mat, n=40, name="inner")
        if kind == 'socket':
            for a in range(4):
                ang = math.pi / 4 + a * math.pi / 2
                S.stud(k, c + (r * 1.12 - 4) * math.cos(ang), c + (r * 1.12 - 4) * math.sin(ang), 4.2, 6.2, 'trim' if part == 'lit' else 'stud')
        if kind == 'gate':
            arch = [(c - r * 0.42, c + r * 0.62), (c - r * 0.42, c - r * 0.1)]
            for i in range(9):
                a = math.pi * (1 - i / 8.0)
                arch.append((c + r * 0.42 * math.cos(a), c - r * 0.1 - r * 0.5 * math.sin(a)))
            arch += [(c + r * 0.42, c + r * 0.62)]
            k.solid(arch, [(0, 2.0), (0, 5.0), (1.2, 6.2), (3.0, 6.2), (4.0, 4.4)], mat, cap=False, name="door")
        if kind == 'start':
            for i in range(8):
                ray(k, (c, c), math.pi / 4 * i + math.pi / 8, r * 0.95, r * 1.36, 0.2, mat, z=4.0)
        k.catcher()
    return fn


for _kind in PN_KINDS:
    piece('pn_%s' % _kind, 'sprite', w=128, h=128, pad=16, mip=True, shadow=(3, 1, 2, 0.5))(_pn(_kind, 'rim'))
    piece('pn_%s_lit' % _kind, 'sprite', w=128, h=128, pad=16, mip=True, shadow=(3, 1, 2, 0.5), glow=(5, 0.45))(_pn(_kind, 'lit'))
    piece('pn_%s_fill' % _kind, 'sprite', w=128, h=128, pad=16, mip=True)(_pn(_kind, 'fill'))


# ═════════════════════════════════ skill-tree node frames ═════════════════════════════════
def _node(shape, state):
    def fn(k, S, m):
        c = m['w'] * 0.5
        R = 72.0                                      # logical half size
        if shape == 'oct': out = k.ngon(c, c, R / math.cos(math.pi / 8), 8, math.pi / 8)
        elif shape == 'hub': out = k.rhombus(c, c, R, R)
        else: out = circle(c, c, R, 72)
        mat = {'locked': 'face_off', 'avail': 'bar', 'owned': 'plate', 'off': 'face_off', 'on': 'plate'}[state]
        k.solid(out, [(0, 0), (0, 6), (2.5, 8.5), (7, 8.5), (9, 6), (11, 5), (13.5, 5), (16, 1.5)], mat, cap=False, name="rim")
        line = {'locked': 'trim_off', 'avail': 'ember_lit', 'owned': 'trim_hi', 'off': 'trim_off', 'on': 'trim_hi'}[state]
        hair(k, out, 10.4, 5.0, line, w=3.0, h=1.5)
        if shape == 'key':
            ring(k, c, c, R + 12, [(0, 0), (0, 2.5), (1.2, 3.8), (3.0, 3.8), (4.2, 2.5), (4.2, 0)], mat, n=72, name="outer")
            for i in range(8):
                a = math.pi / 4 * i
                S.stud(k, c + (R + 10) * math.cos(a), c + (R + 10) * math.sin(a), 4.0, 3.8, 'trim' if state == 'owned' else 'stud')
        if shape == 'oct' and state == 'owned':
            for i in range(4):
                a = math.pi / 2 * i
                S.stud(k, c + (R - 4.8) * math.cos(a), c + (R - 4.8) * math.sin(a), 3.4, 8.5, 'trim')
        k.catcher()
    return fn


for _shape in ('oct', 'round', 'key'):
    for _state in ('locked', 'avail', 'owned'):
        piece('node_%s_%s' % (_shape, _state), 'sprite', w=192, h=192, pad=24, mip=True, shadow=(4, 2, 3, 0.5),
              **({'glow': (6, 0.5)} if _state != 'locked' else {}))(_node(_shape, _state))
for _state in ('off', 'on'):
    piece('node_hub_%s' % _state, 'sprite', w=192, h=192, pad=24, mip=True, shadow=(4, 2, 3, 0.5),
          **({'glow': (6, 0.5)} if _state == 'on' else {}))(_node('hub', _state))


# ═════════════════════════════════ separators and small ornaments ═════════════════════════════════
@piece('rule', 'h3', ml=48, mr=48, px=32, h=16, pad=4, blend=6)
def rule(k, S, m):
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    k.tube([(x0 + 2, cy), (x0 + 44, cy), (x1 - 44, cy), (x1 - 2, cy)], 1.5, 'trim', z=1.4, taper=[0.1, 1.0, 1.0, 0.1])


@piece('rule_gem', 'sprite', w=200, h=40, pad=4, anchor=(100, 20))
def rule_gem(k, S, m):
    cx, cy = m['anchor']
    for s in (-1, 1):
        k.tube([(cx + s * 16, cy), (cx + s * 60, cy), (cx + s * 94, cy)], 1.6, 'trim', z=1.4, taper=[1.0, 0.8, 0.1])
        k.solid(k.rhombus(cx + s * 30, cy, 4.5, 4.5), [(0, 0.5), (1.4, 2.6)], 'trim', cap=True, name="bead")
    S.jewel(k, cx, cy, 7.5, 9.5, 1.5)
    k.catcher()


@piece('header_line', 'h3', ml=40, mr=24, px=32, h=24, pad=4, blend=6)
def header_line(k, S, m):
    # underline of a section header: an ember-diamond bullet, then a line fading to the right
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    k.solid(k.rhombus(x0 + 9, cy, 7, 7), [(0, 0.5), (0, 2), (1.5, 3.4), (3, 3.4)], 'trim', cap=True, name="bullet")
    k.tube([(x0 + 22, cy), (x0 + 38, cy), (x1 - 22, cy), (x1 - 2, cy)], 1.3, 'trim', z=1.2, taper=[0.5, 1.0, 1.0, 0.1])


@variants([('bullet', 'stone'), ('bullet_gold', 'trim')], 'sprite', w=36, h=36, pad=4, mip=True)
def bullet(k, S, m, mat):
    c = 18.0
    k.solid(k.rhombus(c, c, 12, 12), [(0, 0), (0, 2), (1.5, 3.5), (3.5, 3.5), (4.5, 2)], 'trim', cap=True, name="bezel")
    k.gem(k.rhombus(c, c, 7.5, 7.5), 3.6, mat, z=2.0, table=0.4)
    k.catcher()


@piece('corner_orn', 'sprite', w=120, h=120, pad=4)
def corner_orn(k, S, m):
    # a loose gold flame sprig for the corner of a card (drawn in the top-left corner; flip for the others)
    for sw in (False, True):
        def F(p, sw=sw): return (4 + p[1], 4 + p[0]) if sw else (4 + p[0], 4 + p[1])
        S.sprig(k, F, 16.0, 12.0, 84.0, 2.0)
    k.solid(k.rhombus(14, 14, 8, 8), [(0, 0.5), (0, 2.2), (1.6, 3.8), (3.4, 3.8)], 'trim', cap=True, name="knot")
    k.catcher()


@piece('banner', 'h3', ml=72, mr=72, px=64, h=96, pad=12, blend=10, shadow=(5, 2, 3, 0.55))
def banner(k, S, m):
    # section banner: a low plate with spear ends and a stone at each end
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5 - 6, 34)
    plate_rim(k, out, 'face_dn', z=7.0, fz=4.4, rim=6.0)
    hair(k, out, 9.5, 4.4)
    for x in (x0 + 40, x1 - 40):
        S.jewel(k, x, cy, 5.5, 5.5, 5.0, bezel=True, h=2.6)
    k.catcher()


# ═════════════════════════════════ HUD ═════════════════════════════════
@piece('hud_plate', 'nine', ml=104, mt=44, mr=104, mb=44, px=128, py=32, pad=20, blend=12, center=True, shadow=(10, 3, 6, 0.6))
def hud_plate(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 12)
    k.solid(out, [(0, 0), (0, 7), (3, 10), (8, 10), (10, 7), (12, 5.5), (16, 5.5), (18, 3.0)], 'bar', cap=True, name="plate")
    hair(k, out, 12.6, 5.5, w=2.8, h=1.5)
    for x in edge_marks(m, x0 + 60, x1 - 60, m['ml'], m['px'], m['px']):
        S.stud(k, x, y0 + 5.5, 3.2, 10.0); S.stud(k, x, y1 - 5.5, 3.2, 10.0)
    k.catcher()


@piece('globe_rim', 'sprite', w=464, h=464, pad=60, shadow=(8, 3, 5, 0.6))
def globe_rim(k, S, m):
    # ring for a 172 px globe (logical box = the globe's square): iron band, gold inner bead, crown rays, a cradle
    c = m['w'] * 0.5
    Ro = 176.0                                             # outer radius; inner lip at 142 covers the edge of the glass (r = 144.5)
    ring(k, c, c, Ro, [(0, 0), (0, 9), (3, 12), (9, 12), (11, 8), (13, 6.5), (20, 6.5), (22, 8), (24, 13), (29, 13), (33, 5), (34, 0)], 'bar', n=120)
    ring(k, c, c, Ro - 14.2, [(0, 6.0), (0.8, 8.4), (4.2, 8.4), (5.0, 6.0)], 'trim', n=120, name="bead")
    # crown: three rays over the top
    for i in (-1, 0, 1):
        ray(k, (c, c), math.radians(-90 + i * 15.0), Ro - 6, Ro + (18.0 if i == 0 else 11.0), 0.060 if i == 0 else 0.048)
    S.jewel(k, c, c - Ro + 16, 8.5, 10.5, 12.5)
    # side lugs and the cradle underneath
    for a_deg in (0, 180):
        a = math.radians(a_deg)
        x, y = c + (Ro - 16) * math.cos(a), c + (Ro - 16) * math.sin(a)
        k.solid(k.rhombus(x, y, 15, 19), [(0, 11), (0, 14), (2, 16), (5, 16), (6.5, 14)], 'plate', cap=True, name="lug")
        S.stud(k, x, y, 4.6, 16.0, 'trim')
    for s in (-1, 1):
        pts = []
        for i in range(15):
            a = math.radians(90 + s * (16 + i * 4.0))
            rr = Ro + 2 + 13 * math.sin(math.pi * i / 14.0)
            pts.append((c + rr * math.cos(a), c + rr * math.sin(a)))
        inner = []
        for i in range(15):
            a = math.radians(90 + s * (16 + (14 - i) * 4.0))
            inner.append((c + (Ro - 8) * math.cos(a), c + (Ro - 8) * math.sin(a)))
        k.solid(pts + inner, [(0, 0), (0, 12), (2.5, 14.5), (5, 14.5)], 'plate', cap=True, name="claw")
    k.solid(k.rhombus(c, c + Ro - 4, 30, 22), [(0, 0), (0, 14), (3, 17), (7, 17), (9, 15)], 'plate', cap=True, name="foot")
    S.stud(k, c, c + Ro - 4, 6.0, 16.5, 'trim')
    k.catcher()


@piece('xp_frame', 'h3', ml=48, mr=48, px=64, h=44, pad=8, blend=8, shadow=(4, 1, 2, 0.5))
def xp_frame(k, S, m):
    # trough frame for the experience bar: the bar itself is drawn by the game inside the hole
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 14)
    k.solid(out, [(0, 0), (0, 4), (1.6, 5.6), (4.0, 5.6), (5.4, 3.6), (6.6, 1.4)], 'bar', cap=False, name="rim")
    for x in (x0 + 16, x1 - 16):
        k.solid(k.rhombus(x, cy, 9, 9), [(0, 2), (0, 5), (1.6, 6.6), (3.4, 6.6)], 'trim', cap=True, name="cap")
    for x in edge_marks(m, x0 + 40, x1 - 40, m['ml'], m['px'], m['px']):
        k.solid(k.rect(x - 1.2, y0 + 1, x + 1.2, y1 - 1), [(0, 3.0), (0.6, 5.8)], 'bar', cap=True, name="tick")
    k.catcher()


@piece('buff_frame', 'sprite', w=112, h=112, pad=8, shadow=(3, 1, 2, 0.5))
def buff_frame(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 9)
    k.solid(out, [(0, 0), (0, 3.6), (1.6, 5.2), (4.0, 5.2), (5.4, 3.4), (7.0, 1.2)], 'bar', cap=False, name="rim")
    hair(k, out, 6.0, 1.2, 'trim', w=1.8, h=1.0)
    k.catcher()


@piece('boss_frame', 'h3', ml=150, mr=150, px=128, h=120, pad=20, blend=12, shadow=(8, 2, 4, 0.6))
def boss_frame(k, S, m):
    # health-bar frame of a boss: heavy trough with spear ends and stones; the bars are drawn by the game in the hole
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 30)
    k.solid(out, [(0, 0), (0, 7), (3, 10), (7, 10), (9, 7), (10.5, 6), (13.5, 6), (15, 9), (17.5, 9), (20, 2.5)], 'bar', cap=False, name="rim")
    hair(k, out, 10.7, 6.0, w=2.6, h=1.3)
    for s, x in ((1, x0 + 44), (-1, x1 - 44)):
        k.solid(k.rhombus(x, cy, 34, 38), [(0, 0), (0, 12), (3, 15), (7, 15), (9, 13)], 'plate', cap=True, name="end")
        S.jewel(k, x, cy, 10.5, 12.5, 13.0)
    for x in edge_marks(m, x0 + 150, x1 - 150, m['ml'], m['px'], m['px']):
        S.stud(k, x, y0 + 5.0, 3.0, 10.0); S.stud(k, x, y1 - 5.0, 3.0, 10.0)
    k.catcher()


@piece('target_frame', 'h3', ml=36, mr=36, px=64, h=56, pad=6, blend=6, shadow=(4, 1, 2, 0.5))
def target_frame(k, S, m):
    # health-bar frame of the enemy under the pointer (12 px bar in the hole)
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 16)
    k.solid(out, [(0, 0), (0, 4.5), (1.8, 6.3), (5, 6.3), (6.5, 4.5), (8, 3.8), (8.6, 3.8), (10, 1.2)], 'bar', cap=False, name="rim")
    for x in (x0 + 15, x1 - 15):
        k.solid(k.rhombus(x, cy, 10, 12), [(0, 2), (0, 5.5), (1.6, 7.1), (3.6, 7.1)], 'trim', cap=True, name="cap")
    k.catcher()


@piece('boss_stagger', 'h3', ml=28, mr=28, px=64, h=36, pad=6, blend=6)
def boss_stagger(k, S, m):
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 10)
    k.solid(out, [(0, 0), (0, 3), (1.4, 4.4), (3.4, 4.4), (4.6, 2.8), (5.8, 1.0)], 'bar', cap=False, name="rim")
    k.catcher()


@piece('plate_bar', 'h3', ml=20, mr=20, px=32, h=32, pad=4, blend=6)
def plate_bar(k, S, m):
    # enemy health-bar frame (the fill is drawn by the game inside)
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 8)
    k.solid(out, [(0, 0), (0, 2.6), (1.2, 3.8), (3.0, 3.8), (4.2, 2.4), (5.2, 0.8)], 'bar', cap=False, name="rim")
    k.catcher()


@piece('minimap_ring', 'sprite', w=576, h=576, pad=52, shadow=(8, 3, 5, 0.55))
def minimap_ring(k, S, m):
    c = m['w'] * 0.5
    Ro = 240.0                                             # inner lip at 210 covers the edge of the map disc (r = 212.4)
    ring(k, c, c, Ro, [(0, 0), (0, 8), (3, 11), (8, 11), (10, 7), (11.5, 6), (17.5, 6), (19, 7), (21, 12), (25, 12), (29, 4), (30, 0)], 'bar', n=144)
    ring(k, c, c, Ro - 12.4, [(0, 5.4), (0.8, 7.8), (3.6, 7.8), (4.4, 5.4)], 'trim', n=144, name="bead")
    # north: a beacon flame; east / south / west: small stones
    k.solid([(c - 15, c - Ro + 8), (c, c - Ro - 34), (c + 15, c - Ro + 8), (c, c - Ro + 22)], [(0, 0), (0, 12), (2.5, 14.5), (5, 14.5)], 'plate', cap=True, name="north")
    S.jewel(k, c, c - Ro + 1, 6.5, 12.0, 14.0, bezel=False)
    for a_deg in (0, 90, 180):
        a = math.radians(a_deg)
        x, y = c + (Ro - 15) * math.cos(a), c + (Ro - 15) * math.sin(a)
        k.solid(k.rhombus(x, y, 15, 15), [(0, 10), (0, 13), (2, 15), (5, 15), (6.5, 13)], 'plate', cap=True, name="lug")
        S.stud(k, x, y, 4.4, 15.0, 'trim')
    for a_deg in range(15, 360, 30):
        a = math.radians(a_deg)
        S.stud(k, c + (Ro - 23.2) * math.cos(a), c + (Ro - 23.2) * math.sin(a), 2.8, 12.0)
    k.catcher()


@piece('keycap', 'nine', ml=22, mt=22, mr=22, mb=22, px=16, py=16, pad=6, blend=4, center=True, shadow=(3, 1, 2, 0.5))
def keycap(k, S, m):
    x0, y0, x1, y1 = box(m)
    out = k.round_rect(x0, y0, x1, y1, 6, 3)
    k.solid(out, [(0, 0), (0, 3), (1.5, 5), (3.5, 6), (5, 6.2)], 'face_hi', cap=True, name="key")
    k.catcher()


# ═════════════════════════════════ main menu ═════════════════════════════════
@variants([('menu_plate', False), ('menu_plate_hover', True)], 'h3', ml=92, mr=92, px=64, h=144, pad=16, blend=8, shadow=(7, 2, 4, 0.55))
def menu_plate(k, S, m, hot):
    x0, y0, x1, y1 = box(m)
    cy = (y0 + y1) * 0.5
    out = spear(x0, x1, cy, (y1 - y0) * 0.5, 30)
    plate_rim(k, out, 'face_hi' if hot else 'face', z=8.0, fz=5.0, rim=6.5)
    hair(k, out, 9.6, 5.0, 'trim_hi' if hot else 'trim', w=2.6, h=1.4)
    for x in (x0 + 38, x1 - 38):
        S.jewel(k, x, cy, 6.5, 6.5, 5.6, bezel=True, mat='stone' if hot else 'trim', h=3.0)
    k.catcher()


@variants([('card', False), ('card_on', True)], 'nine', ml=64, mt=64, mr=64, mb=64, px=128, py=128, pad=16, blend=12, shadow=(8, 2, 4, 0.5))
def card(k, S, m, on):
    # save-slot / choice card: the sub-panel moulding; chosen = its inlay is lit
    frame(k, S, m, S.sub_bar, S.sub_inlay or (6.8, 2.6, 3.5, 1.4), False, inl_mat='trim_hi' if on else 'trim')
    k.catcher()


@piece('emblem', 'sprite', w=1280, h=440, pad=20, anchor=(640, 220), shadow=(10, 4, 6, 0.5))
def emblem(k, S, m):
    # backing of the title lettering: the dead Sun-Forge as a ring of rays, with two long wings
    cx, cy = m['anchor']
    R = 150.0
    ring(k, cx, cy, R, [(0, 0), (0, 9), (3, 12), (9, 12), (11, 8), (13, 6.5), (20, 6.5), (22, 8), (24, 13), (29, 13), (33, 5), (34, 0)], 'bar', n=144)
    ring(k, cx, cy, R - 14.2, [(0, 6.0), (0.8, 8.4), (4.2, 8.4), (5.0, 6.0)], 'trim', n=144, name="bead")
    for i in range(24):
        a = math.radians(i * 15.0)
        if abs(math.cos(a)) > 0.93: continue                # the wings take the sides
        ray(k, (cx, cy), a, R - 6, R + (40.0 if i % 2 == 0 else 24.0), 0.062 if i % 2 == 0 else 0.048)
    for s in (-1, 1):
        out = spear(cx + (R - 10), cx + 600, cy, 17, 60) if s > 0 else spear(cx - 600, cx - (R - 10), cy, 17, 60)
        k.solid(out, [(0, 0), (0, 10), (2.5, 12.5), (6, 12.5), (7.5, 11)], 'plate', cap=True, name="wing")
        S.sprig(k, lambda p, s=s: (cx + s * p[0], p[1]), R + 40.0, cy, 170.0, 12.0)
        S.sprig(k, lambda p, s=s: (cx + s * p[0], 2 * cy - p[1]), R + 230.0, cy, 150.0, 12.0)
        for dx in (R + 218, R + 400):
            S.jewel(k, cx + s * dx, cy, 7, 7, 11.5, bezel=True, h=3.2)
    k.catcher()


EXPLORE = ['window', 'crest', 'tooltip', 'tooltip_band', 'btn', 'slot', 'slot_rarity']


@piece('boxline', 'nine', ml=20, mt=20, mr=20, mb=20, px=16, py=16, pad=4, blend=4)
def boxline(k, S, m):
    # neutral bevelled line frame, tinted in the engine (selection, state and rarity borders of plain boxes)
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 4)
    k.solid(out, [(0, 0), (0, 2.2), (1.2, 3.4), (2.8, 3.4), (4.0, 2.2), (5.0, 0.6)], 'enamel', cap=False, name="line")
    k.catcher()
