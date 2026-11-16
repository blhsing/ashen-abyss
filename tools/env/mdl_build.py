# Build game-ready .glb props from Poly Haven glTF sources according to mdl_spec.py
# run: blender -b --factory-startup --python Y:\tools\env\mdl_build.py -- [name ...]     (no names = everything not built yet; "--all" = rebuild all)
# Per prop: import (vertices merged) -> pick objects -> strip materials -> join -> scale/orient -> decimate -> origin at base centre
#           -> shrink textures -> export single GLB (+Y up, metres) -> write stats to Y:\assets_src\env\models_build\<name>.json
import bpy, bmesh, os, re, sys, json, math, time
import numpy as np
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mdl_spec import SPEC

SRC = r"Y:\assets_src\env\polyhaven\models"
OUT = r"Y:\game\assets\env\models"
STAT = r"Y:\assets_src\env\models_build"  # per-prop build stats (kept out of the Godot project)
TEXDIR = r"Y:\game\assets\env\tex"         # tiling sets used by the 'cap' option (moss)
os.makedirs(STAT, exist_ok=True)
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
NATURAL = ("tree", "rock")


def tri_count(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)


def bbox(objs):
    lo = np.array([1e9] * 3); hi = -lo
    for o in objs:
        n = len(o.data.vertices)
        if not n:
            continue
        co = np.empty(n * 3, np.float32); o.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        m = np.array(o.matrix_world)
        w = co @ m[:3, :3].T + m[:3, 3]
        lo = np.minimum(lo, w.min(axis=0)); hi = np.maximum(hi, w.max(axis=0))
    return lo, hi


def activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def bake_world(o):
    """clear parent, bake world transform into mesh data."""
    mw = o.matrix_world.copy()
    o.parent = None
    if o.data.users > 1:
        o.data = o.data.copy()
    o.data.transform(mw)
    o.matrix_world = Matrix.Identity(4)
    if mw.determinant() < 0:
        o.data.flip_normals()


def join(objs, name):
    if len(objs) > 1:
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active if len(objs) > 1 else objs[0]
    o.name = name
    o.data.name = name
    return o


def mat_islands(o, rx, delete):
    """centroids of connected face islands whose material matches rx; optionally delete those faces."""
    idx = [i for i, s in enumerate(o.material_slots) if s.material and re.search(rx, s.material.name, re.I)]
    if not idx:
        return []
    bm = bmesh.new(); bm.from_mesh(o.data)
    faces = [f for f in bm.faces if f.material_index in idx]
    seen = set(); cents = []
    for f in faces:
        if f.index in seen:
            continue
        stack = [f]; seen.add(f.index); vs = {}
        while stack:
            g = stack.pop()
            for v in g.verts:
                vs[v.index] = v.co
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen and h.material_index in idx:
                        seen.add(h.index); stack.append(h)
        pts = np.array([tuple(c) for c in vs.values()])
        cents.append(((pts.min(axis=0) + pts.max(axis=0)) / 2).tolist() + [len(vs)])
    if delete and faces:
        bmesh.ops.delete(bm, geom=faces, context="FACES")
        bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    return cents


def drop_materials(o, rx):
    idx = [i for i, s in enumerate(o.material_slots) if s.material and re.search(rx, s.material.name, re.I)]
    if not idx:
        return 0
    bm = bmesh.new(); bm.from_mesh(o.data)
    faces = [f for f in bm.faces if f.material_index in idx]
    n = len(faces)
    bmesh.ops.delete(bm, geom=faces, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(o.data); bm.free(); o.data.update()
    return n


def cleanup_slots(o):
    activate(o)
    try:
        bpy.ops.object.material_slot_remove_unused()
    except Exception:
        pass


def weld(o, dist=1e-5):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bm.to_mesh(o.data); bm.free(); o.data.update()


def prune_islands(o, target=None, min_diag=None):
    """delete the smallest disconnected pieces: below min_diag metres, and/or until the triangle count fits target."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    seen = bytearray(len(bm.faces)); comps = []
    for f in bm.faces:
        if seen[f.index]:
            continue
        stack = [f]; seen[f.index] = 1; fs = []
        lo = [1e9] * 3; hi = [-1e9] * 3; nt = 0
        while stack:
            g = stack.pop(); fs.append(g); nt += len(g.verts) - 2
            for v in g.verts:
                c = v.co
                for k in range(3):
                    if c[k] < lo[k]: lo[k] = c[k]
                    if c[k] > hi[k]: hi[k] = c[k]
            for e in g.edges:
                for h in e.link_faces:
                    if not seen[h.index]:
                        seen[h.index] = 1; stack.append(h)
        comps.append((math.dist(lo, hi), nt, fs))
    comps.sort(key=lambda c: -c[0])
    kill = []; acc = 0
    for diag, nt, fs in comps:
        if (min_diag and diag < min_diag) or (target and acc + nt > target and acc > 0):
            kill += fs
        else:
            acc += nt
    n = len(kill)
    if kill:
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(o.data)
    bm.free(); o.data.update()
    return len(comps), n


def decimate(o, target):
    cur = tri_count([o])
    if cur <= target * 1.05 or cur == 0:
        return False
    weld(o)
    activate(o)
    m = o.modifiers.new("dec", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = max(target / cur, 0.0005)
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=m.name)
    me = o.data
    if "custom_normal" in me.attributes:
        me.attributes.remove(me.attributes["custom_normal"])
    for p in me.polygons:
        p.use_smooth = True
    try:
        me.set_sharp_from_angle(angle=math.radians(55))
    except Exception:
        pass
    me.update()
    return True


def shrink_images(objs, size, tint):
    done = {}
    for o in objs:
        for s in o.material_slots:
            m = s.material
            if not m or not m.node_tree:
                continue
            base = None
            for n in m.node_tree.nodes:
                if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].is_linked:
                    src = n.inputs["Base Color"].links[0].from_node
                    while src and src.type != "TEX_IMAGE" and src.inputs and any(i.is_linked for i in src.inputs):
                        src = next(i for i in src.inputs if i.is_linked).links[0].from_node
                    if src and src.type == "TEX_IMAGE":
                        base = src.image
            for n in m.node_tree.nodes:
                if n.type != "TEX_IMAGE" or not n.image:
                    continue
                img = n.image
                if img.name in done:
                    continue
                w, h = img.size
                if max(w, h) > size:
                    k = size / max(w, h)
                    img.scale(max(1, int(w * k)), max(1, int(h * k)))
                if tint and img is base:
                    px = np.empty(img.size[0] * img.size[1] * 4, np.float32)
                    img.pixels.foreach_get(px)
                    px = px.reshape(-1, 4)
                    px[:, :3] *= np.array(tint, np.float32)
                    img.pixels.foreach_set(px.ravel())
                    img.update()
                done[img.name] = tuple(img.size)
    return done


def base_tex_node(m):
    for n in m.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].is_linked:
            src = n.inputs["Base Color"].links[0].from_node
            while src and src.type != "TEX_IMAGE" and src.inputs and any(i.is_linked for i in src.inputs):
                src = next(i for i in src.inputs if i.is_linked).links[0].from_node
            if src and src.type == "TEX_IMAGE" and src.image:
                return n, src
    return None, None


def fix_alpha(objs, src_id, size):
    """Poly Haven glTF packages ship JPEG base colours, so cut-out foliage loses its alpha: rebuild RGBA from the separate
    Alpha map when there is one; make untextured-alpha glass translucent; make everything else opaque."""
    adir = os.path.join(SRC, src_id, "alpha")
    amaps = {os.path.splitext(f)[0].lower(): os.path.join(adir, f) for f in os.listdir(adir)} if os.path.isdir(adir) else {}
    cache = {}
    notes = []
    for o in objs:
        for s in o.material_slots:
            m = s.material
            if not m or not m.node_tree or m.name in cache:
                continue
            cache[m.name] = 1
            bsdf, tex = base_tex_node(m)
            if not bsdf or not bsdf.inputs["Alpha"].is_linked:
                continue
            img = tex.image
            w, h = img.size
            px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
            real = float(px[3::4].min()) < 0.98
            if real:
                continue
            key = next((k for k in amaps if k != "alpha" and k.split("_")[0] in m.name.lower()), "alpha" if "alpha" in amaps else None)
            if key and "glass" not in m.name.lower():
                a = bpy.data.images.load(amaps[key])
                a.colorspace_settings.name = "Non-Color"
                if tuple(a.size) != (w, h):
                    a.scale(w, h)
                ap = np.empty(w * h * 4, np.float32); a.pixels.foreach_get(ap)
                px[3::4] = ap[0::4]
                nm = img.name + "_rgba"
                if nm in bpy.data.images:
                    new = bpy.data.images[nm]
                else:
                    new = bpy.data.images.new(nm, w, h, alpha=True)
                    new.pixels.foreach_set(px); new.file_format = "PNG"; new.alpha_mode = "STRAIGHT"
                    new.pack()
                tex.image = new
                bpy.data.images.remove(a)
                notes.append("%s: alpha map" % m.name)
            else:
                for l in list(bsdf.inputs["Alpha"].links):
                    m.node_tree.links.remove(l)
                if "glass" in m.name.lower():
                    bsdf.inputs["Alpha"].default_value = 0.28
                    notes.append("%s: glass alpha 0.28" % m.name)
                else:
                    bsdf.inputs["Alpha"].default_value = 1.0
                    notes.append("%s: opaque" % m.name)
    return notes


def alpha_mask(objs):
    """make alpha-textured materials export as alphaMode MASK (alpha scissor) instead of BLEND."""
    for o in objs:
        for s in o.material_slots:
            m = s.material
            if not m or not m.node_tree:
                continue
            for n in m.node_tree.nodes:
                if n.type == "BSDF_PRINCIPLED" and n.inputs["Alpha"].is_linked:
                    l = n.inputs["Alpha"].links[0]
                    if l.from_node.type == "MATH":
                        continue
                    mt = m.node_tree.nodes.new("ShaderNodeMath"); mt.operation = "ROUND"
                    frm = l.from_socket
                    m.node_tree.links.remove(l)
                    m.node_tree.links.new(frm, mt.inputs[0])
                    m.node_tree.links.new(mt.outputs[0], n.inputs["Alpha"])
            try:
                m.blend_method = "CLIP"
            except Exception:
                pass


def has_alpha(objs):
    for o in objs:
        for s in o.material_slots:
            m = s.material
            if m and m.node_tree:
                for n in m.node_tree.nodes:
                    if n.type == "BSDF_PRINCIPLED" and n.inputs["Alpha"].is_linked:
                        return True
    return False


def uv_up_mask(o, slot, w, h):
    """per-texel up-facing component (Blender +Z) of the surface, rasterised in UV space; -2 where no triangle covers the texel."""
    me = o.data
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    loops = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get("loops", loops); loops = loops.reshape(-1, 3)
    mi = np.empty(nt, np.int32); me.loop_triangles.foreach_get("material_index", mi)
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
    nrm = np.empty(len(me.loops) * 3, np.float32); me.corner_normals.foreach_get("vector", nrm); nz = nrm.reshape(-1, 3)[:, 2]
    mask = np.full((h, w), -2.0, np.float32)
    e = 0.06
    for t in range(nt):
        if mi[t] != slot:
            continue
        a, b, c = loops[t]
        ax, ay = uv[a, 0] * w - 0.5, uv[a, 1] * h - 0.5
        bx, by = uv[b, 0] * w - 0.5, uv[b, 1] * h - 0.5
        cx, cy = uv[c, 0] * w - 0.5, uv[c, 1] * h - 0.5
        x0 = max(int(math.floor(min(ax, bx, cx))) - 1, 0); x1 = min(int(math.ceil(max(ax, bx, cx))) + 1, w - 1)
        y0 = max(int(math.floor(min(ay, by, cy))) - 1, 0); y1 = min(int(math.ceil(max(ay, by, cy))) + 1, h - 1)
        if x1 < x0 or y1 < y0:
            continue
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9:
            continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1, dtype=np.float32), np.arange(y0, y1 + 1, dtype=np.float32))
        l0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / d
        l1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / d
        l2 = 1.0 - l0 - l1
        ins = (l0 >= -e) & (l1 >= -e) & (l2 >= -e)
        if ins.any():
            v = l0 * nz[a] + l1 * nz[b] + l2 * nz[c]
            sub = mask[y0:y1 + 1, x0:x1 + 1]
            sub[ins] = v[ins]
    for _ in range(12):  # dilate into the UV padding so mips / bilinear do not bleed un-capped texels
        un = mask < -1.5
        if not un.any():
            break
        acc = np.zeros_like(mask); cnt = np.zeros_like(mask)
        for sh_ in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(mask, sh_, axis=(0, 1)); ok = sh > -1.5
            acc += np.where(ok, sh, 0.0); cnt += ok
        fill = un & (cnt > 0)
        mask[fill] = acc[fill] / cnt[fill]
    return mask


def apply_cap(objs, cap):
    """Bake a snow or moss layer onto up-facing surfaces: blends the base-colour texture towards snow / a tiling moss albedo and
    flattens the normal map under it. The mask comes from the mesh normals rasterised in UV space, broken up by albedo luminance."""
    kind = cap["kind"]; lo = cap.get("lo", 0.3); hi = cap.get("hi", 0.72)
    strength = cap.get("strength", 0.96); flat = cap.get("flatten", 0.75 if kind == "snow" else 0.45); brk = cap.get("breakup", 0.22)
    out = []
    done = set()
    for o in objs:
        for si, s in enumerate(o.material_slots):
            m = s.material
            if not m or not m.node_tree or m.name in done:
                continue
            done.add(m.name)
            bsdf, tex = base_tex_node(m)
            if not tex:
                continue
            img = tex.image
            w, h = img.size
            up = uv_up_mask(o, si, w, h)
            px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px); px = px.reshape(h, w, 4)
            lum = px[..., :3] @ np.array([0.30, 0.59, 0.11], np.float32)
            cov = up > -1.5
            if not cov.any():
                continue
            mean = float(lum[cov].mean()); sd = float(lum[cov].std()) + 1e-4
            v = up + brk * np.clip((lum - mean) / (2.5 * sd), -1.0, 1.0)
            t = np.clip((v - lo) / (hi - lo), 0.0, 1.0)
            mk = (t * t * (3.0 - 2.0 * t)) * strength
            mk[~cov] = 0.0
            if kind == "snow":
                detail = np.clip((lum - mean) / (3.0 * sd), -1.0, 1.0)[..., None]
                layer = np.array(cap.get("color", (0.86, 0.89, 0.93)), np.float32)[None, None, :] * (0.95 + 0.05 * detail)
            else:
                mi_ = bpy.data.images.load(os.path.join(TEXDIR, cap.get("tex", "moss"), "albedo.jpg"))
                tw, th = max(w // 2, 1), max(h // 2, 1)
                mi_.scale(tw, th)
                mp = np.empty(tw * th * 4, np.float32); mi_.pixels.foreach_get(mp); mp = mp.reshape(th, tw, 4)[..., :3]
                layer = np.tile(mp, (2, 2, 1))[:h, :w, :] * cap.get("darken", 0.8)
                ds = cap.get("desat", 0.0)
                if ds:
                    grey = (layer @ np.array([0.30, 0.59, 0.11], np.float32))[..., None]
                    layer = layer * (1.0 - ds) + grey * ds
                bpy.data.images.remove(mi_)
            px[..., :3] = px[..., :3] * (1.0 - mk[..., None]) + layer * mk[..., None]
            img.pixels.foreach_set(px.ravel()); img.update()
            # flatten the normal map under the layer
            nn = bsdf.inputs["Normal"].links[0].from_node if bsdf.inputs["Normal"].is_linked else None
            if nn is not None and nn.type == "NORMAL_MAP" and nn.inputs["Color"].is_linked:
                nt_ = nn.inputs["Color"].links[0].from_node
                if nt_.type == "TEX_IMAGE" and nt_.image:
                    ni = nt_.image
                    nw, nh = ni.size
                    npx = np.empty(nw * nh * 4, np.float32); ni.pixels.foreach_get(npx); npx = npx.reshape(nh, nw, 4)
                    mk2 = mk if (nw, nh) == (w, h) else mk[(np.arange(nh) * h // nh)[:, None], (np.arange(nw) * w // nw)[None, :]]
                    f = (mk2 * flat)[..., None]
                    npx[..., :3] = npx[..., :3] * (1.0 - f) + np.array((0.5, 0.5, 1.0), np.float32)[None, None, :] * f
                    ni.pixels.foreach_set(npx.ravel()); ni.update()
            out.append("%s %s %.0f%%" % (m.name, kind, 100.0 * float(mk[cov].mean())))
    return out


def to_godot(p):
    return [round(float(p[0]), 3), round(float(p[2]), 3), round(float(-p[1]), 3)]


def build(sp):
    t0 = time.time()
    name, src = sp["name"], sp["src"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, src, src + ".gltf"), merge_vertices=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    src_tris = tri_count(meshes)
    for o in meshes:
        bake_world(o)
    parts_spec = sp.get("parts", {})
    part_objs = {}
    for pn, (rx, pivot) in parts_spec.items():
        sel = [o for o in meshes if re.search(rx, o.name)]
        if not sel:
            raise RuntimeError("part %s: no objects match %s" % (pn, rx))
        part_objs[pn] = sel
    taken = set(o.name for sel in part_objs.values() for o in sel)
    rx = sp.get("objs")
    main_sel = [o for o in meshes if o.name not in taken and (rx is None or re.search(rx, o.name)) and len(o.material_slots) > 0]
    if not main_sel:
        raise RuntimeError("no objects match %r in %s: %s" % (rx, src, [o.name for o in meshes][:30]))
    flame_obj_centre = None
    fl = sp.get("flame")
    if fl and fl.startswith("obj:"):
        fo = [o for o in meshes if re.search(fl[4:], o.name)]
        if fo:
            lo, hi = bbox(fo); flame_obj_centre = ((lo + hi) / 2).tolist()
    keep = set(o.name for o in main_sel) | taken
    for o in list(bpy.context.scene.objects):
        if o.name not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
    main = join(main_sel, name)
    parts = {pn: join(sel, pn) for pn, sel in part_objs.items()}
    objs = [main] + list(parts.values())
    # strip unwanted materials (leaves...)
    dropped = 0
    if sp.get("drop"):
        for o in objs:
            dropped += drop_materials(o, sp["drop"])
    # flames from material islands (in source space; transformed below with the same matrix)
    flames = []
    if fl:
        mode, _, arg = fl.partition(":")
        if mode in ("mat", "matc", "mata"):
            cents = []
            for o in objs:
                cents += mat_islands(o, arg, delete=(mode == "mat"))
            if mode == "mata" and cents:
                pts = np.array([c[:3] for c in cents]); wts = np.array([c[3] for c in cents], float)
                flames = [(pts * wts[:, None]).sum(axis=0) / wts.sum()]
            else:
                # merge islands that belong to the same flame/bulb (crossed cards, shells)
                lo_, hi_ = bbox(objs); rad = max(0.035, 0.06 * float(max(hi_ - lo_)))
                groups = []
                for c in sorted(cents, key=lambda c: -c[3]):
                    p = np.array(c[:3])
                    for gq in groups:
                        if np.linalg.norm(gq[0] / gq[1] - p) < rad:
                            gq[0] += p * c[3]; gq[1] += c[3]; break
                    else:
                        groups.append([p * c[3], float(c[3])])
                big = max((gq[1] for gq in groups), default=0)
                flames = [gq[0] / gq[1] for gq in groups if gq[1] >= 0.2 * big]
        elif mode == "obj" and flame_obj_centre:
            flames = [np.array(flame_obj_centre)]
    for o in objs:
        cleanup_slots(o)
    # orientation / scale
    xf = Matrix.Identity(4)
    lo, hi = bbox(objs); d = hi - lo
    if sp.get("up") == "longest":
        ax = int(np.argmax(d))
        if ax == 0:
            xf = Matrix.Rotation(math.radians(-90), 4, "Y") @ xf
        elif ax == 1:
            xf = Matrix.Rotation(math.radians(90), 4, "X") @ xf
    sc = sp.get("scale", 1.0)
    if isinstance(sc, (int, float)):
        sc = (sc, sc, sc)
    xf = Matrix.Diagonal((sc[0], sc[1], sc[2], 1.0)) @ xf
    if sp.get("height"):
        h0 = float(sorted(d)[-1]) if sp.get("up") == "longest" else float(d[2])
        zs = sp.get("zstretch", 1.0)
        k = sp["height"] / (h0 * zs)
        xf = Matrix.Diagonal((k, k, k * zs, 1.0)) @ xf
    if xf != Matrix.Identity(4):
        for o in objs:
            o.data.transform(xf)
            if xf.determinant() < 0:
                o.data.flip_normals()
        flames = [np.array(xf @ Vector(f)) for f in flames]
    # decimate
    budget = sp["tris"]
    pt = sp.get("part_tris")
    total0 = tri_count(objs)
    for o in objs:
        if o is main:
            share = budget if pt else int(budget * tri_count([o]) / max(total0, 1)) if parts else budget
        else:
            share = pt if pt else int(budget * tri_count([o]) / max(total0, 1))
        if sp.get("min_island"):
            lo_, hi_ = bbox([o])
            print("  islands/pruned (size):", prune_islands(o, min_diag=sp["min_island"] * float(max(hi_ - lo_))), flush=True)
        decimate(o, max(share, 60))
        if tri_count([o]) > share * 1.12 and sp["cat"] in ("tree", "plant", "rock"):
            print("  islands/pruned (budget):", prune_islands(o, target=share), "->", tri_count([o]), flush=True)
    # origin
    lo, hi = bbox(objs); d = hi - lo
    g = sp.get("ground", "auto" if sp["cat"] in NATURAL else "min")
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    sink = 0.0
    if g == "min":
        z0 = lo[2]
    elif g == "top":
        z0 = hi[2]
    elif g == "keep":
        cx = cy = 0.0; z0 = 0.0
    elif g == "auto":
        if -0.25 * d[2] <= lo[2] <= 0.02 * d[2]:
            z0 = 0.0
        else:
            z0 = lo[2] + 0.12 * d[2]
        sink = z0 - lo[2]
    else:
        z0 = lo[2] + float(g) * d[2]; sink = z0 - lo[2]
    off = Vector((-cx, -cy, -z0))
    for o in objs:
        o.data.transform(Matrix.Translation(off))
    flames = [f + np.array(off) for f in flames]
    lo, hi = bbox(objs); d = hi - lo
    if fl and fl.startswith("top:"):
        flames = [np.array([0.0, 0.0, hi[2] + float(fl[4:])])]
    # part pivots
    part_info = {}
    for pn, o in parts.items():
        pivot = parts_spec[pn][1]
        plo, phi = bbox([o])
        if pivot == "left":
            pv = Vector((plo[0], (plo[1] + phi[1]) / 2, plo[2]))
        elif pivot == "right":
            pv = Vector((phi[0], (plo[1] + phi[1]) / 2, plo[2]))
        else:  # back_bottom (hinge of a lid): +Y is the back in Blender
            pv = Vector(((plo[0] + phi[0]) / 2, phi[1], plo[2]))
        o.data.transform(Matrix.Translation(-pv))
        o.location = pv
        part_info[pn] = {"pivot": to_godot(pv), "hinge": "vertical (Y) axis" if pivot in ("left", "right") else "X axis", "triangles": tri_count([o])}
    # textures
    imgs = shrink_images(objs, sp["tex"], sp.get("tint"))
    anotes = fix_alpha(objs, src, sp["tex"])
    if anotes:
        print("  alpha:", "; ".join(anotes), flush=True)
    if sp.get("mask"):
        alpha_mask(objs)
    cap_notes = apply_cap(objs, sp["cap"]) if sp.get("cap") else []
    if cap_notes:
        print("  cap:", "; ".join(cap_notes), flush=True)
    alpha = has_alpha(objs) or any("glass" in a for a in anotes)
    # export
    odir = os.path.join(OUT, name)
    os.makedirs(odir, exist_ok=True)
    fp = os.path.join(odir, name + ".glb")
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = main
    bpy.ops.export_scene.gltf(filepath=fp, export_format="GLB", use_selection=True, export_apply=True, export_yup=True,
                              export_image_format="JPEG", export_jpeg_quality=88, export_materials="EXPORT",
                              export_normals=True, export_tangents=False, export_animations=False, export_cameras=False,
                              export_lights=False, export_extras=False, export_texcoords=True)
    tris = tri_count(objs)
    info = {
        "name": name, "path": "res://assets/env/models/%s/%s.glb" % (name, name), "source_id": src,
        "source_url": "https://polyhaven.com/a/" + src, "category": sp["cat"], "zones": sp["zones"],
        "triangles": tris, "source_triangles": src_tris,
        "size": [round(float(d[0]), 3), round(float(d[2]), 3), round(float(d[1]), 3)],
        "origin": {"min": "base centre", "auto": "base centre (ground level)", "top": "top centre (hang point)", "keep": "as authored (wall mount point)"}.get(g, "base centre (ground level)") if isinstance(g, str) else "base centre (ground level)",
        "below_ground": round(float(-lo[2]), 3), "texture_px": max(max(v) for v in imgs.values()) if imgs else 0,
        "materials": len(set(s.material.name for o in objs for s in o.material_slots if s.material)),
        "alpha": ("mask" if sp.get("mask") else "blend (glass)" if any("glass" in a for a in anotes) or sp["cat"] == "light" else "blend") if alpha else "opaque",
        "casts_light": bool(fl), "flames": [to_godot(f) for f in flames],
        "bytes": os.path.getsize(fp),
    }
    if part_info:
        info["parts"] = part_info
    if sp.get("derived"):
        info["derived"] = sp["derived"]
    if cap_notes:
        info["cap"] = {"kind": sp["cap"]["kind"], "coverage": cap_notes}
    if sp.get("drop"):
        info["removed"] = "%s geometry (%d faces)" % (sp["drop"], dropped)
    if "scale" in sp or "height" in sp:
        info["rescaled"] = True
    json.dump(info, open(os.path.join(STAT, name + ".json"), "w", encoding="utf-8"), indent=1)
    print("OK %-18s %-26s %7d -> %5d tris  %5.2fx%5.2fx%5.2f m  tex %4d  %5.2f MB  flames %d  %.0fs" % (
        name, src, src_tris, tris, d[0], d[1], d[2], info["texture_px"], info["bytes"] / 1e6, len(flames), time.time() - t0), flush=True)


names = [a for a in argv if not a.startswith("--")]
todo = [s for s in SPEC if (s["name"] in names) or (not names and ("--all" in argv or not os.path.exists(os.path.join(STAT, s["name"] + ".json"))))]
fails = []
for sp in todo:
    try:
        build(sp)
    except Exception as e:
        import traceback
        traceback.print_exc()
        fails.append((sp["name"], str(e)))
        print("FAIL %s: %s" % (sp["name"], e), flush=True)
print("built %d, failed %d: %s" % (len(todo) - len(fails), len(fails), fails))
