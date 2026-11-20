class Looper:
    """inchworm: the rear end (head of s1) and the front end (tail of sN) are placed on targets; the chain between
    them humps up. The end links keep their own pitch (flat on the ground by default), so nothing digs into the floor:
    link pitch phi(t) = th_r (1-t)^2 + th_f t^2 + A sin(2 pi t) + B sin(pi t); A and B are solved so the ends meet
    the targets (A = height of the hump, B = the slope when one end is lifted)."""
    A_MAX = 2.5

    def __init__(self, C):
        self.C = C
        rg = C.rg
        self.seg = sorted([n for n in rg.names if n.startswith('s') and n[1:].isdigit()], key=lambda n: int(n[1:]))
        self.n = len(self.seg)
        self.rear0 = rg.H(self.seg[0])
        self.front0 = rg.T(self.seg[-1])
        self.L = float(np.linalg.norm(self.front0 - self.rear0))
        self.ls = self.L / self.n
        self.z = float(self.rear0[2])
        self.has_head = 'head' in rg.idx
        self.fr = Leg([self.seg[-1]], self.front0, name='front')
        self.rr = Leg([self.seg[0]], self.rear0, name='rear')
        self.fr.home = V(0, -self.L * 0.42, self.z)
        self.rr.home = V(0, self.L * 0.42, self.z)
        C.legs = [self.fr, self.rr]
        self.top = rg.T('head') if self.has_head else self.front0
        self._t = np.arange(self.n) / (self.n - 1.0)
        self._s2 = np.sin(2 * np.pi * self._t)
        self._s1 = np.sin(np.pi * self._t)
        self.min_span = self.span_at(self.A_MAX)

    def span_at(self, A):
        return float(self.ls * np.cos(A * self._s2).sum())

    def _solve(self, dh, dz, th_r=0.0, th_f=0.0):
        """-> (link pitches phi (rad, + = nose up), error m)"""
        t = self._t
        base = th_r * (1 - t) ** 2 + th_f * t ** 2
        s1, s2, ls = self._s1, self._s2, self.ls

        def a_for(Bc):
            b = base + Bc * s1
            g = lambda A: float(np.cos(b + A * s2).sum()) * ls - dh
            if g(0.0) <= 0.0:
                return 0.0
            if g(self.A_MAX) >= 0.0:
                return self.A_MAX
            lo, hi = 0.0, self.A_MAX
            for _ in range(26):
                mid = (lo + hi) / 2
                if g(mid) > 0:
                    lo = mid
                else:
                    hi = mid
            return (lo + hi) / 2

        def h(Bc):
            A = a_for(Bc)
            return float(np.sin(base + Bc * s1 + A * s2).sum()) * ls - dz, A
        lo, hi = -1.5, 1.5
        hl, _ = h(lo); hh, _ = h(hi)
        if hl >= 0:
            Bc = lo
        elif hh <= 0:
            Bc = hi
        else:
            for _ in range(24):
                mid = (lo + hi) / 2
                if h(mid)[0] < 0:
                    lo = mid
                else:
                    hi = mid
            Bc = (lo + hi) / 2
        A = a_for(Bc)
        ph = base + Bc * s1 + A * s2
        e = math.hypot(float(np.cos(ph).sum()) * ls - dh, float(np.sin(ph).sum()) * ls - dz)
        return ph, e

    def place(self, p, rear, front, roll=0.0, yaw=0.0, pitch_r=0.0, pitch_f=0.0, drag=True):
        """rear / front: targets of the two ends (bone axis, armature space). pitch_r / pitch_f = pitch of the end links
        in degrees (+ = nose up; 0 = flat). roll turns the loop about the chord (90 = the hump lies on its side);
        yaw turns the whole animal about its rear end. drag: a target further than the body is long pulls the
        rear end after it"""
        rear = np.asarray(rear, dtype=float).copy(); front = np.asarray(front, dtype=float)
        ch = front - rear
        d = float(np.linalg.norm(ch))
        if d > 0.985 * self.L:
            if drag:
                rear = front - ch / d * (0.985 * self.L)
                ch = front - rear
            else:
                p.err = max(p.err, d - self.L)
        dh = math.hypot(ch[0], ch[1])
        ph, e = self._solve(dh, float(ch[2]), math.radians(pitch_r), math.radians(pitch_f))
        if e > 0.01:
            p.err = max(p.err, e)
        az = math.degrees(math.atan2(-ch[0], -ch[1])) if dh > 1e-9 else 0.0
        b0 = self.seg[0]
        p.setR(b0, rz(-az + yaw) @ ry(roll) @ rx(-math.degrees(ph[0])))
        p.move(b0, rear - self.rear0)
        for i in range(1, self.n):
            p.rot(self.seg[i], x=math.degrees(ph[i - 1] - ph[i]))
        return ph

    def targets(self, u, S, duty, lift, lift_r=None, phase_r=0.5):
        self.fr.phase = 0.0; self.rr.phase = phase_r
        Pf, swf, _ = gait_foot(self.fr, u, S, duty, lift)
        Pr, swr, _ = gait_foot(self.rr, u, S, duty, lift if lift_r is None else lift_r)
        return Pf, Pr, swf, swr

    def set_homes(self, S, duty, phase_r=0.5, slack=0.97, lift=0.0, lift_r=None, n=240):
        """distance between the homes of the two ends such that the longest span of the cycle is slack x body length.
        -> (home distance, shortest span of the cycle)"""
        self.fr.home = V(0, 0, self.z); self.rr.home = V(0, 0, self.z)
        D = []
        for i in range(n):
            Pf, Pr, _, _ = self.targets(i / float(n), S, duty, lift, lift_r, phase_r)
            D.append((Pr[1] - Pf[1], Pr[2] - Pf[2]))
        D = np.array(D)
        lo, hi = 0.0, 2 * self.L
        for _ in range(40):
            mid = (lo + hi) / 2
            if np.hypot(mid + D[:, 0], D[:, 1]).max() > slack * self.L:
                hi = mid
            else:
                lo = mid
        h = lo
        self.fr.home = V(0, -h / 2, self.z); self.rr.home = V(0, h / 2, self.z)
        return h, float(np.hypot(h + D[:, 0], D[:, 1]).min()), float((h + D[:, 0]).min())

    def gait(self, name, v, T, duty, lift=0.04, lift_r=None, phase_r=0.5, head=0.0, sway=0.0, nose=0.0, extra=None, slack=0.97):
        """inchworm travel. The ends are planted for `duty` of the cycle each, half a cycle apart (duty 0.5 = classic
        inchworm; < 0.5 = bounding loops with both ends in the air in between). nose = pitch of the front link while
        it swings (degrees, + = up). Put duty on the frame grid (k / frames)."""
        T = max(2, round(T * B.FPS)) / B.FPS
        S = v * duty * T
        h, mn, mny = self.set_homes(S, duty, phase_r, slack, lift, lift_r)
        if mn < self.min_span or mny < 0:
            B.log("NOTE %s %s: looper span gets as short as %.2f m (tightest loop %.2f m, stride %.2f m)" % (self.C.id, name, mn, self.min_span, S))
        homes = (self.fr.home.copy(), self.rr.home.copy())

        def fn(p, u):
            self.fr.home, self.rr.home = homes[0].copy(), homes[1].copy()
            Pf, Pr, swf, swr = self.targets(u, S, duty, lift, lift_r, phase_r)
            self.place(p, Pr, Pf, yaw=sway * osc(u, 1), pitch_f=nose * swf, drag=False)
            if self.has_head:
                p.rot('head', x=head * swf)
            if extra:
                extra(p, u, swf, swr)
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        self.fr.home, self.rr.home = V(0, -self.L * 0.42, self.z), V(0, self.L * 0.42, self.z)
        return c

    def rest_ends(self, k=0.42):
        return V(0, self.L * k, self.z), V(0, -self.L * k, self.z)

    def reach(self, clip, pt=None):
        return self.C.reach(clip, 'head' if self.has_head else self.seg[-1], self.top if pt is None else pt)
