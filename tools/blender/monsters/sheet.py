"""Before / after contact sheet of the raking-light close-ups written by closeup.py (ba.ps1 renders the pairs):
   blender -b --factory-startup --python sheet.py -- <out.jpg> [--tile=400] [--text=46] [--title=Some_title]
            [--cols=bust_v1:BEFORE,bust:AFTER,{z}:AFTER_zoom]  id[:note[:zoom]] id[:note[:zoom]] ...
One row per creature, one column per --cols entry  <shot tag>[@]:<label>  (at most 3 columns keeps it readable):
   the tile is Y:\\shots\\chars\\<id>_<shot tag>.png, area-resampled to the tile size, or with '@' the centre of the
   render cropped at its native pixels (a true zoom: nothing is rescaled).  '{z}' in a shot tag is replaced by the
   row's own zoom shot (third field of the row, default 'mat@'), e.g.  mon_husk:dead_flesh:face  or  mon_skeleton::bust@
Text ('_' = space) is about --text px high (em) on --tile px tiles: a name band above every row, the column label on
the top-left corner of every tile.  Labels are rendered once from Blender text objects and pasted with numpy."""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

args = sys.argv[sys.argv.index('--') + 1:]
out_path = args[0]
flags = [a for a in args[1:] if a.startswith('--')]
items = [a for a in args[1:] if not a.startswith('--')]


def flag(name, default):
    for f in flags:
        if f.startswith('--' + name + '='):
            return f.split('=', 1)[1]
    return default


TILE = int(flag('tile', '400'))
EM = float(flag('text', '46'))
title = flag('title', '').replace('_', ' ')
cols = []
for c in flag('cols', 'bust_v1:BEFORE,bust:AFTER,{z}:AFTER_zoom').split(','):
    tag, _, lab = c.partition(':')
    crop = tag.endswith('@')
    cols.append((tag.rstrip('@'), lab.replace('_', ' '), crop))
rows = []
for it in items:
    q = it.split(':')
    rows.append((q[0], (q[1] if len(q) > 1 else '').replace('_', ' '), q[2] if len(q) > 2 and q[2] else 'mat@'))

PP = 96                    # label render: pixels per text row
FONT = 0.62                # font size in rows -> em = FONT * PP px in the strip
STRIP = int(round(EM / FONT))          # strip height that gives an em of EM px
BAND = STRIP + 10          # name band above each row
GAP = 3                    # dark line between tiles
BG = (0.035, 0.037, 0.045)


def render_labels(strings, width_px=4800):
    """one EEVEE render with every string on its own row -> list of alpha strips (PP, w), top-down"""
    M.reset_scene()
    sc = bpy.context.scene
    n = len(strings)
    mat = bpy.data.materials.new("label")
    mat.use_nodes = True
    nt = mat.node_tree
    for nd in list(nt.nodes):
        nt.nodes.remove(nd)
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs[0].default_value = (1, 1, 1, 1); em.inputs[1].default_value = 1.0
    o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], o.inputs[0])
    for i, s in enumerate(strings):
        cu = bpy.data.curves.new("t%d" % i, 'FONT')
        cu.body = s
        cu.size = FONT
        cu.align_x = 'LEFT'
        cu.align_y = 'CENTER'
        ob = bpy.data.objects.new("t%d" % i, cu)
        ob.location = (0.12, -(i + 0.5), 0.0)
        ob.data.materials.append(mat)
        sc.collection.objects.link(ob)
    wu = width_px / PP
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    cd.type = 'ORTHO'
    cd.ortho_scale = max(wu, n)
    cam.location = (wu / 2, -n / 2, 10.0)
    world = bpy.data.worlds.new("w"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0, 0, 0, 1)
    sc.world = world
    S.setup_render(width_px, n * PP, samples=16)
    p = os.path.join(M.WORK, "_labels.png")
    S.render(p)
    a = S.load_px(p)[::-1, :, 0]          # top-down
    strips = []
    for i in range(n):
        st = a[i * PP:(i + 1) * PP]
        cc = np.where(st.max(axis=0) > 0.04)[0]
        w = int(cc.max()) + 10 if len(cc) else 10
        strips.append(st[:, :w].copy())
    return strips


def area_weights(n_in, n_out):
    s = n_in / n_out
    W = np.zeros((n_out, n_in), dtype=np.float64)
    for i in range(n_out):
        a0 = i * s; a1 = (i + 1) * s
        for j in range(int(math.floor(a0)), min(n_in - 1, int(math.ceil(a1)) - 1) + 1):
            W[i, j] = max(0.0, min(a1, j + 1) - max(a0, j))
    return W / s


def resample(a, h, w):
    """exact area (box) resampling of (H, W[, C]) to (h, w)"""
    Wy = area_weights(a.shape[0], h); Wx = area_weights(a.shape[1], w)
    if a.ndim == 2:
        return Wy @ a @ Wx.T
    return np.stack([Wy @ a[:, :, c] @ Wx.T for c in range(a.shape[2])], axis=2)


def to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def load_tile(path, crop):
    if not os.path.isfile(path):
        M.log("MISSING", path)
        return np.full((TILE, TILE, 3), 0.1, dtype=np.float64)
    a = S.load_px(path)[::-1, :, :3].astype(np.float64)       # top-down, sRGB-encoded
    h, w = a.shape[:2]
    if crop and h >= TILE and w >= TILE:
        y0 = (h - TILE) // 2; x0 = (w - TILE) // 2
        return a[y0:y0 + TILE, x0:x0 + TILE]
    if h == TILE and w == TILE:
        return a
    return to_srgb(resample(to_lin(a), TILE, TILE))


def paste_text(canvas, strip, x, y, h, color, max_w, box=None):
    """strip alpha -> canvas (top-down) with its top-left at (x, y), strip height h (shrunk only if wider than max_w)"""
    if strip.shape[1] * h / strip.shape[0] > max_w:
        h2 = max(12, int(max_w * strip.shape[0] / strip.shape[1]))
        y += (h - h2) // 2
        h = h2
    st = np.clip(resample(strip.astype(np.float64), h, max(1, int(round(strip.shape[1] * h / strip.shape[0])))), 0, 1)
    hh, ww = st.shape
    ww = min(ww, canvas.shape[1] - x)
    if box is not None:
        canvas[y - 4:y + hh + 4, x - 6:x + ww + 6] *= box
    reg = canvas[y:y + hh, x:x + ww]
    al = st[:reg.shape[0], :ww, None]
    reg[:] = reg * (1 - al) + np.array(color, dtype=np.float64)[None, None, :] * al


strings = [title or " "] + [c[1] for c in cols] + ["%s  -  %s" % (cid, note) if note else cid for cid, note, _z in rows]
strips = render_labels(strings)
ncol = len(cols)
W = ncol * TILE + (ncol - 1) * GAP
TB = (BAND + 14) if title else 0
Htot = TB + len(rows) * (BAND + TILE)
canvas = np.zeros((Htot, W, 3), dtype=np.float64)
canvas[:] = BG
if title:
    canvas[:TB] = (0.11, 0.11, 0.13)
    paste_text(canvas, strips[0], 10, 7, STRIP, (1.0, 1.0, 1.0), W - 20)
y = TB
for r, (cid, note, zoom) in enumerate(rows):
    canvas[y:y + BAND] = (0.17, 0.135, 0.09)
    paste_text(canvas, strips[1 + ncol + r], 10, y + 5, STRIP, (1.0, 0.87, 0.6), W - 20)
    y += BAND
    for k, (tag, lab, crop) in enumerate(cols):
        x = k * (TILE + GAP)
        if '{z}' in tag:
            crop = crop or zoom.endswith('@')
            tag = tag.replace('{z}', zoom.rstrip('@'))
        canvas[y:y + TILE, x:x + TILE] = load_tile(os.path.join(M.SHOT_DIR, "%s_%s.png" % (cid, tag)), crop)
        col = (0.88, 0.90, 0.94) if lab.upper().startswith('BEFORE') else (1.0, 0.70, 0.22)
        paste_text(canvas, strips[1 + k], x + 8, y + 6, STRIP, col, TILE - 16, box=0.22)
    y += TILE

img = np.clip(canvas, 0, 1)
ok = False
try:
    import OpenImageIO as oiio
    a8 = np.ascontiguousarray((img * 255.0 + 0.5).astype(np.uint8))
    spec = oiio.ImageSpec(W, Htot, 3, 'uint8')
    spec.attribute('Compression', 'jpeg:93')
    spec.attribute('jpeg:subsampling', '4:4:4')
    o = oiio.ImageOutput.create(out_path)
    ok = bool(o and o.open(out_path, spec) and o.write_image(a8))
    if o:
        o.close()
except Exception as e:
    M.log("OIIO not usable for the sheet:", e)
if not ok:
    im = bpy.data.images.new("sheet", W, Htot, alpha=False)
    px = np.ones((Htot, W, 4), dtype=np.float32)
    px[:, :, :3] = img[::-1]
    im.pixels.foreach_set(px.ravel())
    im.filepath_raw = out_path
    im.file_format = 'JPEG'
    im.save()
M.log("SHEET OK", out_path, "%dx%d" % (W, Htot), "rows", len(rows), "cols", ncol, "text em %d px (strip %d px)" % (EM, STRIP))
