"""Prepare the scanned PBR sets for the monster looks:
   blender -b --factory-startup --python montex_prep.py -- [alias ...] [--force] [--sheet]
For every set of montex.SETS (see montex.py for the cache format): load the raw maps, remove the low-frequency
lighting / colour gradient from the albedo (so the tile repeats invisibly and can modulate any look colour),
integrate the OpenGL normal map into a float height field (FFT / Frankot-Chellappa, exact for tileable maps) and
write cache\\montex\\<alias>.npz.   --sheet writes Y:\\shots\\chars\\_montex_sheet.png (albedo detail | height | rough)."""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import montex as MT

RES = 1024


def log(*a):
    print("[montex]", *a)
    sys.stdout.flush()


def load_px(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[:, :, :3]


def to_res(a, res=RES):
    h, w = a.shape[:2]
    if h == res and w == res:
        return a
    if h % res == 0 and w % res == 0 and h // res == w // res:
        return MT.box_down(a, h // res)
    # generic bilinear resample (non power-of-two / non-square sources)
    ys = (np.arange(res) + 0.5) * h / res - 0.5
    xs = (np.arange(res) + 0.5) * w / res - 0.5
    y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
    fy = (ys - y0)[:, None, None]; fx = (xs - x0)[None, :, None]
    y1 = (y0 + 1) % h; x1 = (x0 + 1) % w; y0 %= h; x0 %= w
    if a.ndim == 2:
        a = a[:, :, None]
    out = (a[y0][:, x0] * (1 - fy) * (1 - fx) + a[y0][:, x1] * (1 - fy) * fx + a[y1][:, x0] * fy * (1 - fx) + a[y1][:, x1] * fy * fx)
    return out if out.shape[2] > 1 else out[:, :, 0]


def srgb2lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lowpass(a, cycles):
    """periodic gaussian low-pass keeping frequencies below ~`cycles` per tile"""
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h) * h
    fx = np.fft.fftfreq(w) * w
    g = np.exp(-(fy[:, None] ** 2 + fx[None, :] ** 2) / (2.0 * cycles ** 2))
    if a.ndim == 2:
        return np.real(np.fft.ifft2(np.fft.fft2(a) * g))
    return np.stack([np.real(np.fft.ifft2(np.fft.fft2(a[:, :, k]) * g)) for k in range(a.shape[2])], axis=2)


def integrate_normals(n):
    """n: (h,w,3) OpenGL tangent normals in -1..1 (row 0 = bottom, +x right, +y up) -> height in tile units"""
    nz = np.clip(n[:, :, 2], 0.08, None)
    gx = -n[:, :, 0] / nz
    gy = -n[:, :, 1] / nz
    gx -= gx.mean(); gy -= gy.mean()
    h, w = gx.shape
    ky = 2 * np.pi * np.fft.fftfreq(h, d=1.0 / h)[:, None]     # radians per tile unit
    kx = 2 * np.pi * np.fft.fftfreq(w, d=1.0 / w)[None, :]
    den = kx ** 2 + ky ** 2
    den[0, 0] = 1.0
    H = (-1j * kx * np.fft.fft2(gx) - 1j * ky * np.fft.fft2(gy)) / den
    H[0, 0] = 0.0
    return np.real(np.fft.ifft2(H)).astype(np.float32), float(np.sqrt((gx ** 2 + gy ** 2).mean()))


def prep(alias, force=False):
    out = MT.cache_path(alias)
    if os.path.isfile(out) and not force:
        return None
    m = MT.src_maps(alias)
    if not m['diff'] or not m['nor']:
        log("MISSING maps for", alias, MT.SETS[alias]['id'])
        return None
    alb = srgb2lin(to_res(load_px(m['diff'])))
    # flatten the lowest frequencies (lighting gradients, big stains) to half strength, in log space
    mean = alb.mean(axis=(0, 1))
    lum = alb @ np.array([0.2126, 0.7152, 0.0722])
    ll = np.log(np.clip(lum, 2e-3, None))
    lo = lowpass(ll, 1.6)
    lr = ll - lo + 0.5 * (lo - lo.mean())                      # luminance log-ratio
    lr -= lr.mean()
    s = float(lr.std())
    if s > 0.42:                                               # very contrasty scans: compress to a common range
        lr *= 0.42 / s
    # chroma variation relative to the luminance, only from well exposed channels, at half strength
    cr = np.zeros_like(alb)
    for k in range(3):
        if mean[k] > 0.03:
            ck = np.log(np.clip(alb[:, :, k], 2e-3, None)) - ll
            cr[:, :, k] = np.clip(0.5 * (ck - lowpass(ck, 1.6)), -0.5, 0.5)
    det = np.exp(lr[:, :, None] + cr)
    det = det / det.mean(axis=(0, 1))
    nrm = to_res(load_px(m['nor'])) * 2.0 - 1.0
    nrm /= np.maximum(np.linalg.norm(nrm, axis=2, keepdims=True), 1e-6)
    hgt, slope = integrate_normals(nrm)
    # drop the slowest undulations (integration drift, uneven scan lighting): the height is then a LOCAL relief,
    # so "h below zero" really means a pore / crack / gap between threads
    hgt = (hgt - lowpass(hgt, 2.5)).astype(np.float32)
    hgt -= hgt.mean()
    if m['rough']:
        rough = to_res(load_px(m['rough']))[:, :, 0]
    else:
        rough = np.full((RES, RES), 0.6, dtype=np.float32)
    if m['ao']:
        ao = to_res(load_px(m['ao']))[:, :, 0]
    else:
        hb = lowpass(hgt, 24.0)
        ao = np.clip(1.0 - np.clip(hb - hgt, 0, None) / max(float(hgt.std()), 1e-6) * 0.35, 0, 1)
    meta = dict(alias=alias, id=MT.SETS[alias]['id'], size_m=MT.SETS[alias]['size'], mean_albedo=[float(x) for x in mean],
                slope_rms=slope, h_std=float(hgt.std()), rough_mean=float(rough.mean()), rough_std=float(rough.std()),
                det_std=float(det.mean(axis=2).std()))
    os.makedirs(MT.CACHE, exist_ok=True)
    np.savez_compressed(out, det=np.clip(det, 0, 8).astype(np.float16), rough=np.clip(rough * 255 + 0.5, 0, 255).astype(np.uint8),
                        h=hgt.astype(np.float32), ao=np.clip(ao * 255 + 0.5, 0, 255).astype(np.uint8), meta=np.array(json.dumps(meta)))
    log("%-12s %-30s slope_rms %.3f  relief(std) %.2f mm  rough %.2f+-%.2f  albedo %s  det_std %.2f" % (
        alias, meta['id'], slope, meta['h_std'] * meta['size_m'] * 1000, meta['rough_mean'], meta['rough_std'],
        "(%.2f %.2f %.2f)" % tuple(mean), meta['det_std']))
    return meta


def sheet(aliases, path, tile=256):
    rows = []
    for a in aliases:
        if not MT.available(a):
            continue
        d = MT.load(a)
        f = RES // tile
        det = MT.box_down(d['det'], f) * np.array(d['meta']['mean_albedo'])[None, None, :]
        det = np.clip(det, 0, 1) ** (1 / 2.2)
        h = MT.box_down(d['h'], f)
        h = np.clip(0.5 + h / (6 * max(float(h.std()), 1e-9)), 0, 1)
        r = MT.box_down(d['rough'], f)
        rows.append(np.concatenate([det, np.repeat(h[:, :, None], 3, 2), np.repeat(r[:, :, None], 3, 2)], axis=1))
    cols = 3
    while len(rows) % cols:
        rows.append(np.zeros_like(rows[0]))
    lines = [np.concatenate(rows[i:i + cols], axis=1) for i in range(0, len(rows), cols)]
    img = np.concatenate(lines[::-1], axis=0)
    hh, ww = img.shape[:2]
    im = bpy.data.images.new("sheet", ww, hh, alpha=False)
    a = np.ones((hh, ww, 4), dtype=np.float32); a[:, :, :3] = img
    im.pixels.foreach_set(a.ravel())
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    log("sheet", path, "order:", [a for a in aliases if MT.available(a)])


args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
force = '--force' in args
names = [a for a in args if not a.startswith('--')] or list(MT.SETS)
for a in names:
    try:
        prep(a, force)
    except Exception as e:
        import traceback
        traceback.print_exc()
        log("FAILED", a, e)
if '--sheet' in args:
    sheet(names, r"Y:\shots\chars\_montex_sheet.png")
log("PREP DONE", len([a for a in names if MT.available(a)]), "of", len(names))
