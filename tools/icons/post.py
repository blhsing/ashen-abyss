"""Icon post-processing: raw Blender renders and image-generation sheets -> game masters.

Run with Blender's bundled Python (no Blender process is started):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\5.2\\python\\bin\\python.exe" -I Y:\\tools\\icons\\post.py render [category|id ...]
  ... post.py sheets [sheet id ...]        (slices Y:\\assets_src\\icons_gen\\sheets\\<id>.png using its <id>.json)
  ... post.py single <png> <category> <id> (one generated picture on black -> one master)

Masters: Y:\\game\\assets\\icons\\r\\<category>\\<id>.png, 256x256 RGBA (512 for categories in BIG), subject centred,
longest side = FILL of the frame, straight alpha, same glow / grade for both sources (STYLE.md).
"""
import sys, os, json, glob
sys.path.insert(0, os.path.join(os.environ.get("LOCALAPPDATA", ""), "AshenAbyssTools", "pylibs"))
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

RAW = r"Y:\assets_src\icons_render"
GEN = r"Y:\assets_src\icons_gen"
OUT = r"Y:\game\assets\icons\r"
BIG = {"crest": 512}
FILL = 0.86


def to_f(im):
    return np.asarray(im.convert("RGBA"), dtype=np.float32) / 255.0


def to_im(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255.0 + 0.5).astype(np.uint8), "RGBA")


def blur(a, radius):
    """gaussian blur of a float HxW or HxWxC array"""
    if a.ndim == 2:
        return np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius)), dtype=np.float32) / 255.0
    return np.stack([blur(a[:, :, i], radius) for i in range(a.shape[2])], axis=2)


def bloom(a, thresh=0.80, radius=0.022, gain=0.9):
    """soft glow round the hottest pixels; it also extends the alpha so glows fade into the slot instead of being cut"""
    h = a.shape[0]
    rgb = a[:, :, :3] * a[:, :, 3:4]
    lum = rgb.max(axis=2)
    hot = np.clip((lum - thresh) / (1.0 - thresh), 0, 1)[:, :, None] * rgb
    g = blur(hot, h * radius) * gain + blur(hot, h * radius * 3.0) * gain * 0.6
    out_rgb = 1.0 - (1.0 - np.clip(rgb, 0, 1)) * (1.0 - np.clip(g, 0, 1))        # screen
    al = np.maximum(a[:, :, 3], np.clip(g.max(axis=2) * 1.6, 0, 1))
    res = np.zeros_like(a)
    res[:, :, :3] = out_rgb / np.maximum(al[:, :, None], 1e-4)
    res[:, :, 3] = al
    return np.clip(res, 0, 1)


def grade(a, contrast=1.08, sat=1.06, lift=0.0):
    rgb = a[:, :, :3]
    l = (rgb * np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).sum(axis=2, keepdims=True)
    rgb = l + (rgb - l) * sat
    rgb = (rgb - 0.5) * contrast + 0.5 + lift
    out = a.copy(); out[:, :, :3] = np.clip(rgb, 0, 1)
    return out


def fit(a, size, fill=FILL, thr=0.035):
    """crop to the subject, scale its longest side to fill*size, centre on a transparent square (premultiplied resize)"""
    al = a[:, :, 3]
    ys, xs = np.where(al > thr)
    if len(xs) == 0:
        return np.zeros((size, size, 4), dtype=np.float32)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    c = a[y0:y1, x0:x1].copy()
    c[:, :, :3] *= c[:, :, 3:4]
    w, h = x1 - x0, y1 - y0
    k = fill * size / max(w, h)
    nw, nh = max(1, int(round(w * k))), max(1, int(round(h * k)))
    chans = [np.asarray(Image.fromarray(c[:, :, i]).resize((nw, nh), Image.LANCZOS), dtype=np.float32) for i in range(4)]
    r = np.clip(np.stack(chans, axis=2), 0, 1)
    out = np.zeros((size, size, 4), dtype=np.float32)
    ox, oy = (size - nw) // 2, (size - nh) // 2
    out[oy:oy + nh, ox:ox + nw] = r
    al2 = out[:, :, 3:4]
    out[:, :, :3] = np.where(al2 > 1e-4, out[:, :, :3] / np.maximum(al2, 1e-4), 0)
    return np.clip(out, 0, 1)


def outline(a, radius=0.012, strength=0.9):
    """soot-black halo round the silhouette: map markers sit on bright terrain as well as on dark panels"""
    h = a.shape[0]
    halo = np.clip(blur(a[:, :, 3], h * radius) * 2.6, 0, 1) * strength
    al = a[:, :, 3]
    out = np.zeros_like(a)
    oa = al + halo * (1 - al)
    out[:, :, :3] = a[:, :, :3] * (al / np.maximum(oa, 1e-4))[:, :, None]
    out[:, :, 3] = oa
    return out


OUTLINED = ('marker',)


def sharpen(a, amount=0.35):
    rgb = a[:, :, :3]
    out = a.copy()
    out[:, :, :3] = np.clip(rgb + (rgb - blur(rgb, 0.8)) * amount, 0, 1)
    return out


def save(a, cat, iid):
    size = BIG.get(cat, 256)
    p = os.path.join(OUT, cat, iid + ".png")
    os.makedirs(os.path.dirname(p), exist_ok=True)
    to_im(sharpen(fit(a, size))).save(p, optimize=True)
    return p


# ------------------------------------------------------------------------------------------------ Blender renders
def do_render(sel):
    n = 0
    for p in sorted(glob.glob(os.path.join(RAW, "*", "*.png"))):
        cat = os.path.basename(os.path.dirname(p)); iid = os.path.splitext(os.path.basename(p))[0]
        if sel and not (cat in sel or iid in sel or any(s.endswith("*") and iid.startswith(s[:-1]) for s in sel)):
            continue
        a = to_f(Image.open(p))
        a = grade(bloom(a))
        if cat in OUTLINED:
            a = outline(a)
        save(a, cat, iid); n += 1
    print("post: %d renders -> masters" % n)


# ------------------------------------------------------------------------------------------------ generated pictures
def alpha_from_black(rgb, lo=0.035, hi=0.21):
    """cut-out of a picture painted on pure black: alpha from brightness, enclosed dark areas stay opaque"""
    m = rgb.max(axis=2)
    a = np.clip((m - lo) / (hi - lo), 0, 1)
    a = a * a * (3 - 2 * a)
    solid = (blur(m, 1.2) > 0.10).astype(np.uint8) * 255
    pad = np.zeros((solid.shape[0] + 2, solid.shape[1] + 2), dtype=np.uint8); pad[1:-1, 1:-1] = solid
    im = Image.fromarray(pad, "L").copy()
    im.load()
    ImageDraw.floodfill(im, (0, 0), 128)
    inside = (np.asarray(im)[1:-1, 1:-1] == 0).astype(np.float32)          # dark but enclosed by the subject
    inside = blur(inside, 1.0)
    al = np.maximum(a, inside)
    out = np.zeros(rgb.shape[:2] + (4,), dtype=np.float32)
    out[:, :, :3] = np.clip(rgb / np.maximum(al[:, :, None], 0.02), 0, 1)
    out[:, :, 3] = al
    return out


def components(mask):
    """4-connected labelling of a small boolean array (pure Python flood fill)"""
    h, w = mask.shape
    lab = np.zeros((h, w), dtype=np.int32)
    cur = 0
    for sy, sx in zip(*np.where(mask)):
        if lab[sy, sx]:
            continue
        cur += 1
        stack = [(sy, sx)]; lab[sy, sx] = cur
        while stack:
            y, x = stack.pop()
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not lab[ny, nx]:
                    lab[ny, nx] = cur; stack.append((ny, nx))
    return lab, cur


def slice_sheet(rgb, grid):
    """cut a grid x grid sheet painted on black into its cells; every blob goes to the cell holding its centre"""
    H, W = rgb.shape[:2]
    q = 4
    small = np.asarray(Image.fromarray((rgb.max(axis=2) * 255).astype(np.uint8), "L").resize((W // q, H // q), Image.BILINEAR), dtype=np.float32) / 255.0
    mask = blur(small, 1.5) > 0.05
    lab, n = components(mask)
    h, w = lab.shape
    owner = np.zeros(n + 1, dtype=np.int32) - 1
    boxes = {}
    for i in range(1, n + 1):
        ys, xs = np.where(lab == i)
        if len(xs) < 6:
            continue
        cy, cx = ys.mean(), xs.mean()
        cell = min(grid - 1, int(cy * grid / h)) * grid + min(grid - 1, int(cx * grid / w))
        owner[i] = cell
        b = boxes.get(cell)
        nb = [xs.min(), ys.min(), xs.max() + 1, ys.max() + 1]
        boxes[cell] = nb if b is None else [min(b[0], nb[0]), min(b[1], nb[1]), max(b[2], nb[2]), max(b[3], nb[3])]
    own_map = owner[lab]                                              # per small pixel: owning cell or -1
    own_big = np.asarray(Image.fromarray(own_map.astype(np.int16)).resize((W, H), Image.NEAREST))
    cells = {}
    for cell, b in boxes.items():
        x0, y0, x1, y1 = max(0, b[0] * q - 10), max(0, b[1] * q - 10), min(W, b[2] * q + 10), min(H, b[3] * q + 10)
        crop = rgb[y0:y1, x0:x1].copy()
        other = (own_big[y0:y1, x0:x1] >= 0) & (own_big[y0:y1, x0:x1] != cell)
        crop[other] = 0.0
        cells[cell] = crop
    return cells


def gen_master(rgb, cat, iid):
    a = alpha_from_black(rgb)
    a = grade(bloom(a, thresh=0.86, gain=0.45), contrast=1.04, sat=1.0)
    return save(a, cat, iid)


def do_sheets(sel):
    n = 0
    for jp in sorted(glob.glob(os.path.join(GEN, "sheets", "*.json"))):
        sid = os.path.splitext(os.path.basename(jp))[0]
        if sel and sid not in sel:
            continue
        meta = json.load(open(jp, encoding="utf-8"))
        png = os.path.join(GEN, "sheets", sid + ".png")
        if not os.path.isfile(png):
            continue
        rgb = np.asarray(Image.open(png).convert("RGB"), dtype=np.float32) / 255.0
        grid = int(meta.get("grid", 3))
        cells = slice_sheet(rgb, grid)
        for k, cid in enumerate(meta["cells"]):
            if not cid:
                continue
            if k not in cells:
                print("post: sheet %s cell %d (%s) is EMPTY" % (sid, k, cid)); continue
            cat, iid = cid.split("/")
            gen_master(cells[k], cat, iid); n += 1
    print("post: %d generated icons -> masters" % n)


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "render"
    rest = sys.argv[2:]
    if mode == "render":
        do_render(rest)
    elif mode == "sheets":
        do_sheets(rest)
    elif mode == "single":
        rgb = np.asarray(Image.open(rest[0]).convert("RGB"), dtype=np.float32) / 255.0
        print(gen_master(rgb, rest[1], rest[2]))
