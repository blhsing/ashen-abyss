"""Helpers for creatures sculpted from the re-fitted base body (weights preserved)."""
import math
import numpy as np
import monlib as M
from monlib import V, tube, ellipsoid, box, join, horn, lerp

# landmarks of the male base body in UAL rest space (face towards -Y)
FACE = dict(chin=(0, -0.105, 1.555), mouth=(0, -0.119, 1.602), nose=(0, -0.137, 1.626), eye_l=(0.031, -0.108, 1.668),
            brow=(0, -0.117, 1.69), top=(0, -0.02, 1.779), ear_l=(0.088, -0.01, 1.66), centre=(0, -0.02, 1.67),
            jaw_hinge=(0, -0.005, 1.632), cheek_l=(0.052, -0.09, 1.625), back=(0, 0.084, 1.68))

FINGERS = ('index', 'middle', 'ring', 'pinky', 'thumb')


def open_jaw(body, deg=16.0, drop=0.0):
    """hang the lower jaw open (mesh-only: the rig has no jaw bone)"""
    v = body.v
    hw = body.wsum(['Head'])
    w = M.smoothstep(1.612, 1.594, v[:, 2]) * M.smoothstep(0.035, -0.02, v[:, 1]) * M.smoothstep(0.3, 0.7, hw)
    hinge = np.array(FACE['jaw_hinge'])
    R = M.rot_axis('x', deg)
    moved = (v - hinge) @ R.T + hinge + np.array([0, 0, -drop])
    body.v = v + (moved - v) * w[:, None]
    lower_lip = (np.array(FACE['mouth']) + np.array([0, 0, -0.006]) - hinge) @ R.T + hinge
    return (np.array(FACE['mouth']) + lower_lip) / 2, np.linalg.norm(np.array(FACE['mouth']) - lower_lip)


def gaunt_face(body, amt=1.0):
    for sx in (1, -1):
        e = np.array(FACE['eye_l']) * np.array([sx, 1, 1])
        body.dent(e, 0.02, 0.016 * amt, direction=(0, 1, 0))               # sunken eyes
        c = np.array(FACE['cheek_l']) * np.array([sx, 1, 1])
        body.dent(c, 0.03, 0.014 * amt, direction=(-sx * 0.7, 0.7, 0))      # hollow cheeks
        body.dent((sx * 0.075, -0.045, 1.70), 0.03, 0.008 * amt, direction=(-sx, 0, 0))   # temples
    return body


def fingertip(body, finger, side):
    """(tip position, finger direction, tip radius) measured on the (sculpted) body mesh"""
    R = M.RIG
    bn = '%s_03_%s' % (finger, side)
    w = body.W[:, R.idx[bn]]
    sel = np.where(w > 0.45)[0]
    a = R.H(bn); d = M.unit(R.T(bn) - a)
    if len(sel) < 3:
        return R.T(bn), d, 0.007
    vv = body.v[sel]
    t = (vv - vv.mean(0)) @ d
    tip = vv[np.argmax(t)]
    cen = vv.mean(0)
    rad = float(np.linalg.norm((vv - cen) - np.outer((vv - cen) @ d, d), axis=1).mean())
    return tip, d, rad


def claws(C, look, body=None, length=0.05, r=None, sides=('l', 'r'), curl=0.4, fingers=FINGERS, n=5, sink=0.014):
    """rigid curved claws on the last finger bones. With `body` they are seated on the real fingertips of the
    sculpted mesh, otherwise on the bone tails."""
    R = M.RIG
    for side in sides:
        for fg in fingers:
            bn = '%s_03_%s' % (fg, side)
            if body is not None:
                tip, d, rad = fingertip(body, fg, side)
            else:
                tip, d, rad = R.T(bn), M.unit(R.T(bn) - R.H(bn)), 0.007
            rr = (rad * 0.95 if r is None else r) * (1.12 if fg == 'thumb' else 1.0)
            p0 = tip - d * sink
            down = np.array([0, 0, -1.0])
            path = [p0, p0 + d * (sink + length * 0.5) + down * length * curl * 0.15, p0 + d * (sink + length) + down * length * curl]
            cl = horn(M.polyline(path, 5), rr, n=n, r_tip=0.0008, power=0.8)
            cl.smooth = 40
            C.add(cl, bone=bn, look=look)


def toe_claws(C, look, length=0.035, r=0.007, sides=('l', 'r')):
    xs = [0.058, 0.076, 0.093, 0.108, 0.121]
    for i, x in enumerate(xs):
        y0 = -0.2 + 0.012 * i
        cl = horn([(x, y0 + 0.01, 0.022), (x, y0 - length * 0.6, 0.02), (x, y0 - length, 0.004)], r * (1.2 if i == 0 else 1.0), n=5, r_tip=0.001)
        if 'l' in sides:
            C.add(cl, bone='ball_l', look=look)
        if 'r' in sides:
            C.add(cl.copy().mirror(), look=look)


def noise_mask(part, freq, thresh, seed=0.0):
    """per-vertex boolean mask from 3D noise (ragged patches)"""
    return np.array([M.fbm(p, freq, seed, 3) for p in part.v]) > thresh


def shell(body, vmask, offset=0.008, mode='all'):
    """a skin-tight shell (torn cloth, bandages, plates of hide) made from the body's own faces: weights for free"""
    s = body.sub(body.vmask_faces(vmask, mode))
    s.inflate(offset)
    s.smooth = 180.0
    return s


def spine_knobs(C, look, pose=None, n=9, r=0.016, y_off=0.0):
    """vertebra bumps showing through the skin of the back"""
    ctrl = [(0, 0.118, 1.06), (0, 0.105, 1.16), (0, 0.13, 1.28), (0, 0.15, 1.38), (0, 0.11, 1.47), (0, 0.075, 1.53)]
    pts = M.polyline(ctrl, n)
    for p in pts:
        z = p[2]
        bn = 'spine_01' if z < 1.17 else ('spine_02' if z < 1.31 else ('spine_03' if z < 1.48 else 'neck_01'))
        k = ellipsoid(r * 0.9, r, r * 0.8, n=8, m=5).move(p[0], p[1] + y_off, p[2])
        if pose is not None:
            M.pose_part(k, pose, bn)
        C.add(k, bone=bn, look=look, smooth=90)
