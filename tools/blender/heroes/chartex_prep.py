"""Prepare the scanned / synthesised detail tiles of chartex.SETS:
   blender -b --factory-startup --python chartex_prep.py -- [alias ...] [--force] [--sheet]
Scans: albedo -> linear detail multiplier (mean removed, lowest frequencies flattened), OpenGL normal map -> float height
(Frankot-Chellappa integration, exact for tileable maps), roughness. Synth sets (fur, mail) are generated here.
--sheet writes Y:\\shots\\chars\\_chartex_sheet.jpg (albedo | height | roughness per set)."""
import sys, os, json, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import chartex as CT


def log(*a):
    print("[hero]", *a)
    sys.stdout.flush()


def load_px(path):
    im = bpy.data.images.load(path, check_existing=False)
    im.colorspace_settings.name = 'Non-Color'
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)[:, :, :3]


def box_down(a, f):
    if f <= 1:
        return a
    h, w = a.shape[:2]
    h2, w2 = h // f, w // f
    a = a[:h2 * f, :w2 * f]
    if a.ndim == 2:
        return a.reshape(h2, f, w2, f).mean(axis=(1, 3))
    return a.reshape(h2, f, w2, f, a.shape[2]).mean(axis=(1, 3))


def to_res(a, res):
    h, w = a.shape[:2]
    if h == res and w == res:
        return a
    if h % res == 0 and w % res == 0 and h // res == w // res:
        return box_down(a, h // res)
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
    """n: (h,w,3) OpenGL tangent normals -1..1 (row 0 = bottom) -> (height in tile units, rms slope)"""
    nz = np.clip(n[:, :, 2], 0.08, None)
    gx = -n[:, :, 0] / nz
    gy = -n[:, :, 1] / nz
    gx -= gx.mean(); gy -= gy.mean()
    h, w = gx.shape
    ky = 2 * np.pi * np.fft.fftfreq(h, d=1.0 / h)[:, None]
    kx = 2 * np.pi * np.fft.fftfreq(w, d=1.0 / w)[None, :]
    den = kx ** 2 + ky ** 2
    den[0, 0] = 1.0
    H = (-1j * kx * np.fft.fft2(gx) - 1j * ky * np.fft.fft2(gy)) / den
    H[0, 0] = 0.0
    return np.real(np.fft.ifft2(H)).astype(np.float32), float(np.sqrt((gx ** 2 + gy ** 2).mean()))


def slope_rms(h):
    """rms slope of a tileable height field given in tile units"""
    n = h.shape[0]
    gx = (np.roll(h, -1, 1) - h) * n
    gy = (np.roll(h, -1, 0) - h) * n
    return float(np.sqrt((gx ** 2 + gy ** 2).mean()))


def dominant_period(h):
    """strongest spatial period of the height field in px (radial spectrum peak, 1/f compensated)"""
    n = h.shape[0]
    P = np.abs(np.fft.fft2(h - h.mean())) ** 2
    fy = np.fft.fftfreq(n) * n
    fx = np.fft.fftfreq(n) * n
    r = np.sqrt(fy[:, None] ** 2 + fx[None, :] ** 2)
    ri = np.rint(r).astype(int)
    rad = np.bincount(ri.ravel(), P.ravel(), minlength=n)[:n // 2]
    cnt = np.bincount(ri.ravel(), minlength=n)[:n // 2]
    spec = rad / np.maximum(cnt, 1) * (np.arange(n // 2) ** 2.0)
    spec[:3] = 0
    k = int(np.argmax(spec))
    return n / max(k, 1)


def save_npz(alias, det, h, rough, aux, meta):
    os.makedirs(CT.CACHE, exist_ok=True)
    hs = float(h.std())
    meta = dict(meta, alias=alias, h_std=hs, slope_rms=slope_rms(h), period_px=dominant_period(h), res=int(h.shape[0]),
                rough_mean=float(rough.mean()), rough_std=float(rough.std()), det_std=float(det.mean(axis=2).std()))
    np.savez_compressed(CT.cache_path(alias), det=np.clip(det, 0, 8).astype(np.float16),
                        h=(h / max(hs, 1e-9)).astype(np.float16),
                        rough=np.clip(rough * 255 + 0.5, 0, 255).astype(np.uint8),
                        aux=np.clip(aux * 255 + 0.5, 0, 255).astype(np.uint8), meta=np.array(json.dumps(meta)))
    s = CT.SETS[alias]
    log("%-10s %-28s res %d slope %.3f relief(std) %.2f mm@tile period %.1f px = %.1f mm@tile  rough %.2f+-%.2f  albedo %s det_std %.2f" % (
        alias, s['id'], h.shape[0], meta['slope_rms'], hs * s['tile'] * 1000, meta['period_px'],
        meta['period_px'] / h.shape[0] * s['tile'] * 1000, meta['rough_mean'], meta['rough_std'],
        "(%.2f %.2f %.2f)" % tuple(meta.get('mean_albedo', (0, 0, 0))), meta['det_std']))
    return meta


def prep_scan(alias):
    s = CT.SETS[alias]
    res = s.get('res', CT.RES)
    m = CT.src_maps(alias)
    if not m.get('diff') or not m.get('nor'):
        log("MISSING maps for", alias, s['id'], m)
        return None
    alb = srgb2lin(to_res(load_px(m['diff']), res))
    la = np.log(np.clip(alb, 1e-4, None))
    lo = lowpass(la, 1.6)
    mean = np.exp(lo.mean(axis=(0, 1)))
    det = np.exp(la - lo + 0.45 * (lo - lo.mean(axis=(0, 1))))
    det = det / det.mean(axis=(0, 1))
    nrm = to_res(load_px(m['nor']), res) * 2.0 - 1.0
    nrm /= np.maximum(np.linalg.norm(nrm, axis=2, keepdims=True), 1e-6)
    hgt, slope = integrate_normals(nrm)
    if m.get('rough'):
        rough = to_res(load_px(m['rough']), res)[:, :, 0]
    elif m.get('arm'):
        rough = to_res(load_px(m['arm']), res)[:, :, 1]
    else:
        rough = np.full((res, res), 0.6, dtype=np.float32)
    hb = lowpass(hgt, 20.0)
    cav = np.clip((hb - hgt) / max(float(hgt.std()), 1e-9) * 0.5, 0, 1)
    aux = np.stack([cav, np.zeros_like(cav)], axis=2)
    return save_npz(alias, det, hgt, rough, aux, dict(id=s['id'], size_m=s['size'], mean_albedo=[float(x) for x in mean]))


# ---------------------------------------------------------------------------------------------- synthesised tiles
def _stamp(H, layers, x0, y0, pad, fn):
    """evaluate fn(X, Y) -> (inside, z, values...) on a wrapped patch around (x0, y0) and z-buffer it into H / layers"""
    res = H.shape[0]
    ix = np.arange(int(math.floor(x0)) - pad, int(math.floor(x0)) + pad + 1)
    iy = np.arange(int(math.floor(y0)) - pad, int(math.floor(y0)) + pad + 1)
    X, Y = np.meshgrid(ix - x0, iy - y0)
    out = fn(X, Y)
    inside, z = out[0], out[1]
    wy = (iy % res)[:, None]
    wx = (ix % res)[None, :]
    cur = H[wy, wx]
    win = inside & (z > cur)
    cur = np.where(win, z, cur)
    H[wy, wx] = cur
    for L, v in zip(layers, out[2:]):
        c = L[wy, wx]
        L[wy, wx] = np.where(win, v, c)


def syn_fur(alias, res, n_locks, length, width, seed, strands=3.2, lift=1.5, spread=0.20):
    rng = np.random.RandomState(seed)
    H = np.full((res, res), -0.6, dtype=np.float32)
    Tp = np.zeros((res, res), dtype=np.float32)
    Lm = np.full((res, res), 0.45, dtype=np.float32)
    for _ in range(n_locks):
        x0, y0 = rng.rand(2) * res
        ang = -math.pi / 2 + rng.randn() * spread
        L = length[0] + rng.rand() * (length[1] - length[0])
        w0 = width[0] + rng.rand() * (width[1] - width[0])
        curl = rng.randn() * 0.25
        zlev = rng.rand() * 1.2
        lum = 0.72 + 0.56 * rng.rand()
        ph = rng.rand() * 6.28
        sf = strands * (0.8 + 0.4 * rng.rand())
        dx, dy = math.cos(ang), math.sin(ang)

        def fn(X, Y):
            u = X * dx + Y * dy
            v = -X * dy + Y * dx
            v = v - curl * u * u / L
            t = u / L
            hw = w0 * 0.5 * np.clip(1 - t, 0, 1) ** 0.8 * np.clip(t * 7 + 0.3, 0, 1)
            s = v / np.maximum(hw, 1e-3)
            inside = (t >= 0) & (t <= 1) & (np.abs(s) < 1)
            st = np.sin(v * sf + ph + 2.0 * np.sin(u * 0.11 + ph))
            z = zlev + lift * t + 0.85 * np.sqrt(np.clip(1 - s * s, 0, 1)) * (hw / (w0 * 0.5)) + 0.20 * st * (1 - 0.6 * t)
            return inside, z.astype(np.float32), t.astype(np.float32), (lum * (1.0 + 0.22 * st)).astype(np.float32)
        _stamp(H, (Tp, Lm), x0, y0, int(L + w0) + 2, fn)
    hb = lowpass(H, res / 14.0)
    cav = np.clip((hb - H) / max(float(H.std()), 1e-9) * 0.7, 0, 1)
    lum = Lm * (1.0 - 0.45 * cav)
    det = np.stack([lum / lum.mean(), Tp, cav], axis=2)      # fur tiles: R = strand lightness, G = tip-ness, B = cavity
    hgt = (H - H.mean()) / res * 0.9            # tile units: one lock is ~1.5 px-heights high
    rough = np.clip(0.80 - 0.22 * Tp + 0.15 * cav, 0, 1)
    aux = np.stack([cav, Tp], axis=2)
    return save_npz(alias, det, hgt, rough, aux, dict(id=alias, size_m=CT.SETS[alias]['size'], mean_albedo=[0.5, 0.5, 0.5]))


def syn_mail(alias, res, cols):
    dx = res / cols
    dy = dx / 2.0
    R = 0.58 * dx
    wr = 0.135 * dx
    H = np.full((res, res), -wr * 1.3, dtype=np.float32)
    for j in range(2 * cols):
        par = j % 2
        sg = 1.0 if par else -1.0
        for i in range(cols):
            xc = (i + 0.5 * par + 0.25) * dx
            yc = (j + 0.5) * dy

            def fn(X, Y):
                r = np.hypot(X, Y / 0.82)
                d = np.abs(r - R)
                inside = d < wr
                z = np.sqrt(np.clip(1 - (d / wr) ** 2, 0, 1)) * wr + sg * 0.42 * Y * 0.5 + 0.10 * wr * np.cos(np.arctan2(Y, X) * 2)
                return inside, z.astype(np.float32)
            _stamp(H, (), xc, yc, int(R + wr) + 2, fn)
    zn = (H - H.min()) / (H.max() - H.min())
    det = np.repeat((0.30 + 1.25 * zn ** 1.5)[:, :, None], 3, axis=2)
    det = det / det.mean()
    hgt = (H - H.mean()) / res
    rough = np.clip(0.72 - 0.38 * zn, 0, 1)
    aux = np.stack([np.clip(1.0 - zn * 1.6, 0, 1), zn], axis=2)
    return save_npz(alias, det, hgt, rough, aux, dict(id=alias, size_m=CT.SETS[alias]['size'], mean_albedo=[0.5, 0.5, 0.5]))


def prep(alias, force=False):
    if os.path.isfile(CT.cache_path(alias)) and not force:
        return None
    s = CT.SETS[alias]
    res = s.get('res', CT.RES)
    if s['kind'] != 'syn':
        return prep_scan(alias)
    if alias == 'fur':
        return syn_fur(alias, res, 2200, (34, 62), (6, 11), 7, strands=2.0, lift=1.6, spread=0.24)
    if alias == 'fur_long':
        return syn_fur(alias, res, 1300, (60, 112), (8, 15), 11, strands=1.6, lift=1.9, spread=0.30)
    if alias == 'mail':
        return syn_mail(alias, res, 12)
    log("unknown synth set", alias)


def sheet(aliases, path, tile=256, cols=3):
    cells = []
    for a in aliases:
        if not CT.available(a):
            continue
        d = CT.load(a)
        res = d['h'].shape[0]

        def fit(x):
            idx = (np.arange(tile) * res // tile)
            return x[idx][:, idx]
        mean = np.array(d['meta'].get('mean_albedo', (0.5, 0.5, 0.5)))
        det = np.clip(fit(d['det']) * mean[None, None, :], 0, 1) ** (1 / 2.2)
        h = np.clip(0.5 + fit(d['h']) / 6.0, 0, 1)
        r = fit(d['rough'])
        cells.append(np.concatenate([det, np.repeat(h[:, :, None], 3, 2), np.repeat(r[:, :, None], 3, 2)], axis=1))
    while len(cells) % cols:
        cells.append(np.zeros_like(cells[0]))
    lines = [np.concatenate(cells[i:i + cols], axis=1) for i in range(0, len(cells), cols)]
    img = np.concatenate(lines[::-1], axis=0)
    hh, ww = img.shape[:2]
    im = bpy.data.images.new("sheet", ww, hh, alpha=False)
    im.colorspace_settings.name = 'Non-Color'
    px = np.ones((hh, ww, 4), dtype=np.float32)
    px[:, :, :3] = img
    im.pixels.foreach_set(px.ravel())
    im.filepath_raw = path
    im.file_format = 'JPEG'
    im.save()
    log("sheet", path, "order (row-major, 3 per row):", [a for a in aliases if CT.available(a)])


args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
args = [('--' + a) if a in ('sheet', 'force') else a for a in args]      # PowerShell -File swallows leading dashes
force = '--force' in args
names = [a for a in args if not a.startswith('--')] or list(CT.SETS)
for a in names:
    try:
        prep(a, force)
    except Exception as e:
        import traceback
        traceback.print_exc()
        log("FAILED", a, e)
if '--sheet' in args:
    sheet(list(CT.SETS), r"Y:\shots\chars\_chartex_sheet.jpg")
log("PREP DONE", len([a for a in CT.SETS if CT.available(a)]), "of", len(CT.SETS))
