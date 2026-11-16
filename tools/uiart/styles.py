"""Style directions of the UI chrome. A style = material roles + moulding profiles + ornament motifs.
Pieces (pieces.py) only talk to these hooks, so switching `--style` re-dresses the whole set.

Roles: bar (frame mouldings), plate (castings, button faces), trim (inlay lines, filigree), stud (rivets),
       stone (ember stones), floor (recess floors), band (tintable enamel).
All sizes are texture px of the 2x sheet.
"""
import math


class Style:
    key = "?"
    title = "?"
    field = "slate"            # tile used for panel fields (slice.py builds it)
    field_tip = "soot"
    # moulding cross-sections (inset, z) from the outer edge inward
    win_bar = [(0, 0), (0, 7), (3, 10), (8, 10), (10, 6), (11, 5), (20, 5), (21, 6), (23, 12), (28, 12), (33, 4), (34, 0)]
    win_inlay = (12.5, 6.5, 5.0, 2.2)      # inset, width, floor z, height
    sub_bar = [(0, 0), (0, 4), (2, 6), (5, 6), (6, 3.5), (10, 3.5), (11, 7), (14, 7), (17, 2), (18, 0)]
    sub_inlay = (6.8, 2.6, 3.5, 1.4)
    tip_bar = [(0, 0), (0, 4), (2, 6), (6, 6), (7, 3.5), (10.5, 3.5), (11.5, 6.5), (13.5, 6.5), (16, 2), (16, 0)]
    tip_inlay = (7.6, 2.4, 3.5, 1.3)

    def mats(self, k):
        raise NotImplementedError

    # ── motifs (each draws in a local corner frame: u along the horizontal edge, v along the vertical edge) ──
    def corner(self, k, T, bar_w, heavy=True, idx=0):
        pass

    def crest(self, k, cx, y, bar_w):
        pass

    def stud(self, k, x, y, r, z, mat='stud'):
        return k.pyramid(x, y, r, r * 0.55, mat, z=z)

    def finish(self, k):
        """State materials every style needs; a style may define its own before this fills the gaps."""
        M = k.MATS
        M.setdefault('face_hi', M['face']); M.setdefault('face_dn', M['face']); M.setdefault('face_off', M['face']); M.setdefault('face_on', M['face'])
        if 'trim_hi' not in M: M['trim_hi'] = k.mat_glow(self.key + '_trim_hi', (1.0, 0.66, 0.26), 1.15)
        if 'trim_off' not in M: M['trim_off'] = k.mat_metal(self.key + '_trim_off', base=(0.16, 0.13, 0.10), rough=(0.45, 0.65), edge_amt=0.3, soot=0.6, hammer=0.2, bump=0.4)
        if 'ember_lit' not in M: M['ember_lit'] = k.mat_glow(self.key + '_ember_lit', (1.0, 0.36, 0.07), 1.5)
        if 'white_lit' not in M: M['white_lit'] = k.mat_glow(self.key + '_white_lit', (1.0, 1.0, 1.0), 1.2)
        if 'enamel' not in M: M['enamel'] = k.mat_enamel(self.key + '_enamel', (0.86, 0.86, 0.86), 0.25)
        M.setdefault('band', M['enamel'])

    def jewel(self, k, cx, cy, rx, ry, z, bezel=True, h=None, mat='stone'):
        """The style's inlaid stone with its setting."""
        if bezel:
            k.solid(k.rhombus(cx, cy, rx + 4.5, ry + 4.5), [(0, z - 2.5), (0, z + 1.0), (1.6, z + 2.6), (3.6, z + 2.6), (4.6, z)], 'trim', cap=True, name="bezel")
        k.gem(k.rhombus(cx, cy, rx, ry), h if h is not None else min(rx, ry) * 0.5, mat, z=z, table=0.42)

    def sprig(self, k, F, u0, vm, ln, z):
        k.tube([F(p) for p in k.bez((u0, vm + 4), (u0 + ln * 0.3, vm - 10), (u0 + ln * 0.6, vm + 10), (u0 + ln, vm), 24)], 2.6, 'trim', z=z, taper='out')

    def crest_small(self, k, cx, y, bar_w):
        """Compact top ornament for dialogs: a stone on a small plate with two short wings."""
        b = bar_w
        mid = y + b * 0.5
        for s in (-1, 1):
            out = [(cx + s * 16, y - 2), (cx + s * 70, y - 2), (cx + s * 88, mid), (cx + s * 70, y + b + 2), (cx + s * 16, y + b + 2)]
            if s < 0: out = out[::-1]
            k.solid(out, [(0, 0), (0, 12.5), (2.5, 15), (5.5, 15), (7, 13.5)], 'plate', cap=True, name="wing")
            self.sprig(k, lambda p, s=s: (cx + s * p[0], p[1]), 34.0, mid, 46.0, 14.2)
        k.solid(k.rhombus(cx, mid - 2, 30, 34), [(0, 0), (0, 14.5), (3, 17.5), (7, 17.5), (9, 15.5)], 'plate', cap=True, name="boss")
        self.jewel(k, cx, mid - 2, 12.5, 16.5, 15.5)

    # ── periodic detail on the straight runs of a heavy frame (phase-locked to the edge tile) ──
    corner_reach = 136.0       # how far a heavy corner casting reaches along the edge (from the outer corner)

    def edge(self, k, m, R, bw, heavy=True):
        pass

    def _runs(self, m):
        """(positions along x for the top/bottom edges, positions along y for the side edges) once per period."""
        from pieces import edge_marks
        p = m['pad']
        xs = edge_marks(m, p + self.corner_reach, m['w'] - p - self.corner_reach, m['ml'], m['px'], m['px'])
        ys = edge_marks(m, p + self.corner_reach, m['h'] - p - self.corner_reach, m['mt'], m['py'], m['py'])
        return xs, ys

    # ── small widgets ──
    btn_cut = 7.0
    slot_cut = 9.0

    def outline(self, x0, y0, x1, y1, c, k):
        return k.chamfer_rect(x0, y0, x1, y1, c)

    def button(self, k, m, state='normal'):
        p = m['pad']
        out = self.outline(p, p, m['w'] - p, m['h'] - p, self.btn_cut, k)
        down = state == 'pressed'
        fz = 3.0 if down else 4.8
        face = {'normal': 'face', 'hover': 'face_hi', 'pressed': 'face_dn', 'disabled': 'face_off', 'focus': 'face'}.get(state, 'face')
        k.solid(out, [(0, 0), (0, 5), (2, 7), (5, 7), (6.5, 5.2), (8, fz)], face if face in k.MATS else 'face', cap=True, name="btn")
        if state != 'disabled':
            trim = 'trim_hi' if state in ('hover', 'focus') and 'trim_hi' in k.MATS else 'trim'
            k.solid(k.offset(out, 9.2), [(0, fz - 0.4), (0.5, fz + 1.0), (1.9, fz + 1.0), (2.4, fz - 0.4)], trim, cap=False, name="hair")

    def slot(self, k, m):
        p = m['pad']
        out = self.outline(p, p, m['w'] - p, m['h'] - p, self.slot_cut, k)
        k.solid(out, [(0, 0), (0, 4.5), (1.6, 6), (4.2, 6), (5.6, 4), (8, 1.6)], 'bar', cap=False, name="rim")
        k.solid(k.offset(out, 7.6), [(0, 1.8)], 'floor', cap=True, name="floor")
        k.solid(k.offset(out, 6.8), [(0, 1.5), (0.4, 2.7), (1.6, 2.7), (2.0, 1.5)], 'trim', cap=False, name="hair")

    def slot_inner(self, k, m):
        p = m['pad']
        return k.offset(self.outline(p, p, m['w'] - p, m['h'] - p, self.slot_cut, k), 9.6)


def _T(cx, cy, sx, sy):
    """Local corner frame -> image px. (u, v) = distance from the corner along the horizontal / vertical edge."""
    def f(p):
        if isinstance(p, (list,)):
            q = [f(a) for a in p]
            return q if sx * sy > 0 else q[::-1]
        return (cx + sx * p[0], cy + sy * p[1]) + tuple(p[2:])
    return f


def corners(x0, y0, x1, y1):
    return [_T(x0, y0, 1, 1), _T(x1, y0, -1, 1), _T(x1, y1, -1, -1), _T(x0, y1, 1, -1)]


def flame(k, T, pts_fn, r, mat, z):
    """A tapering gold tongue along a list of local points."""
    k.tube([T(p) for p in pts_fn], r, mat, z=z, taper='out')


# ═════════════════════════════════ A: forged iron, ember-gold inlay ═════════════════════════════════
class StyleA(Style):
    key = "A"
    title = "forged iron with ember-gold inlay"
    field = "slate"
    field_tip = "soot"
    corner_reach = 166.0

    def mats(self, k):
        M = k.MATS
        M['bar'] = k.mat_metal('A_bar', metal=0.85, base=(0.034, 0.033, 0.038), rough=(0.32, 0.58), edge=(0.62, 0.58, 0.52), edge_amt=0.9, dark=(0.016, 0.013, 0.012), soot=0.8, hammer=1.0)
        M['plate'] = k.mat_metal('A_plate', metal=0.85, base=(0.046, 0.044, 0.048), rough=(0.30, 0.54), edge=(0.70, 0.66, 0.60), edge_amt=1.0, dark=(0.02, 0.016, 0.014), soot=0.8, hammer=0.9)
        M['face'] = k.mat_metal('A_face', metal=0.4, spec=0.25, base=(0.040, 0.037, 0.038), rough=(0.44, 0.68), edge=(0.6, 0.56, 0.5), edge_amt=0.7, dark=(0.03, 0.026, 0.024), soot=0.6, hammer=0.45)
        M['face_hi'] = k.mat_metal('A_face_hi', metal=0.45, spec=0.3, base=(0.075, 0.060, 0.048), rough=(0.40, 0.62), edge=(0.75, 0.66, 0.52), edge_amt=0.8, dark=(0.045, 0.034, 0.026), soot=0.5, hammer=0.45)
        M['face_dn'] = k.mat_metal('A_face_dn', metal=0.35, spec=0.2, base=(0.022, 0.021, 0.023), rough=(0.5, 0.72), edge=(0.45, 0.42, 0.38), edge_amt=0.6, dark=(0.014, 0.012, 0.012), soot=0.7, hammer=0.45)
        M['face_off'] = k.mat_metal('A_face_off', metal=0.3, spec=0.15, base=(0.024, 0.023, 0.024), rough=(0.62, 0.82), edge=(0.22, 0.21, 0.20), edge_amt=0.3, dark=(0.016, 0.015, 0.015), soot=0.7, hammer=0.4)
        M['face_on'] = k.mat_metal('A_face_on', metal=0.5, spec=0.3, base=(0.115, 0.046, 0.016), rough=(0.45, 0.66), edge=(1.0, 0.70, 0.36), edge_amt=0.7, dark=(0.055, 0.02, 0.008), soot=0.35, hammer=0.45)
        M['trim'] = k.mat_metal('A_trim', base=(0.86, 0.50, 0.15), rough=(0.22, 0.40), edge=(1.0, 0.86, 0.55), edge_amt=0.6, dark=(0.36, 0.17, 0.04),
                                soot=0.55, hammer=0.25, scan_set='Metal038', bump=0.5)
        M['stud'] = k.mat_metal('A_stud', base=(0.10, 0.095, 0.095), rough=(0.28, 0.5), edge=(0.8, 0.76, 0.7), edge_amt=1.0, soot=0.7, hammer=0.3)
        M['stone'] = k.mat_ember('A_stone', strength=5.0)
        M['floor'] = k.mat_field('A_floor', color=(0.0038, 0.0035, 0.0038), rough=0.92, spec=0.035)

    def sprig(self, k, F, u0, vm, ln, z, amp=9.0):
        """Flame sprig: F maps (u, v) to image px; root at u0, centre line v = vm, length ln. A long tongue with two
        leaflets licking off it (no closed loops: they read as eyes at small sizes)."""
        def pts(seq): return [F(p) for p in seq]
        a = amp
        k.tube(pts(k.bez((u0, vm + a * 0.45), (u0 + ln * 0.30, vm - a * 1.25), (u0 + ln * 0.62, vm + a * 1.15), (u0 + ln, vm - 1), 32)), 3.3, 'trim', z=z, taper='out')
        k.tube(pts(k.bez((u0 + ln * 0.14, vm - a * 0.05), (u0 + ln * 0.24, vm - a * 1.15), (u0 + ln * 0.40, vm - a * 1.2), (u0 + ln * 0.50, vm - a * 0.55), 20)), 2.3, 'trim', z=z - 0.15, taper='out')
        k.tube(pts(k.bez((u0 + ln * 0.36, vm + a * 0.35), (u0 + ln * 0.46, vm + a * 1.2), (u0 + ln * 0.62, vm + a * 1.25), (u0 + ln * 0.74, vm + a * 0.6), 20)), 2.1, 'trim', z=z - 0.15, taper='out')
        k.tube(pts(k.bez((u0 + 1, vm + a * 0.5), (u0 + ln * 0.08, vm + a * 1.3), (u0 + ln * 0.2, vm + a * 1.25), (u0 + ln * 0.27, vm + a * 0.8), 16)), 1.8, 'trim', z=z - 0.2, taper='out')
        bx, by = F((u0 - 1.5, vm + a * 0.45))
        k.dome(bx, by, 4.6, 3.2, 'trim', z=z - 1.2)

    def corner(self, k, T, bar_w, heavy=True, idx=0):
        b = bar_w
        if heavy:
            L, e, tip = 128.0, 3.0, 26.0                    # arm length, overhang, spear tip
            mid = b * 0.5
            c = b + e
            # L bracket with spear tips; in the angle a pointed leaf reaches a little into the field
            out = [(-e, -e), (L, -e), (L + tip, mid), (L, c), (c + 30, c), (c + 17, c + 5), (c + 17, c + 17), (c + 5, c + 17), (c, c + 30), (c, L), (mid, L + tip), (-e, L)]
            k.solid(T(out), [(0, 0), (0, 12.5), (2.5, 15), (5.5, 15), (7, 13.5)], 'plate', cap=True, name="bracket")
            for sw in (False, True):
                def S(p, sw=sw): return (p[1], p[0]) if sw else p
                self.sprig(k, lambda p, S=S: T(S(p)), 50.0, mid, L + 10.0 - 50.0, 14.2, amp=8.0)
                self.stud(k, *T(S((L + 4, mid))), 4.2, 15.0)
            # corner boss: raised rhombic seat, gold bezel, ember stone
            cx, cy = T((mid + 5, mid + 5))
            k.solid(k.rhombus(cx, cy, 24, 24), [(0, 13), (0, 16.5), (2.5, 19), (6, 19), (7.5, 17.5)], 'plate', cap=True, name="seat")
            self.jewel(k, cx, cy, 10.5, 10.5, 18.0)
            lx, ly = T((c + 12.5, c + 12.5))
            self.stud(k, lx, ly, 3.4, 15.0, 'trim')
        else:
            # light corner: a clipped square cap with one stud (thin mouldings) or a gold boss (window moulding)
            s = b + 8.0
            out = [(-2, -2), (s, -2), (s + 6, b * 0.5), (s, b + 2), (b + 2, b + 2), (b + 2, s), (b * 0.5, s + 6), (-2, s)]
            if b < 30:
                k.solid(T(out), [(0, 0), (0, 7), (1.6, 8.6), (3.6, 8.6), (4.6, 7.6)], 'plate', cap=True, name="cap")
                self.stud(k, *T((b * 0.5, b * 0.5)), 3.6, 8.6, 'trim')
            else:
                k.solid(T(out), [(0, 0), (0, 12.5), (2.5, 15), (5.5, 15), (7, 13.5)], 'plate', cap=True, name="cap")
                cx, cy = T((b * 0.5 + 1, b * 0.5 + 1))
                k.solid(k.rhombus(cx, cy, 13, 13), [(0, 13.5), (0, 15.5), (1.8, 17.3), (4, 17.3), (5, 16)], 'trim', cap=True, name="boss")

    def edge(self, k, m, R, bw, heavy=True):
        xs, ys = self._runs(m)
        p = m['pad']
        d = 25.5
        for x in xs:
            self.stud(k, x, p + d, 4.0, 12.0); self.stud(k, x, m['h'] - p - d, 4.0, 12.0)
        for y in ys:
            self.stud(k, p + d, y, 4.0, 12.0); self.stud(k, m['w'] - p - d, y, 4.0, 12.0)

    def crest(self, k, cx, y, bar_w):
        """Sun-Forge crest: the dead sun with an ember heart, rising over two spear wings. (cx, y) = middle of the
        frame's outer top edge."""
        from pieces import ray
        b = bar_w
        mid = y + b * 0.5
        w0, w1, tip = 44.0, 214.0, 30.0
        for s in (-1, 1):
            out = [(cx + s * w0, y - 8), (cx + s * (w0 + 66), y - 8), (cx + s * (w0 + 80), y - 3), (cx + s * w1, y - 3), (cx + s * (w1 + tip), mid),
                   (cx + s * w1, y + b + 3), (cx + s * (w0 + 80), y + b + 3), (cx + s * (w0 + 66), y + b + 8), (cx + s * w0, y + b + 8)]
            if s < 0: out = out[::-1]
            k.solid(out, [(0, 0), (0, 12.5), (2.5, 15), (5.5, 15), (7, 13.5)], 'plate', cap=True, name="wing")
            self.sprig(k, lambda p, s=s: (cx + s * p[0], p[1]), w0 + 28.0, mid, w1 + 12.0 - (w0 + 28.0), 14.2, amp=9.0)
            self.stud(k, cx + s * (w1 + 4), mid, 4.4, 15.0)
        # pendant under the disc
        k.solid([(cx - 16, y + b + 2), (cx + 16, y + b + 2), (cx, y + b + 20)], [(0, 0), (0, 10), (2.2, 12.5)], 'plate', cap=True, name="drop")
        # rays of the dead sun: a fan over the top
        cy = y + 15.0
        R = 53.0
        for i in range(-4, 5):
            a = math.radians(-90 + i * 21.0)
            ray(k, (cx, cy), a, R - 6, R + (32.0 if i % 2 == 0 else 20.0), 0.115 if i % 2 == 0 else 0.09, 'plate', z=9.0)
        # disc: iron rim, gold ring, soot well, ember rhombus in a gold bezel, four gold beads
        k.solid(k.ngon(cx, cy, R, 72), [(0, 0), (0, 15), (3, 18), (9, 18), (11, 15.5)], 'plate', cap=True, name="disc")
        k.solid(k.ngon(cx, cy, R - 12, 72), [(0, 15), (0.8, 17.6), (4.0, 17.6), (4.8, 15.2)], 'trim', cap=False, name="ring")
        k.solid(k.ngon(cx, cy, R - 18.5, 72), [(0, 15.2), (2.5, 12.5)], 'floor', cap=True, name="well")
        self.jewel(k, cx, cy, 16.0, 22.5, 14.5, h=8.0)
        for i in range(4):
            a = math.pi / 4 + i * math.pi / 2
            k.dome(cx + 24.5 * math.cos(a), cy + 24.5 * math.sin(a), 3.2, 2.4, 'trim', z=12.6)


# ═════════════════════════════════ B: ash stone, tarnished bronze, candle wax ═════════════════════════════════
class StyleB(Style):
    key = "B"
    title = "ash-grey stone and tarnished bronze, candle wax"
    field = "parchment"
    field_tip = "parchment_dark"
    win_bar = [(0, 0), (0, 8), (4, 12), (11, 12), (14, 9.5), (17, 7.5), (21, 7.5), (24, 9.5), (27, 13), (32, 13), (38, 5), (39, 0)]
    win_inlay = (17.3, 3.4, 7.5, 1.8)
    sub_bar = [(0, 0), (0, 5), (2.5, 7.5), (7, 7.5), (9, 5), (12, 5), (14, 8), (17, 8), (20, 2.5), (21, 0)]
    sub_inlay = (9.3, 2.4, 5.0, 1.3)
    tip_bar = [(0, 0), (0, 4.5), (2.5, 7), (7, 7), (8.5, 4.5), (11, 4.5), (12.5, 7), (15, 7), (18, 2), (18, 0)]
    tip_inlay = (8.8, 2.0, 4.5, 1.2)

    def mats(self, k):
        k.MATS['bar'] = k.mat_stone('B_bar', tint=(0.115, 0.11, 0.107), scan_set='dark_rock', chip=(0.36, 0.35, 0.33))
        k.MATS['plate'] = k.mat_metal('B_plate', base=(0.36, 0.22, 0.10), rough=(0.38, 0.62), edge=(0.95, 0.70, 0.38), edge_amt=0.8, dark=(0.10, 0.06, 0.03),
                                      soot=0.35, hammer=0.5, patina=(0.10, 0.30, 0.25), patina_amt=0.45, scan_set='Metal038')
        k.MATS['face'] = k.mat_stone('B_face', tint=(0.062, 0.06, 0.058), scan_set='dark_rock', dust=0.2, chip=(0.26, 0.25, 0.24))
        k.MATS['trim'] = k.mat_metal('B_trim', base=(0.50, 0.31, 0.13), rough=(0.32, 0.5), edge=(1.0, 0.78, 0.42), edge_amt=0.7, dark=(0.14, 0.08, 0.03),
                                     soot=0.3, hammer=0.2, patina=(0.10, 0.30, 0.25), patina_amt=0.3, bump=0.5)
        k.MATS['stud'] = k.MATS['plate']
        k.MATS['stone'] = k.mat_ember('B_stone', color=(1.0, 0.45, 0.10), strength=2.6)
        k.MATS['floor'] = k.mat_field('B_floor', color=(0.016, 0.015, 0.014))
        k.MATS['band'] = k.mat_enamel('B_band')
        k.MATS['wax'] = k.mat_wax('B_wax')

    def stud(self, k, x, y, r, z, mat='stud'):
        return k.dome(x, y, r, r * 0.6, mat, z=z)

    corner_reach = 140.0
    btn_cut = 5.0

    def outline(self, x0, y0, x1, y1, c, k):
        return k.round_rect(x0, y0, x1, y1, c + 3.0, 4)

    def edge(self, k, m, R, bw, heavy=True):
        # a bronze bead in the cove once per period
        xs, ys = self._runs(m)
        p = m['pad']
        d = 19.0
        for x in xs:
            k.dome(x, p + d, 3.0, 2.0, 'trim', z=7.2); k.dome(x, m['h'] - p - d, 3.0, 2.0, 'trim', z=7.2)
        for y in ys:
            k.dome(p + d, y, 3.0, 2.0, 'trim', z=7.2); k.dome(m['w'] - p - d, y, 3.0, 2.0, 'trim', z=7.2)

    def _drip(self, k, x, y, w, ln, z):
        """A run of candle wax: rounded head hanging down from (x, y)."""
        pts = []
        n = 14
        for i in range(n + 1):                       # down the left side, round the drop, up the right side
            t = i / n
            a = math.pi * (1.0 - t)                  # pi .. 0
            bulge = 1.0 + 0.35 * math.sin(math.pi * t) ** 6
            pts.append((x + math.cos(a) * w * 0.5 * (0.75 + 0.25 * bulge), y + ln - w * 0.5 + math.sin(a) * w * 0.5))
        out = [(x - w * 0.42, y)] + pts + [(x + w * 0.42, y)]
        return k.solid(out, [(0, z), (0.6, z + w * 0.25), (w * 0.22, z + w * 0.42), (w * 0.4, z + w * 0.46)], 'wax', cap=True, smooth=True, name="drip")

    def corner(self, k, T, bar_w, heavy=True, idx=0):
        b = bar_w
        if heavy:
            s = b + 30.0
            out = [(-3, -3), (s + 22, -3), (s + 30, 6), (s + 14, b + 3), (s, b + 3), (s - 8, b + 10), (b + 10, s - 8), (b + 3, s), (b + 3, s + 14), (6, s + 30), (-3, s + 22)]
            k.solid(T(out), [(0, 0), (0, 13.5), (2.5, 16), (6, 16), (7.5, 14)], 'plate', cap=True, name="cap")
            cx, cy = T((b * 0.5 + 9, b * 0.5 + 9))
            # eight-petal rosette
            for i in range(8):
                a = math.radians(i * 45.0)
                px, py = cx + 13 * math.cos(a), cy + 13 * math.sin(a)
                k.dome(px, py, 5.2, 3.2, 'trim', z=14.0)
            k.dome(cx, cy, 8.0, 5.5, 'trim', z=14.0)
            self.stud(k, *T((s + 12, b * 0.5 - 2)), 4.2, 15.5)
            self.stud(k, *T((b * 0.5 - 2, s + 12)), 4.2, 15.5)
            if idx in (0, 1):
                # candle wax has run over the top moulding beside the cap
                for u, ln, w in ((s + 44, 30.0, 8.0), (s + 56, 17.0, 5.6), (s + 34, 12.0, 4.8)):
                    x, y = T((u, 8.0))
                    self._drip(k, x, y, w, ln + b * 0.45, 9.5)
        else:
            s = b + 10.0
            out = [(-2, -2), (s, -2), (s, b * 0.6), (b + 2, b + 2), (b * 0.6, s), (-2, s)]
            k.solid(T(out), [(0, 0), (0, 8), (1.6, 9.6), (3.6, 9.6), (4.6, 8.4)], 'plate', cap=True, name="cap")
            self.stud(k, *T((b * 0.5, b * 0.5)), 3.4, 9.4, 'trim')

    def crest(self, k, cx, y, bar_w):
        """Three votive candles in a bronze sconce; wax has run down over the moulding."""
        b = bar_w
        base_y = y + b * 0.5
        # sconce bar
        out = k.chamfer_rect(cx - 120, y - 4, cx + 120, y + b + 4, 10)
        k.solid(out, [(0, 0), (0, 14), (2.5, 16.5), (6, 16.5), (7.5, 14.5)], 'plate', cap=True, name="sconce")
        for s in (-1, 1):
            self.stud(k, cx + s * 104, base_y, 4.4, 16.0)
            k.tube(k.spiral(cx + s * 78, base_y, 13, 3.0, math.radians(180 if s > 0 else 0), math.radians(-180 if s > 0 else 360), 34), 2.2, 'trim', z=15.6, taper='out')
        # candles: wax pillars in relief, the middle one taller, each with a small ember flame stone above
        for dx, hgt, w in ((-46, 64, 21), (0, 84, 25), (46, 56, 21)):
            x = cx + dx
            top = y + b + 2 - hgt - 6
            body = k.round_rect(x - w * 0.5, top, x + w * 0.5, y + b + 2, 4.0, 3)
            k.solid(body, [(0, 14), (0.6, 18), (3.0, 21.5), (5.5, 22.5)], 'wax', cap=True, smooth=True, name="candle")
            self._drip(k, x - w * 0.22, y + b - 2, 6.0, 15.0 + (dx % 7), 16.0)
            self._drip(k, x + w * 0.30, y + b - 2, 4.6, 9.0, 16.0)
            fl = []
            for i in range(16):
                a = 2 * math.pi * i / 16
                r = 5.0 + 1.5 * math.cos(a)
                fl.append((x + r * math.sin(a) * 0.72, top - 9 - r * math.cos(a) * 1.25 + 2))
            k.gem(fl, 4.5, 'stone', z=0.5, table=0.35)
            k.tube([(x, top + 1), (x + 0.6, top - 3.5)], 0.9, 'floor', z=21.0)
        # long runs over the moulding left and right of the sconce
        for dx, ln, w in ((-150, 26, 7.0), (-136, 14, 5.0), (146, 20, 6.2), (158, 11, 4.4)):
            self._drip(k, cx + dx, y + 6, w, ln + b * 0.5, 9.0)


# ═════════════════════════════════ C: dark leather and iron, reliquary ═════════════════════════════════
class StyleC(Style):
    key = "C"
    title = "dark leather and iron reliquary"
    field = "leather"
    field_tip = "leather_dark"
    win_bar = [(0, 0), (0, 6), (2, 8.5), (5, 8.5), (6.5, 6), (8, 7.5), (11, 9.5), (30, 9.5), (33, 7.5), (35, 5), (37, 6.5), (40, 6.5), (42, 2), (42, 0)]
    win_inlay = None
    sub_bar = [(0, 0), (0, 4), (1.5, 5.5), (3.5, 5.5), (4.5, 4), (6, 6), (16, 6), (18, 4), (20, 4.5), (22, 0)]
    sub_inlay = None
    tip_bar = [(0, 0), (0, 3.5), (1.5, 5), (3.5, 5), (4.5, 3.5), (6, 5.5), (14, 5.5), (16, 3), (17.5, 3.5), (19, 0)]
    tip_inlay = None

    def mats(self, k):
        k.MATS['bar'] = k.mat_leather('C_bar', tint=(0.052, 0.026, 0.016), rough=(0.5, 0.8))
        k.MATS['plate'] = k.mat_metal('C_plate', metal=0.85, base=(0.05, 0.047, 0.047), rough=(0.36, 0.62), edge=(0.62, 0.58, 0.52), edge_amt=0.9, soot=0.7, hammer=1.1,
                                      patina=(0.16, 0.07, 0.03), patina_amt=0.25)
        k.MATS['face'] = k.mat_leather('C_face', tint=(0.050, 0.026, 0.017), rough=(0.5, 0.8))
        k.MATS['trim'] = k.mat_metal('C_trim', base=(0.72, 0.50, 0.24), rough=(0.3, 0.5), edge=(1.0, 0.84, 0.55), edge_amt=0.6, dark=(0.25, 0.14, 0.05),
                                     soot=0.5, hammer=0.3, bump=0.5)
        k.MATS['stud'] = k.mat_metal('C_stud', base=(0.09, 0.085, 0.085), rough=(0.28, 0.5), edge=(0.8, 0.76, 0.7), edge_amt=1.0, soot=0.6, hammer=0.3)
        k.MATS['stone'] = k.mat_ember('C_stone', color=(1.0, 0.33, 0.05), strength=2.8)
        k.MATS['floor'] = k.mat_field('C_floor', color=(0.014, 0.012, 0.011))
        k.MATS['band'] = k.mat_enamel('C_band')
        k.MATS['thread'] = k.mat_plain('C_thread', (0.50, 0.36, 0.17), 0.7)

    def stud(self, k, x, y, r, z, mat='stud'):
        return k.dome(x, y, r, r * 0.62, mat, z=z)

    corner_reach = 104.0
    btn_cut = 3.0
    slot_cut = 5.0

    def edge(self, k, m, R, bw, heavy=True):
        from pieces import edge_marks
        p = m['pad']
        step = m['px'] / 20.0
        lo, hi = p + self.corner_reach + 6, m['w'] - p - self.corner_reach - 6
        for d in (13.5, 27.5):
            for x in edge_marks(m, lo, hi, m['ml'], m['px'], step):
                for yy in (p + d, m['h'] - p - d):
                    k.tube([(x - 3.6, yy, 9.2), (x, yy, 10.3), (x + 3.6, yy, 9.2)], 1.25, 'thread', res=2)
            for y in edge_marks(m, p + self.corner_reach + 6, m['h'] - p - self.corner_reach - 6, m['mt'], m['py'], m['py'] / 20.0):
                for xx in (p + d, m['w'] - p - d):
                    k.tube([(xx, y - 3.6, 9.2), (xx, y, 10.3), (xx, y + 3.6, 9.2)], 1.25, 'thread', res=2)

    def stitch_line(self, k, p0, p1, z, step=13.0, ln=7.5, r=1.25, skip=None):
        """Saddle stitches from p0 to p1; skip = (a, b) distances along the line to leave bare."""
        L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        if L < 1: return
        ux, uy = (p1[0] - p0[0]) / L, (p1[1] - p0[1]) / L
        n = int(L // step)
        off = (L - n * step) * 0.5 + (step - ln) * 0.5
        for i in range(n):
            a = off + i * step
            k.tube([(p0[0] + ux * a, p0[1] + uy * a, z - 0.6), (p0[0] + ux * (a + ln * 0.5), p0[1] + uy * (a + ln * 0.5), z + 0.5),
                    (p0[0] + ux * (a + ln), p0[1] + uy * (a + ln), z - 0.6)], r, 'thread', res=2)

    def corner(self, k, T, bar_w, heavy=True, idx=0):
        b = bar_w
        if heavy:
            s = b + 46.0
            out = [(-3, -3), (s, -3), (s + 10, 8), (b + 14, b + 14), (8, s + 10), (-3, s)]
            k.solid(T(out), [(0, 0), (0, 11.5), (2.5, 14), (5, 14), (6.5, 12.5)], 'plate', cap=True, name="strap")
            for p in ((s - 12, 12), (12, s - 12), (b * 0.5 + 2, b * 0.5 + 2)):
                self.stud(k, *T(p), 5.4 if p[0] == p[1] else 4.2, 13.8)
            # engraved-looking gold line following the diagonal edge
            k.tube([T((s - 4, 14)), T((b + 15, b + 3)), T((b + 3, b + 15)), T((14, s - 4))], 1.5, 'trim', z=13.6)
        else:
            s = b + 14.0
            out = [(-2, -2), (s, -2), (b * 0.5 + 4, b * 0.5 + 4), (-2, s)]
            k.solid(T(out), [(0, 0), (0, 7), (1.5, 8.5), (3.2, 8.5), (4.2, 7.4)], 'plate', cap=True, name="strap")
            self.stud(k, *T((b * 0.36, b * 0.36)), 3.2, 8.4)

    def crest(self, k, cx, y, bar_w):
        """Reliquary hasp: a hinged iron plate with a lantern-shaped ember window."""
        b = bar_w
        mid = y + b * 0.5
        plate = [(cx - 66, y - 12), (cx - 30, y - 30), (cx + 30, y - 30), (cx + 66, y - 12), (cx + 66, y + b - 2), (cx + 34, y + b + 14), (cx - 34, y + b + 14), (cx - 66, y + b - 2)]
        k.solid(plate, [(0, 0), (0, 13), (2.5, 15.5), (6, 15.5), (7.5, 13.5)], 'plate', cap=True, name="hasp")
        for s in (-1, 1):
            out = [(cx + s * 60, y + 4), (cx + s * 150, y + 4), (cx + s * 166, mid), (cx + s * 150, y + b - 4), (cx + s * 60, y + b - 4)]
            if s < 0: out = out[::-1]
            k.solid(out, [(0, 0), (0, 11), (2, 13), (4.5, 13), (5.5, 11.8)], 'plate', cap=True, name="hinge")
            for dx in (86, 116, 146):
                self.stud(k, cx + s * dx, mid, 4.0, 13.0)
            self.stud(k, cx + s * 50, y - 6, 4.0, 15.4)
            self.stud(k, cx + s * 50, y + b - 2, 4.0, 15.4)
        # lantern window: pointed arch
        arch = []
        for i in range(13):
            a = math.pi * (1 - i / 12.0)
            arch.append((cx + 15 * math.cos(a), mid - 4 - 16 * math.sin(a) ** 0.8))
        arch = arch + [(cx + 15, mid + 16), (cx - 15, mid + 16)]
        k.solid(k.offset(arch, -5.0), [(0, 13.5), (0, 16.5), (1.6, 18), (3.6, 18), (4.6, 15.6)], 'trim', cap=True, name="bezel")
        k.gem(arch, 6.5, 'stone', z=15.6, table=0.5)
