r"""Turns the Blender renders into game textures: seamless nine-slices, drop shadows, glows, field tiles, a 1x set
and the manifest the game reads.

   python.exe -I Y:\tools\uiart\slice.py --style A [--pieces a,b|explore|all] [--out Y:\game\assets\ui] [--src Y:\assets_src\ui_gen\render]

(python.exe = Blender's bundled interpreter: C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe;
numpy comes with it, Pillow lives in %LOCALAPPDATA%\AshenAbyssTools\pylibs.)

Output: <out>\x2\<piece>.png (2 texture px per design px), <out>\x1\<piece>.png (exactly half), <out>\manifest.json.
All numbers in the manifest are x2 texture px.
"""
import sys, os, json, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
import numpy as np
from PIL import Image, ImageFilter
import pieces as PC

argv = sys.argv[1:]
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default


# ───────────────────────── image helpers (premultiplied float RGBA, sRGB values) ─────────────────────────
def load(path):
    im = Image.open(path)
    if im.mode == 'I;16' or im.mode == 'I':
        raise SystemExit("unexpected single-channel image " + path)
    a = np.asarray(im)
    if a.dtype == np.uint16: a = a.astype(np.float32) / 65535.0
    else: a = a.astype(np.float32) / 255.0
    if a.shape[2] == 3: a = np.dstack([a, np.ones(a.shape[:2], np.float32)])
    a = a.copy()
    a[..., :3] *= a[..., 3:4]
    return a


def save(a, path):
    a = np.clip(a, 0.0, 1.0)
    al = a[..., 3:4]
    rgb = np.where(al > 1e-5, a[..., :3] / np.maximum(al, 1e-5), 0.0)
    out = np.dstack([np.clip(rgb, 0, 1), al])
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), 'RGBA').save(path, optimize=True)


def blur(ch, r):
    """Gaussian blur of one float channel (sigma = r), edges clamped."""
    if r <= 0: return ch
    n = int(math.ceil(r * 3.0))
    x = np.arange(-n, n + 1, dtype=np.float32)
    k = np.exp(-(x * x) / (2.0 * r * r)); k /= k.sum()
    out = ch.astype(np.float32)
    for axis in (0, 1):
        pad = [(0, 0), (0, 0)]; pad[axis] = (n, n)
        t = np.pad(out, pad, mode='edge')
        acc = np.zeros_like(out)
        L = out.shape[axis]
        for i in range(2 * n + 1):
            acc += k[i] * (t[i:i + L] if axis == 0 else t[:, i:i + L])
        out = acc
    return out


def half(a):
    h, w = a.shape[:2]
    chans = []
    for c in range(4):
        im = Image.fromarray(a[..., c].astype(np.float32), 'F').resize((w // 2, h // 2), Image.LANCZOS)
        chans.append(np.asarray(im, dtype=np.float32))
    return np.clip(np.dstack(chans), 0, 1)


def under(top, bottom):
    """top over bottom, both premultiplied."""
    return top + bottom * (1.0 - top[..., 3:4])


def shadow(a, spec, src_alpha=None):
    """Soft drop shadow under the piece. spec = (blur, dx, dy, opacity)."""
    r, dx, dy, op = spec
    al = a[..., 3] if src_alpha is None else src_alpha
    s = blur(al, r)
    s = np.roll(np.roll(s, int(dy), 0), int(dx), 1)
    sh = np.zeros_like(a)
    sh[..., 3] = np.clip(s * op, 0, 1)
    return under(a, sh)


def glow(a, spec):
    """Soft halo in the piece's own colour (for self-lit overlays). spec = (radius, strength)."""
    r, k = spec
    al = blur(a[..., 3], r) * k
    col = a[..., :3].sum(axis=(0, 1)) / max(1e-6, a[..., 3].sum())
    g = np.zeros_like(a)
    al = np.clip(al, 0, 1)
    g[..., :3] = col[None, None, :] * al[..., None]
    g[..., 3] = al
    return under(a, g)


def seam_axis(a, lo, period, blend, axis):
    """Render with two periods along `axis` starting at `lo` -> array with ONE seamless period.
    The tile is cross-faded into its own continuation, and both neighbours (corner blend zones) are eased into the
    tiled content, so corner / tile / corner join without a visible cut."""
    a = np.moveaxis(a, axis, 0)
    n = a.shape[0]
    hi = n - (lo + 2 * period)                       # size of the far margin
    S = a[lo:lo + 2 * period]
    w = np.clip(np.arange(period, dtype=np.float32) / max(1, blend), 0, 1)
    w = (w * w * (3 - 2 * w)).reshape((period,) + (1,) * (a.ndim - 1))
    T = S[:period] * w + S[period:] * (1 - w)        # T[0] = S[P], T[P-1] = S[P-1]
    near = a[:lo].copy()
    far = a[lo + 2 * period:].copy()
    b = min(blend, lo, hi)
    if b > 0:
        t = (np.arange(b, dtype=np.float32) + 0.5) / b
        t = (t * t * (3 - 2 * t)).reshape((b,) + (1,) * (a.ndim - 1))
        # near margin: last b rows ease from the render into "one period later" (which is what the tile starts with)
        near[lo - b:] = a[lo - b:lo] * (1 - t) + a[lo - b + period:lo + period] * t
        # far margin: first b rows ease from "the continuation of the tile" into the render
        far[:b] = a[lo + period:lo + period + b] * (1 - t) + a[lo + 2 * period:lo + 2 * period + b] * t
    out = np.concatenate([near, T, far], axis=0)
    return np.moveaxis(out, 0, axis)


# ───────────────────────── field tiles from the scanned CC0 textures ─────────────────────────
def scan_path(alias, kind):
    names = {'color': ("_Color.jpg", "diff_1k.jpg"), 'disp': ("_Displacement.jpg", "disp_1k.jpg")}[kind]
    for root in (r"Y:\assets_src\montex", r"Y:\assets_src\chartex"):
        d = os.path.join(root, alias)
        if not os.path.isdir(d): continue
        for f in os.listdir(d):
            if f.endswith(names[0]) or f == names[1]: return os.path.join(d, f)
    env = os.path.join(r"Y:\game\assets\env\tex", alias)
    f = {'color': 'albedo.jpg', 'disp': 'height.jpg'}[kind]
    if os.path.exists(os.path.join(env, f)): return os.path.join(env, f)
    raise FileNotFoundError(alias + " " + kind)


def wrap_blur(ch, r):
    p = int(r * 3) + 1
    t = np.pad(ch, p, mode='wrap')
    return blur(t, r)[p:-p, p:-p]


def field_tile(alias, dark, light, size=512, contrast=0.55, relief=0.35, stain=0.25, seed=3, rot=False):
    """Quiet, seamless panel field: scan luminance mapped between two dark tones, a little top-left relief light
    from the height map, low-frequency stains. dark/light: sRGB 0-255."""
    col = np.asarray(Image.open(scan_path(alias, 'color')).convert('L').resize((size, size), Image.LANCZOS), dtype=np.float32) / 255.0
    try:
        hgt = np.asarray(Image.open(scan_path(alias, 'disp')).convert('L').resize((size, size), Image.LANCZOS), dtype=np.float32) / 255.0
    except FileNotFoundError:
        hgt = col
    if rot:
        col = np.rot90(col).copy(); hgt = np.rot90(hgt).copy()
    z = (col - wrap_blur(col, 40)) / (col.std() + 1e-6)            # local contrast only: no big blotches from the scan
    v = 0.5 + contrast * 0.22 * z
    hs = wrap_blur(hgt, 1.2)
    gx = np.roll(hs, -1, 1) - np.roll(hs, 1, 1)
    gy = np.roll(hs, -1, 0) - np.roll(hs, 1, 0)
    lit = -(gx + gy) * 0.5                                           # light from the top-left
    v += relief * 6.0 * lit
    rng = np.random.default_rng(seed)
    st = wrap_blur(rng.random((size, size)).astype(np.float32), 46)
    st = (st - st.mean()) / (st.std() + 1e-6)
    v += stain * 0.10 * st
    v = np.clip(v, 0, 1)[..., None]
    d = np.array(dark, np.float32) / 255.0; l = np.array(light, np.float32) / 255.0
    rgb = d[None, None, :] * (1 - v) + l[None, None, :] * v
    return np.dstack([rgb, np.ones((size, size, 1), np.float32)])


FIELDS = {
    # name: (scan, dark, light, kwargs)
    'slate': ('dark_rock', (15, 14, 16), (33, 31, 33), dict(contrast=0.5, relief=0.30)),
    'soot': ('Metal038', (9, 9, 10), (19, 18, 19), dict(contrast=0.5, relief=0.2, stain=0.3)),
    'well': ('dark_rock', (6, 6, 7), (15, 14, 15), dict(contrast=0.4, relief=0.2, seed=7, rot=True)),
    'parchment': ('leather_white', (33, 29, 25), (52, 46, 39), dict(contrast=0.6, relief=0.5, stain=0.9, seed=5)),
    'parchment_dark': ('leather_white', (20, 18, 16), (33, 29, 25), dict(contrast=0.6, relief=0.4, stain=0.8, seed=6)),
    'leather': ('Leather014', (17, 12, 10), (40, 28, 22), dict(contrast=0.7, relief=0.5, stain=0.4)),
    'leather_dark': ('Leather014', (10, 8, 7), (25, 18, 15), dict(contrast=0.6, relief=0.4, stain=0.4, seed=9)),
}


# ───────────────────────── per-kind processing ─────────────────────────
def bloom(a, radius=7.0, strength=0.6):
    """Faint warm halo round the ember stones and ember-lit lines (orange-hot pixels only; gold does not count)."""
    al = a[..., 3]
    rgb = np.where(al[..., None] > 1e-4, a[..., :3] / np.maximum(al[..., None], 1e-4), 0.0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hot = np.clip((r - 0.62) / 0.3, 0, 1) * np.clip((0.66 - g / (r + 1e-4)) / 0.2, 0, 1) * np.clip((r - b - 0.4) / 0.3, 0, 1) * al
    if hot.max() < 0.08: return a
    add = blur(hot, radius) * strength + blur(hot, radius * 0.35) * strength * 0.5
    col = np.array((1.0, 0.42, 0.10), np.float32)
    out = a.copy()
    out[..., :3] = a[..., :3] + col[None, None, :] * add[..., None] * (1.0 - 0.6 * a[..., :3])
    out[..., 3] = np.clip(np.maximum(al, add * 0.85), 0, 1)
    return out


def process(m, src_dir):
    a = load(os.path.join(src_dir, m['name'] + ".png"))
    assert a.shape[0] == m['h'] and a.shape[1] == m['w'], (m['name'], a.shape, m['w'], m['h'])
    info = dict(kind=m['kind'], pad=m['pad'])
    if m.get('backfill'):
        # translucent veil inside the logical box (HUD plaques): under the render
        r, g, b, al = m['backfill']
        i = m['pad'] + m.get('fill_inset', 0)
        fill = np.zeros_like(a)
        fill[i:a.shape[0] - i, i:a.shape[1] - i] = (r * al, g * al, b * al, al)
        a = under(a, fill)
    if m['kind'] == 'nine':
        a = seam_axis(a, m['ml'], m['px'], m['blend'], 1)
        a = seam_axis(a, m['mt'], m['py'], m['blend'], 0)
        info.update(m=[m['ml'], m['mt'], m['mr'], m['mb']], center=bool(m.get('center')))
    elif m['kind'] == 'h3':
        a = seam_axis(a, m['ml'], m['px'], m['blend'], 1)
        info.update(m=[m['ml'], 0, m['mr'], 0])
    elif m['kind'] == 'v3':
        a = seam_axis(a, m['mt'], m['py'], m['blend'], 0)
        info.update(kind='nine', m=[0, m['mt'], 0, m['mb']], center=True)
    if m.get('glow'): a = glow(a, m['glow'])
    if m.get('bloom', True): a = bloom(a)
    if m.get('shadow'): a = shadow(a, m['shadow'])
    for key in ('anchor', 'band', 'mip'):
        if key in m: info[key] = m[key]
    info['w'] = a.shape[1]; info['h'] = a.shape[0]
    return a, info


def main():
    style = str(opt('--style', 'A'))
    which = str(opt('--pieces', 'all'))
    out = str(opt('--out', r"Y:\game\assets\ui"))
    src = os.path.join(str(opt('--src', r"Y:\assets_src\ui_gen\render")), style)
    names = PC.EXPLORE if which == 'explore' else (PC.ORDER if which == 'all' else which.split(','))
    man_path = os.path.join(out, "manifest.json")
    man = dict(style=style, scale=2, pieces={}, tiles={})
    if os.path.exists(man_path) and which not in ('all', 'explore'):
        man = json.load(open(man_path, encoding='utf-8'))
    n = 0
    for name in names:
        m = PC.PIECES[name]
        p = os.path.join(src, name + ".png")
        if not os.path.exists(p):
            print("slice: missing render", p); continue
        a, info = process(m, src)
        save(a, os.path.join(out, "x2", name + ".png"))
        save(half(a), os.path.join(out, "x1", name + ".png"))
        man['pieces'][name] = info
        n += 1
    if not opt('--no-fields'):
        for fname, (alias, dark, light, kw) in FIELDS.items():
            t = field_tile(alias, dark, light, **kw)
            save(t, os.path.join(out, "x2", "field_" + fname + ".png"))
            save(half(t), os.path.join(out, "x1", "field_" + fname + ".png"))
            man['tiles'][fname] = dict(w=t.shape[1], h=t.shape[0])
    json.dump(man, open(man_path, 'w', encoding='utf-8'), indent=1, sort_keys=True)
    print("slice: %d pieces, %d tiles -> %s" % (n, len(man['tiles']), out))


if __name__ == '__main__':
    main()
