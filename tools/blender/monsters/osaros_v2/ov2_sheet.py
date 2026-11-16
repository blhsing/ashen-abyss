"""Contact sheet with a label on every tile (sheet.py conventions: 400 px tiles, ~46 px text, <= 3 columns).
   blender -b --factory-startup --python ov2_sheet.py -- x <out.jpg> [--tile=400] [--text=46] [--cols=3] [--title=Some_title]
            <png>|<label> <png>|<label> ...          ('_' in labels and the title = space; a tile path may be 'gap')
Tiles are area-resampled to the tile size; labels are rendered once from Blender text objects (as sheet.py does)."""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE)); sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

args = [a for a in sys.argv[sys.argv.index('--') + 1:] if a != 'x']
out_path = args[0]
fl = {a[2:].split('=', 1)[0]: a.split('=', 1)[1] for a in args[1:] if a.startswith('--') and '=' in a}
items = [a for a in args[1:] if not a.startswith('--')]
TILE = int(fl.get('tile', '400')); EM = float(fl.get('text', '46')); NC = int(fl.get('cols', '3'))
title = fl.get('title', '').replace('_', ' ')
tiles = [(it.split('|', 1)[0], it.split('|', 1)[1].replace('_', ' ') if '|' in it else '') for it in items]

PP = 96; FONT = 0.62
STRIP = int(round(EM / FONT)); GAP = 3; BG = (0.035, 0.037, 0.045)


def render_labels(strings, width_px=4800):
    M.reset_scene()
    sc = bpy.context.scene
    n = len(strings)
    mat = bpy.data.materials.new("label"); mat.use_nodes = True
    nt = mat.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs[0].default_value = (1, 1, 1, 1)
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], o.inputs[0])
    for i, s in enumerate(strings):
        cu = bpy.data.curves.new("t%d" % i, 'FONT'); cu.body = s or " "; cu.size = FONT; cu.align_x = 'LEFT'; cu.align_y = 'CENTER'
        ob = bpy.data.objects.new("t%d" % i, cu); ob.location = (0.12, -(i + 0.5), 0.0); ob.data.materials.append(mat)
        sc.collection.objects.link(ob)
    wu = width_px / PP
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    cd.type = 'ORTHO'; cd.ortho_scale = max(wu, n); cam.location = (wu / 2, -n / 2, 10.0)
    w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0, 0, 0, 1); sc.world = w
    S.setup_render(width_px, n * PP, samples=16)
    p = os.path.join(M.WORK, "_ov2_labels.png")
    S.render(p)
    a = S.load_px(p)[::-1, :, 0]
    out = []
    for i in range(n):
        st = a[i * PP:(i + 1) * PP]
        cc = np.where(st.max(axis=0) > 0.04)[0]
        out.append(st[:, :(int(cc.max()) + 10 if len(cc) else 10)].copy())
    return out


def area_weights(n_in, n_out):
    s = n_in / n_out
    W = np.zeros((n_out, n_in))
    for i in range(n_out):
        a0 = i * s; a1 = (i + 1) * s
        for j in range(int(math.floor(a0)), min(n_in - 1, int(math.ceil(a1)) - 1) + 1):
            W[i, j] = max(0.0, min(a1, j + 1) - max(a0, j))
    return W / s


def resample(a, h, w):
    Wy = area_weights(a.shape[0], h); Wx = area_weights(a.shape[1], w)
    if a.ndim == 2:
        return Wy @ a @ Wx.T
    return np.stack([Wy @ a[:, :, c] @ Wx.T for c in range(a.shape[2])], axis=2)


def to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def load_tile(path):
    if path == 'gap' or not os.path.isfile(path):
        if path != 'gap':
            M.log("MISSING", path)
        return np.full((TILE, TILE, 3), BG)
    a = S.load_px(path)[::-1, :, :3].astype(np.float64)
    h, w = a.shape[:2]
    if h != w:                                   # centre-crop to a square
        m = min(h, w); a = a[(h - m) // 2:(h - m) // 2 + m, (w - m) // 2:(w - m) // 2 + m]
    return a if a.shape[0] == TILE else to_srgb(resample(to_lin(a), TILE, TILE))


def paste_text(canvas, strip, x, y, h, color, max_w, box=None):
    if strip.shape[1] * h / strip.shape[0] > max_w:
        h2 = max(12, int(max_w * strip.shape[0] / strip.shape[1])); y += (h - h2) // 2; h = h2
    st = np.clip(resample(strip.astype(np.float64), h, max(1, int(round(strip.shape[1] * h / strip.shape[0])))), 0, 1)
    hh, ww = st.shape
    ww = min(ww, canvas.shape[1] - x)
    if box is not None:
        canvas[y - 4:y + hh + 4, x - 6:x + ww + 6] *= box
    reg = canvas[y:y + hh, x:x + ww]
    al = st[:reg.shape[0], :ww, None]
    reg[:] = reg * (1 - al) + np.array(color)[None, None, :] * al


strips = render_labels([title or " "] + [t[1] for t in tiles])
nrow = (len(tiles) + NC - 1) // NC
W = NC * TILE + (NC - 1) * GAP
TB = (STRIP + 24) if title else 0
H = TB + nrow * TILE + (nrow - 1) * GAP
cv = np.zeros((H, W, 3)); cv[:] = BG
if title:
    cv[:TB] = (0.11, 0.11, 0.13)
    paste_text(cv, strips[0], 10, 12, STRIP, (1.0, 1.0, 1.0), W - 20)
for i, (p, lab) in enumerate(tiles):
    r, c = divmod(i, NC)
    x = c * (TILE + GAP); y = TB + r * (TILE + GAP)
    cv[y:y + TILE, x:x + TILE] = load_tile(p)
    if lab:
        paste_text(cv, strips[1 + i], x + 10, y + 8, STRIP, (1.0, 0.72, 0.25), TILE - 20, box=0.25)
img = np.clip(cv, 0, 1)
im = bpy.data.images.new("sheet", W, H, alpha=False)
px = np.ones((H, W, 4), dtype=np.float32); px[:, :, :3] = img[::-1]
im.pixels.foreach_set(px.ravel())
im.filepath_raw = out_path
im.file_format = 'JPEG'
bpy.context.scene.render.image_settings.quality = 92
im.save()
M.log("SHEET OK", out_path, "%dx%d" % (W, H), "tiles", len(tiles), "text em %d px" % EM)
