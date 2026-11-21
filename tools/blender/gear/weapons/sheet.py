"""Contact sheets of gear models: every variant of a base side by side (face view, three-quarter view, icon row).

As a module: sheet.render(groups, out_path, ...) with groups = [(label, [mesh objects in weapon-local space]), ...].
As a script:  blender -b --python sheet.py -- <base> [<base> ...]      renders the exported .glb files of each base
              (end-to-end check of the files the game loads) to Y:\\shots\\gear\\weapons\\<base>.jpg
"""
import sys, os, math, json
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gearlib import *

BG = (0.030, 0.032, 0.038)


def _group_bbox(objs):
    mn = np.array([1e9] * 3)
    mx = -mn
    for o in objs:
        if o.type != 'MESH':
            continue
        co = get_co(o)
        if not len(co):
            continue
        M = np.array([list(r) for r in o.matrix_world])
        w = co @ M[:3, :3].T + M[:3, 3]
        mn = np.minimum(mn, w.min(0))
        mx = np.maximum(mx, w.max(0))
    return mn, mx


def _label(text, pos, size):
    cu = bpy.data.curves.new("lbl", 'FONT')
    cu.body = text
    cu.size = size
    cu.align_x = 'CENTER'
    ob = bpy.data.objects.new("lbl", cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = pos
    ob.rotation_euler = (math.radians(90), 0, math.radians(180))
    m = bpy.data.materials.get("lbl_mat")
    if m is None:
        m = bpy.data.materials.new("lbl_mat")
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        em = nt.nodes.new('ShaderNodeEmission')
        em.inputs['Color'].default_value = (0.55, 0.52, 0.46, 1)
        em.inputs['Strength'].default_value = 1.0
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(em.outputs[0], out.inputs[0])
    cu.materials.append(m)
    return ob


def _read(path):
    return mats.read_img(path)[..., :3]


def _down(a, k):
    h, w = a.shape[:2]
    h2, w2 = h // k, w // k
    return a[:h2 * k, :w2 * k].reshape(h2, k, w2, k, a.shape[2]).mean(axis=(1, 3))


def _bgcol():
    return mats.lin2srgb(np.array(BG, dtype=np.float32))


def _hcat(imgs, pad=8):
    H_ = max(i.shape[0] for i in imgs)
    out = []
    for i in imgs:
        c = np.empty((H_, i.shape[1] + pad, 3), dtype=np.float32)
        c[:] = _bgcol()
        y0 = (H_ - i.shape[0]) // 2
        c[y0:y0 + i.shape[0], :i.shape[1]] = i
        out.append(c)
    return np.concatenate(out, axis=1)


def _stack(rows, pad=8):
    """stack images (H, W, 3; bottom-up pixel rows) vertically, first = top, centred on the backdrop"""
    W_ = max(r.shape[1] for r in rows)
    H_ = sum(r.shape[0] for r in rows) + pad * (len(rows) + 1)
    can = np.empty((H_, W_ + 2 * pad, 3), dtype=np.float32)
    can[:] = _bgcol()
    y = H_ - pad
    for r in rows:
        h, w = r.shape[:2]
        x0 = pad + (W_ - w) // 2
        can[y - h:y, x0:x0 + w] = r
        y -= h + pad
    return can


def render(groups, out_path, face_yaw=180.0, quarter=-38.0, icon_roll=45.0, width=None, title=None, tmp_tag="x",
           quarter_pitch=16.0, pre_rot=None):
    """groups: [(label, [objects]), ...] all at the origin in weapon-local space (they are moved apart here)."""
    sc = bpy.context.scene
    tmp = os.path.join(GEAR_SHOTS, "_tmp")
    os.makedirs(tmp, exist_ok=True)
    if pre_rot:
        from mathutils import Euler
        Rm = Euler([math.radians(a) for a in pre_rot], 'XYZ').to_matrix().to_4x4()
        for _, objs in groups:
            for o in objs:
                o.matrix_world = Rm @ o.matrix_world
        bpy.context.view_layer.update()
    # ---- lay the variants out along X (camera at +Y looks down -Y, so screen-right is -X: first variant at +X)
    boxes = [_group_bbox(objs) for _, objs in groups]
    gap = 0.10 * max(max((b[1] - b[0])[[0, 2]].max() for b in boxes), 0.1)
    widths = [float(b[1][0] - b[0][0]) for b in boxes]
    total = sum(widths) + gap * (len(groups) - 1)
    x = total / 2
    empties = []
    for (label, objs), b, wd in zip(groups, boxes, widths):
        e = bpy.data.objects.new("grp", None)
        sc.collection.objects.link(e)
        cx = x - wd / 2
        e.location = (cx - float((b[0][0] + b[1][0]) / 2), 0.0, -float(b[0][2]))
        for o in objs:
            o.parent = e
        empties.append((e, label, cx, b))
        x -= wd + gap
    bpy.context.view_layer.update()
    allobjs = [o for _, objs in groups for o in objs]
    mn, mx = _group_bbox(allobjs)
    H = float(mx[2] - mn[2])
    Wd = float(mx[0] - mn[0])
    lbl = max(H, Wd * 0.4) * 0.028
    nchar = max(len(g[0]) for g in groups)
    lsz = min(lbl, (min(widths) + gap) / (0.52 * nchar))
    labels = [_label(label, (cx, 0.0, -lbl * 2.2), lsz) for e, label, cx, b in empties]
    if title:
        labels.append(_label(title, (0.0, 0.0, H + lbl * 1.2), lbl * 1.25))
    # ---- face view (orthographic)
    m_ = 1.10
    need_w, need_h = Wd * m_, (H + lbl * 6.5) * m_
    asp = need_h / need_w
    if width is None:
        width = 880 if asp > 0.8 else (1250 if asp > 0.45 else 1700)
    hpx = int(min(1150, max(420, width * asp)))
    setup_render(width, hpx, samples=32)
    if sc.world is None or not sc.world.name.startswith("stage"):
        setup_stage(floor=False, hdri="overcast", hdri_strength=0.5)
        k = bpy.data.objects.get(sc.get("stage_key", ""))
        if k is not None:
            k.data.color = (1.0, 0.95, 0.90)        # near-neutral key: the sheets are for judging material colours
    cam = bpy.data.objects.get("cam") or make_camera()
    sc.camera = cam
    cz = (H + lbl * 1.0) / 2 - lbl * 1.4
    aim_camera(cam, (0.0, 0.0, cz), face_yaw, 0.0, 12.0,
               ortho_scale=max(need_w, need_h * width / hpx) if width >= hpx else max(need_h, need_w * hpx / width))
    aim_key(face_yaw)
    p1 = os.path.join(tmp, tmp_tag + "_face.png")
    render_to(p1)
    # ---- three-quarter view (perspective)
    for l in labels:
        l.hide_render = True
    size = math.hypot(Wd, H)
    setup_render(width, hpx, samples=32)
    fov = 2 * math.atan(18.0 / 85.0)
    span = max(H * 1.12, Wd * 1.05) if hpx >= width else max(Wd * 1.05, H * 1.12 * width / hpx)
    aim_camera(cam, (0.0, 0.0, H * 0.47), face_yaw + quarter, quarter_pitch, span / 2 / math.tan(fov / 2) + 0.3 * Wd, lens=85.0)
    aim_key(face_yaw + quarter)
    p2 = os.path.join(tmp, tmp_tag + "_quarter.png")
    render_to(p2)
    # ---- icon row: each variant alone, rolled onto the diagonal, 256 px and the same picture at 64 px
    icons = []
    setup_render(256, 256, samples=32)
    for e, label, cx, b in empties:
        for e2, _, _, _ in empties:
            for o in e2.children:
                o.hide_render = e2 is not e
        loc = tuple(e.location)
        e.location = (0, 0, 0)
        e.rotation_euler = (0.0, math.radians(icon_roll), 0.0)
        bpy.context.view_layer.update()
        mn2, mx2 = _group_bbox(list(e.children))
        c = (mn2 + mx2) / 2
        ext = float(max(mx2[0] - mn2[0], mx2[2] - mn2[2]))
        aim_camera(cam, (float(c[0]), 0.0, float(c[2])), face_yaw, 0.0, 12.0, ortho_scale=ext * 1.12)
        aim_key(face_yaw)
        pi_ = os.path.join(tmp, "%s_icon_%d.png" % (tmp_tag, len(icons)))
        render_to(pi_)
        icons.append(_read(pi_))
        e.rotation_euler = (0, 0, 0)
        e.location = loc
    for e2, _, _, _ in empties:
        for o in e2.children:
            o.hide_render = False
    sep = np.empty((256, 24, 3), dtype=np.float32)
    sep[:] = mats.lin2srgb(np.array(BG, dtype=np.float32))
    row = []
    for ic in icons:
        small = _down(ic, 4)
        cell = sep[:, :1].repeat(96, axis=1).copy()
        cell[96:160, 16:80] = small
        row += [ic, cell, sep]
    icon_row = np.concatenate(row[:-1], axis=1)
    views = _hcat([_read(p1), _read(p2)]) if asp > 0.45 else _stack([_read(p1), _read(p2)])
    sheet = _stack([views, icon_row])
    mats.save_rgb(sheet, out_path, 90)
    log("SHEET", out_path)
    for l in labels:
        bpy.data.objects.remove(l, do_unlink=True)
    return out_path


def load_glb(mid):
    """import an exported gear .glb (written without the Y-up conversion) back into weapon-local Blender space"""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(GEAR_OUT, mid + ".glb"))
    objs = [o for o in bpy.data.objects if o not in before]
    fix = Matrix.Rotation(-math.pi / 2, 4, 'X')
    meshes = []
    for o in objs:
        if o.type == 'MESH':
            o.parent = None
            o.matrix_world = fix
            meshes.append(o)
    for o in objs:
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    return meshes


def render_exported(base, prefix="wpn", opts=None):
    opts = dict(opts or {})
    with open(os.path.join(MANI, "%s_%s.json" % (prefix, base)), encoding="utf8") as f:
        mani = json.load(f)
    reset_scene()
    groups = []
    for e in mani["entries"]:
        tag = e["id"].split("_")[-1] if e["kind"] == "gem" else "v%d" % e["variant"]
        groups.append(("%s  %d tris  %.2f m" % (tag, e["tris"], e["length"]), load_glb(e["id"])))
    return render(groups, os.path.join(GEAR_SHOTS, "%s.jpg" % base), tmp_tag=base, **opts)


if __name__ == "__main__":
    import defs
    for b in sys.argv[sys.argv.index('--') + 1:]:
        spec = defs.DEFS[b]
        render_exported(b, spec.get("prefix", "wpn"), spec.get("sheet"))
