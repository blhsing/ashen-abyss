r"""Masters -> game cursors: dark outline + ember rim, the size set, hotspots, cursors.json.

   python.exe -I Y:\tools\cursor\post.py [--kinds a,b] [--src Y:\assets_src\cursor_gen\render] [--out Y:\game\assets\ui\cursor]

(python.exe = Blender's bundled interpreter, C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe;
it is not a Blender process. numpy comes with it, Pillow lives in %LOCALAPPDATA%\AshenAbyssTools\pylibs.)

For every size the outline and the rim are drawn at the master's resolution with widths given in FINAL pixels, then
the whole picture is reduced once, in linear light, so thin bright edges keep their brightness:
    object  over  soot outline (reads on bright scenes)  over  thin ember rim (reads on dark scenes).
Output: <out>\<kind>_<size>.png (8-bit RGBA, straight alpha) and <out>\cursors.json:
    { "design": 64, "sizes": [...], "kinds": { "<kind>": { "hot": {"32": [x, y], ...} } } }
"""
import sys, os, json, math
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
import numpy as np
from PIL import Image

argv = sys.argv[1:]
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

SIZES = [32, 40, 48, 64, 96]
LO_MAX = 48            # sizes up to this use the small-size build of the model (<kind>_lo.png)
ORDER = ['default', 'attack', 'talk', 'pickup', 'interact', 'forbid', 'ui', 'ui_hot', 'caret', 'busy']
SOOT = (10, 8, 8)
# rim colour (sRGB 0-255), strength, extra width factor
RIM = {
    'default':  ((255, 150, 60), 0.80, 1.0),
    'attack':   ((255, 64, 28), 0.95, 1.15),
    'talk':     ((255, 196, 96), 0.85, 1.0),
    'pickup':   ((255, 170, 70), 0.85, 1.0),
    'interact': ((255, 180, 80), 0.85, 1.0),
    'forbid':   ((255, 52, 34), 0.95, 1.15),
    'ui':       ((255, 170, 84), 0.78, 1.0),
    'ui_hot':   ((255, 206, 120), 1.00, 1.25),
    'caret':    ((255, 176, 90), 0.85, 1.0),
    'busy':     ((255, 140, 52), 0.85, 1.0),
}


def outline_px(size):
    """(dark outline, ember rim) widths in final px: about 1 px at 32, growing a little slower than the cursor."""
    k = (size / 32.0) ** 0.8
    return 1.0 * k, 0.85 * k


def to_lin(c):
    c = np.asarray(c, np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1.0 / 2.4) - 0.055)


def load_lin(path):
    """Premultiplied linear RGBA float."""
    a = np.asarray(Image.open(path))
    a = a.astype(np.float32) / (65535.0 if a.dtype == np.uint16 else 255.0)
    if a.shape[2] == 3: a = np.dstack([a, np.ones(a.shape[:2], np.float32)])
    rgb = to_lin(a[..., :3])
    al = a[..., 3:4]
    return np.dstack([rgb * al, al]).astype(np.float32)


def edt(mask):
    """Distance in px from every pixel to the nearest True pixel (0 inside)."""
    h, w = mask.shape
    xs = np.arange(w, dtype=np.float32)
    g = np.full((h, w), 1e6, np.float32)
    for y in range(h):
        cols = xs[mask[y]]
        if cols.size: g[y] = np.abs(xs[:, None] - cols[None, :]).min(axis=1)
    g2 = g * g
    d2 = np.full((h, w), 1e12, np.float32)
    ys = np.arange(h, dtype=np.float32)
    for yy in range(h):
        dy = ys - yy
        np.minimum(d2, g2[yy][None, :] + (dy * dy)[:, None], out=d2)
    return np.sqrt(d2)


def resize(a, size):
    chans = []
    for c in range(a.shape[2]):
        im = Image.fromarray(a[..., c].astype(np.float32), 'F').resize((size, size), Image.LANCZOS)
        chans.append(np.asarray(im, dtype=np.float32))
    out = np.dstack(chans)
    out[..., 3] = np.clip(out[..., 3], 0.0, 1.0)
    out[..., :3] = np.clip(out[..., :3], 0.0, out[..., 3:4])
    return out


def blur(ch, r):
    if r <= 0: return ch
    n = int(math.ceil(r * 3.0))
    x = np.arange(-n, n + 1, dtype=np.float32)
    k = np.exp(-(x * x) / (2.0 * r * r)); k /= k.sum()
    out = ch.astype(np.float32)
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim; pad[axis] = (n, n)
        t = np.pad(out, pad, mode='edge')
        acc = np.zeros_like(out)
        L = out.shape[axis]
        for i in range(2 * n + 1):
            acc += k[i] * (t[i:i + L] if axis == 0 else t[:, i:i + L])
        out = acc
    return out


def under(top, bottom):
    return top + bottom * (1.0 - top[..., 3:4])


def layer(alpha, rgb255):
    lin = to_lin(np.asarray(rgb255, np.float32) / 255.0)
    return np.dstack([alpha * lin[0], alpha * lin[1], alpha * lin[2], alpha]).astype(np.float32)


def make(master, dist, kind, size):
    m = master.shape[0]
    f = m / float(size)                       # master px per final px
    wo, wr = outline_px(size)
    col, strength, wide = RIM[kind]
    ro = wo * f
    rr = ro + wr * wide * f
    aa = 1.0
    a_out = np.clip((ro + 0.5 - dist) / aa, 0.0, 1.0) * 0.97
    soft = 0.55 * f                           # the rim fades over about half a final pixel
    a_rim = np.clip((rr + 0.5 - dist) / soft, 0.0, 1.0)
    a_rim = a_rim * a_rim * (3.0 - 2.0 * a_rim) * strength
    comp = under(master, under(layer(a_out, SOOT), layer(a_rim, col)))
    small = resize(comp, size)
    # a touch of unsharp mask on the small sizes (colour only, alpha untouched)
    amt = 0.45 if size <= 32 else 0.36 if size <= 40 else 0.30 if size <= 48 else 0.15 if size <= 64 else 0.0
    if amt > 0:
        # brightness only: sharpening the channels one by one draws complementary fringes along the coloured rim
        rgb = small[..., :3]
        y = rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722
        gain = np.clip((y + (y - blur(y, 0.7)) * amt) / np.maximum(y, 1e-4), 0.5, 1.8)
        small[..., :3] = np.clip(rgb * gain[..., None], 0.0, small[..., 3:4])
    return small


def save(a, path):
    al = np.clip(a[..., 3:4], 0.0, 1.0)
    rgb = np.where(al > 1e-5, a[..., :3] / np.maximum(al, 1e-5), 0.0)
    out = np.dstack([to_srgb(rgb), al])
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), 'RGBA').save(path, optimize=True)


def main():
    src = str(opt('--src', r"Y:\assets_src\cursor_gen\render"))
    out = str(opt('--out', r"Y:\game\assets\ui\cursor"))
    which = opt('--kinds', None)
    kinds = which.split(',') if isinstance(which, str) else ORDER
    os.makedirs(out, exist_ok=True)
    man_path = os.path.join(out, "cursors.json")
    man = {"design": 64, "sizes": SIZES, "kinds": {}}
    if os.path.exists(man_path):
        try:
            old = json.load(open(man_path, encoding="utf-8"))
            if old.get("sizes") == SIZES: man["kinds"] = old.get("kinds", {})
        except Exception as e:
            print("post: old manifest unreadable:", e)
    lo_max = int(opt('--lo-max', LO_MAX))
    for kind in kinds:
        cache = {}
        hots = {}
        for size in SIZES:
            tag = kind + ("_lo" if size <= lo_max and os.path.exists(os.path.join(src, kind + "_lo.png")) else "")
            if tag not in cache:
                p = os.path.join(src, tag + ".png")
                if not os.path.exists(p):
                    print("post: MISSING", p); cache[tag] = None
                else:
                    master = load_lin(p)
                    cache[tag] = (master, edt(master[..., 3] >= 0.5), json.load(open(os.path.join(src, tag + ".json"), encoding="utf-8")))
            if cache[tag] is None: continue
            master, dist, meta = cache[tag]
            img = make(master, dist, kind, size)
            save(img, os.path.join(out, "%s_%d.png" % (kind, size)))
            k = size / float(meta["canvas"])
            hots[str(size)] = [int(min(size - 1, max(0, math.floor(meta["hot"][0] * k)))), int(min(size - 1, max(0, math.floor(meta["hot"][1] * k))))]
            edge = max(img[0, :, 3].max(), img[-1, :, 3].max(), img[:, 0, 3].max(), img[:, -1, 3].max())
            if edge > 0.03: print("post: WARNING %s_%d touches the canvas edge (alpha %.2f)" % (kind, size, edge))
        if not hots: continue
        man["kinds"][kind] = {"hot": hots}
        print("post: %-9s hot %s" % (kind, " ".join("%s:%d,%d" % (s_, h[0], h[1]) for s_, h in hots.items())))
    man["kinds"] = {k: man["kinds"][k] for k in ORDER if k in man["kinds"]}
    with open(man_path, "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1)
    print("post: wrote", man_path, "kinds=%d sizes=%s" % (len(man["kinds"]), SIZES))


main()
