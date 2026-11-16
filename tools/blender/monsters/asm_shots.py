"""Review tiles + family sheets of assembler creatures (one Blender run for a whole family):
   blender -b --factory-startup --python asm_shots.py -- --family=<fam> | --ids=a,b,c [--name=<sheet name>]
            [--fresh=a,b] [--tile=400] [--text=46] [--nodetail] [--perpage=3]
Per creature (cached in Y:\\shots\\chars\\asm, re-rendered when the .glb is newer or the id is in --fresh):
   <id>_game.png   game camera (pitch 52 deg, far, idle pose), same stage volume for the whole family and the creature at
                   its engine scale, so relative size and silhouette can be compared
   <id>_sil.png    the same view, flat black on grey
   <id>_bust.png   head + chest in rest pose under raking light (surface relief, floating / poking parts)
   <id>_atk.png, <id>_walk.png   the type's attack and walk clips (poses of the spec)
Sheets (tiles --tile px, label text about --text px, at most 3 columns):
   Y:\\shots\\chars\\family_<name>.jpg         one game-camera tile per creature
   Y:\\shots\\chars\\family_<name>_sil.jpg     the silhouettes
   Y:\\shots\\chars\\family_<name>_detail<k>.jpg   rows of bust | attack | walk, --perpage creatures per page"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S
import asm_specs as AS

args = sys.argv[sys.argv.index('--') + 1:]


def flag(name, default):
    for f in args:
        if f.startswith('--' + name + '='):
            return f.split('=', 1)[1]
    return default


TILE = int(flag('tile', '400'))
EM = float(flag('text', '46'))
PERPAGE = int(flag('perpage', '3'))
fam = flag('family', '')
mem = AS.members(fam) if fam else [AS.member(i) for i in flag('ids', '').split(',') if i]
name = flag('name', fam or 'custom')
fresh = set(flag('fresh', '').split(','))
OUT = os.path.join(M.SHOT_DIR, 'asm')
os.makedirs(OUT, exist_ok=True)
TAGS = ('game', 'sil', 'bust', 'atk', 'walk')
smax = max([m[1] for m in mem] + [1.0])


def tile(cid, tag):
    return os.path.join(OUT, "%s_%s.png" % (cid, tag))


def render_member(cid, scale, poses, path):
    M.reset_scene()
    acts = S.load_actions()
    arm, meshes = S.import_creature(path)
    arm.scale = (scale, scale, scale)
    S.setup_render(TILE, TILE, samples=24)
    S.setup_stage()
    cam = S.make_camera()

    def pose(s):
        a, _, t = s.partition(':')
        if a not in acts:
            M.log("MISSING action", a, "for", cid)
            S.set_pose(arm, None, 0)
        else:
            S.set_pose(arm, acts[a], float(t or 0.5))
    pose(poses[0])
    k = smax
    vol = np.array([[x * k, y * k, z * k] for x in (-0.6, 0.6) for y in (-0.6, 0.6) for z in (0.0, 2.05)])
    S.frame(cam, [], 35, 52, 100.0, pts=vol)
    S.render(tile(cid, 'game'))
    S.silhouette_mode(True)
    S.render(tile(cid, 'sil'))
    S.silhouette_mode(False)
    pose(poses[1]); S.frame(cam, meshes, 30, 14); S.render(tile(cid, 'atk'))
    pose(poses[2]); S.frame(cam, meshes, -40, 14); S.render(tile(cid, 'walk'))
    S.set_pose(arm, None, 0)
    for o in list(bpy.data.objects):
        if o.type == 'LIGHT':
            bpy.data.objects.remove(o, do_unlink=True)
    S.setup_rake_stage()
    P = S.posed_points(meshes)
    top = P[(P[:, 2] > P[:, 2].max() - 0.62 * scale) & (np.abs(P[:, 0]) < 0.42 * scale)]
    S.rake_lights(24, 6)
    S.frame(cam, [], 24, 6, 70.0, pts=top)
    S.render(tile(cid, 'bust'))


done = 0
for cid, scale, poses, path in mem:
    if not os.path.isfile(path):
        M.log("MISSING", path)
        continue
    have = all(os.path.isfile(tile(cid, t)) and os.path.getmtime(tile(cid, t)) > os.path.getmtime(path) for t in TAGS)
    if have and cid not in fresh:
        continue
    render_member(cid, scale, poses, path)
    done += 1
M.log("SHOTS rendered %d of %d creatures" % (done, len(mem)))

# ------------------------------------------------------------------------------------------------ sheets (after sheet.py)
PP = 96; FONT = 0.62
STRIP = int(round(EM / FONT)); BAND = STRIP + 10; GAP = 3
BG = (0.035, 0.037, 0.045)


def render_labels(strings, width_px=4800):
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
        cu.body = s; cu.size = FONT; cu.align_x = 'LEFT'; cu.align_y = 'CENTER'
        ob = bpy.data.objects.new("t%d" % i, cu)
        ob.location = (0.12, -(i + 0.5), 0.0)
        ob.data.materials.append(mat)
        sc.collection.objects.link(ob)
    wu = width_px / PP
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    cd.type = 'ORTHO'; cd.ortho_scale = max(wu, n)
    cam.location = (wu / 2, -n / 2, 10.0)
    world = bpy.data.worlds.new("w"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0, 0, 0, 1)
    sc.world = world
    S.setup_render(width_px, n * PP, samples=16)
    p = os.path.join(M.WORK, "_labels_asm.png")
    S.render(p)
    a = S.load_px(p)[::-1, :, 0]
    strips = []
    for i in range(n):
        st = a[i * PP:(i + 1) * PP]
        cc = np.where(st.max(axis=0) > 0.04)[0]
        strips.append(st[:, :(int(cc.max()) + 10 if len(cc) else 10)].copy())
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
    Wy = area_weights(a.shape[0], h); Wx = area_weights(a.shape[1], w)
    if a.ndim == 2:
        return Wy @ a @ Wx.T
    return np.stack([Wy @ a[:, :, c] @ Wx.T for c in range(a.shape[2])], axis=2)


def load_tile(path):
    if not os.path.isfile(path):
        return np.full((TILE, TILE, 3), 0.1, dtype=np.float64)
    a = S.load_px(path)[::-1, :, :3].astype(np.float64)
    if a.shape[0] != TILE or a.shape[1] != TILE:
        a = resample(a, TILE, TILE)
    return a


def paste_text(canvas, strip, x, y, h, color, max_w):
    if strip.shape[1] * h / strip.shape[0] > max_w:
        h2 = max(12, int(max_w * strip.shape[0] / strip.shape[1]))
        y += (h - h2) // 2
        h = h2
    st = np.clip(resample(strip.astype(np.float64), h, max(1, int(round(strip.shape[1] * h / strip.shape[0])))), 0, 1)
    hh, ww = st.shape
    ww = min(ww, canvas.shape[1] - x)
    reg = canvas[y:y + hh, x:x + ww]
    al = st[:reg.shape[0], :ww, None]
    reg[:] = reg * (1 - al) + np.array(color, dtype=np.float64)[None, None, :] * al


def save_jpg(canvas, out_path):
    img = np.clip(canvas, 0, 1)
    H, W = img.shape[:2]
    ok = False
    try:
        import OpenImageIO as oiio
        a8 = np.ascontiguousarray((img * 255.0 + 0.5).astype(np.uint8))
        spec = oiio.ImageSpec(W, H, 3, 'uint8')
        spec.attribute('Compression', 'jpeg:92')
        o = oiio.ImageOutput.create(out_path)
        ok = bool(o and o.open(out_path, spec) and o.write_image(a8))
        if o:
            o.close()
    except Exception as e:
        M.log("OIIO not usable for the sheet:", e)
    if not ok:
        im = bpy.data.images.new("sheet", W, H, alpha=False)
        px = np.ones((H, W, 4), dtype=np.float32)
        px[:, :, :3] = img[::-1]
        im.pixels.foreach_set(px.ravel())
        im.filepath_raw = out_path; im.file_format = 'JPEG'
        im.save()
    M.log("SHEET", out_path, "%dx%d" % (W, H))


ok_mem = [m for m in mem if os.path.isfile(tile(m[0], 'game'))]
if ok_mem and '--nosheet' not in args:
    short = [m[0].replace('mon_', '', 1) + ("  x%g" % m[1] if abs(m[1] - 1.0) > 1e-6 else "") for m in ok_mem]
    strips = render_labels(short + ["bust (raking light)  |  attack  |  walk"])
    n = len(ok_mem)
    cols = min(3, n); rows = (n + cols - 1) // cols
    W = cols * TILE + (cols - 1) * GAP
    for tag, suffix in (('game', ''), ('sil', '_sil')):
        canvas = np.zeros((rows * (BAND + TILE), W, 3), dtype=np.float64)
        canvas[:] = BG
        for i, m in enumerate(ok_mem):
            r, c = divmod(i, cols)
            x = c * (TILE + GAP); y = r * (BAND + TILE)
            canvas[y:y + BAND, x:x + TILE] = (0.17, 0.135, 0.09)
            paste_text(canvas, strips[i], x + 8, y + 5, STRIP, (1.0, 0.87, 0.6), TILE - 16)
            canvas[y + BAND:y + BAND + TILE, x:x + TILE] = load_tile(tile(m[0], tag))
        save_jpg(canvas, os.path.join(M.SHOT_DIR, "family_%s%s.jpg" % (name, suffix)))
    if '--nodetail' not in args:
        W = 3 * TILE + 2 * GAP
        only = [m for m in ok_mem if (not fresh or fresh == {''} or m[0] in fresh)] if '--detailfresh' in args else ok_mem
        idx = {m[0]: i for i, m in enumerate(ok_mem)}
        for pg in range(0, len(only), PERPAGE):
            chunk = only[pg:pg + PERPAGE]
            canvas = np.zeros((len(chunk) * (BAND + TILE), W, 3), dtype=np.float64)
            canvas[:] = BG
            for r, m in enumerate(chunk):
                y = r * (BAND + TILE)
                canvas[y:y + BAND] = (0.17, 0.135, 0.09)
                paste_text(canvas, strips[idx[m[0]]], 10, y + 5, STRIP, (1.0, 0.87, 0.6), TILE + 160)
                paste_text(canvas, strips[-1], TILE + 190, y + 5, STRIP, (0.75, 0.78, 0.84), W - TILE - 200)
                for k, tag in enumerate(('bust', 'atk', 'walk')):
                    x = k * (TILE + GAP)
                    canvas[y + BAND:y + BAND + TILE, x:x + TILE] = load_tile(tile(m[0], tag))
            save_jpg(canvas, os.path.join(M.SHOT_DIR, "family_%s_detail%d.jpg" % (name, pg // PERPAGE + 1)))
