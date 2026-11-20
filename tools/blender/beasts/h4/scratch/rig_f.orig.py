"""Rig F - rigid / floating things. Every creature has its own small skeleton (built with blib.Sk directly); this
module only holds the shared motion helpers. Floaters are authored at their float height (meta fly_height,
hover_in_model = true): Death falls to the ground, Spawn drops in. Turrets stand on the ground; their Walk / Run are
copies of Idle (meta walk_speed = run_speed = 0)."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3
import bparts as P


class Rigid:
    def __init__(self, C, bone='body', pivot=None, hover=None):
        self.C = C
        self.bone = bone
        rg = C.rg
        self.pivot = (rg.H(bone) + rg.T(bone)) / 2 if pivot is None else V(pivot)
        if hover is not None:
            C.meta.update(fly_height=round(float(hover), 3), hover_in_model=True)

    def body(self, p, dx=0.0, dy=0.0, dz=0.0, pitch=0.0, roll=0.0, yaw=0.0, bone=None, pivot=None):
        """move / turn a bone about a pivot (default: the main body about its middle)"""
        b = bone or self.bone
        R = euler(pitch, roll, yaw)
        h = self.C.rg.H(b)
        pv = self.pivot if pivot is None else np.asarray(pivot, dtype=float)
        p.setR(b, R)
        p.move(b, (pv - h) - R @ (pv - h) + V(dx, dy, dz))

    def dangle(self, p, bones, x=0.0, y=0.0, u=0.0, amp=0.0, n=1.0, lag=0.12, amp_y=None, ph=0.0, grow=0.0):
        """hanging chain: a constant lean (x = trails towards +Y, y = towards -X... degrees, shared over the links)
        plus a pendulum wave that lags down the chain"""
        k = max(len(bones), 1)
        for i, b in enumerate(bones):
            g = 1.0 + grow * i
            p.rot(b, x=x / k + amp * g * osc(u, n, ph - i * lag), y=y / k + (amp if amp_y is None else amp_y) * g * osc(u, n, ph + 0.25 - i * lag))

    def copy_clip(self, src, name, speed_key=None, v=0.0):
        """turrets: Walk / Run are the Idle clip under another name"""
        c = dict(self.C.clips[src])
        self.C.clips[name] = c
        if speed_key:
            self.C.meta[speed_key] = v
        return c


def fall_curve(u, land):
    """0..1 height factor of a free fall that lands at u = land (1 = still up, 0 = down)"""
    t = min(1.0, u / land)
    return 1.0 - t * t


def chain_parts(C, bones, look, link=0.045, r=0.006, w=0.62, n=6, m=4, extend=0.0):
    """chain links along a bone chain, each link rigid to the bone it hangs on"""
    rg = C.rg
    for i, b in enumerate(bones):
        a = rg.H(b); e = rg.T(b)
        if i == len(bones) - 1 and extend:
            e = e + unit(e - a) * extend
        ch = M.chain([a, e], link=link, r=r, w=w, n=n, m=m)
        C.rigid(ch, b, look, smooth=60)
