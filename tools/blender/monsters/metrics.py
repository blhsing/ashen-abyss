"""Texture statistics of the baked atlases, v1 (out\\v1_tex\\<id>_*.png, 1024 px) against v2 (out\\tex\\<id>_*.jpg):
   blender -b --factory-startup --python metrics.py -- [id ...]
For every creature: resolution, normal-map deviation from "straight up" (median / 90th percentile, degrees), roughness
standard deviation over the whole atlas and LOCAL roughness contrast (mean standard deviation inside 3 cm UV windows:
16 px at 1K, 32 px at 2K), measured on the covered texels only.  Writes out\\metrics_v1_v2.txt."""
import sys, os, glob
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M


def px(path):
    im = bpy.data.images.load(path, check_existing=False)
    im.colorspace_settings.name = 'Non-Color'
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[:, :, :3]


def stats(bc, nrm, orm):
    if not (os.path.isfile(bc) and os.path.isfile(nrm) and os.path.isfile(orm)):
        return None
    c = px(bc); n = px(nrm); o = px(orm)
    size = c.shape[0]
    cover = np.abs(c - 0.2).sum(axis=2) > 0.03
    v = n * 2.0 - 1.0
    v /= np.maximum(np.linalg.norm(v, axis=2, keepdims=True), 1e-6)
    dev = np.degrees(np.arccos(np.clip(v[:, :, 2], -1, 1)))[cover]
    r = o[:, :, 1]
    win = max(4, size // 64)
    k = size // win
    rw = r[:k * win, :k * win].reshape(k, win, k, win)
    cw = cover[:k * win, :k * win].reshape(k, win, k, win)
    full = cw.all(axis=(1, 3))
    loc = rw.std(axis=(1, 3))[full]
    return dict(size=size, med=float(np.median(dev)), p90=float(np.percentile(dev, 90)), rstd=float(r[cover].std()),
                rloc=float(loc.mean()) if len(loc) else 0.0, cover=float(cover.mean()))


args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ids = [a for a in args if not a.startswith('--')]
if not ids:
    ids = sorted(os.path.basename(p)[:-7] for p in glob.glob(os.path.join(M.TEXDIR, "mon_*_bc.jpg")) +
                 glob.glob(os.path.join(M.TEXDIR, "boss_*_bc.jpg")))
V1 = os.path.join(M.WORK, "v1_tex")
lines = ["%-22s | %-34s | %-34s" % ("", "v1  (px, normal med/p90 deg, rough std / local)", "v2  (px, normal med/p90 deg, rough std / local)")]
for cid in ids:
    a = stats(os.path.join(V1, cid + "_bc.png"), os.path.join(V1, cid + "_nrm.png"), os.path.join(V1, cid + "_orm.png"))
    b = stats(os.path.join(M.TEXDIR, cid + "_bc.jpg"), os.path.join(M.TEXDIR, cid + "_nrm.jpg"), os.path.join(M.TEXDIR, cid + "_orm.jpg"))

    def f(s):
        return "n/a" if s is None else "%4d  %4.1f / %4.1f   %.3f / %.3f" % (s['size'], s['med'], s['p90'], s['rstd'], s['rloc'])
    lines.append("%-22s | %-34s | %-34s" % (cid, f(a), f(b)))
txt = "\n".join(lines)
for l in lines:
    print("[mon] METRIC " + l)
with open(os.path.join(M.WORK, "metrics_v1_v2.txt"), 'w', encoding='utf8') as fh:
    fh.write(txt + "\n")
M.log("METRICS OK", len(ids))
