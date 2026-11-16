"""Multi-atlas unwrap / bake / material assignment for Osaros v2 (built on looks.py, same two-stage bake and texture
conventions; the kit's single-atlas functions are not touched).

* the creature's mesh objects are split into atlas GROUPS (head, body, cloth): each group is unwrapped, baked and
  written as its own 2048 set  <id>_<group>_bc / _orm / _nrm / _em;
* an object can be unwrapped as one relaxed island (angle based, one seam down the back) instead of smart projection,
  and parts can ask for more or fewer texels (Part.texel);
* an object can have a dense sculpt (`hi`): its geometry is baked into the low mesh's tangent space (selected to
  active) and combined with the looks' own relief by reoriented normal mapping;
* after the bake every face whose texels carry emission is moved to the group's emis_ slot: emissive faces are exactly
  the faces that glow, whatever look they were painted with.
"""
import bpy, bmesh, math, os, time, heapq
import numpy as np
import monlib as M
import looks as L


def log(*a):
    M.log(*a)


# ------------------------------------------------------------------------------------------------ unwrap
def _edit(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.mode_set(mode='EDIT')


def _back_seam(ob, face_mask):
    """mark a seam from the top of the head down the back of the selected faces (shortest path hugging x = 0, y > 0)"""
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    vs = set()
    for f in bm.faces:
        if face_mask[f.index]:
            for v in f.verts:
                vs.add(v.index)
    if not vs:
        bm.free(); return 0
    co = {i: np.array(bm.verts[i].co) for i in vs}
    top = max(vs, key=lambda i: co[i][2] - 2.0 * abs(co[i][0]))
    back = [i for i in vs if co[i][1] > 0.0]
    bot = min(back, key=lambda i: co[i][2] + 2.0 * abs(co[i][0]))
    adj = {}
    for e in bm.edges:
        a, b = e.verts[0].index, e.verts[1].index
        if a in vs and b in vs and any(face_mask[f.index] for f in e.link_faces):
            mid = (co[a] + co[b]) / 2
            pen = 1.0 + 40.0 * abs(mid[0]) + (30.0 if mid[1] < -0.01 else 0.0)
            w = float(np.linalg.norm(co[a] - co[b])) * pen
            adj.setdefault(a, []).append((b, w, e.index)); adj.setdefault(b, []).append((a, w, e.index))
    dist = {top: 0.0}; prev = {}
    pq = [(0.0, top)]
    while pq:
        d, u = heapq.heappop(pq)
        if u == bot:
            break
        if d > dist.get(u, 1e18):
            continue
        for (v, w, ei) in adj.get(u, []):
            nd = d + w
            if nd < dist.get(v, 1e18):
                dist[v] = nd; prev[v] = (u, ei); heapq.heappush(pq, (nd, v))
    n = 0
    u = bot
    while u in prev:
        u, ei = prev[u]
        bm.edges[ei].seam = True
        n += 1
    bm.to_mesh(me); bm.free()
    return n


def unwrap_group(C, objs, relaxed=None, margin=0.004):
    """relaxed = {object name: [look names whose faces become ONE relaxed island]}"""
    relaxed = relaxed or {}
    split_faces = []
    sc = bpy.context.scene
    L.unwrap(objs, margin=margin)                                   # smart projection of everything first
    look_of_mat = {m.name: l for l, m in C.look_mats.items()}
    for ob in objs:
        if ob.name not in relaxed:
            continue
        me = ob.data
        looks = [look_of_mat[m.name] for m in me.materials]
        mi = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get('material_index', mi)
        mask = np.array([looks[i] in relaxed[ob.name] for i in mi], dtype=bool)
        ns = _back_seam(ob, mask)
        split = getattr(C, 'uv_split', {}).get(ob.name)
        if split is not None:
            # a second island (back of the head): seam where the two regions meet; its texels are scaled down later
            cen = np.empty(len(me.polygons) * 3, dtype=np.float32)
            me.polygons.foreach_get('center', cen)
            back = np.asarray(split[0](cen.reshape(-1, 3)), dtype=bool) & mask
            bm = bmesh.new(); bm.from_mesh(me)
            bm.faces.ensure_lookup_table()
            for e in bm.edges:
                lf = e.link_faces
                if len(lf) == 2 and mask[lf[0].index] and mask[lf[1].index] and back[lf[0].index] != back[lf[1].index]:
                    e.seam = True
            bm.to_mesh(me); bm.free()
            split_faces.append((me, back, float(split[1])))
        _edit([ob])
        bm = bmesh.from_edit_mesh(me)
        bm.faces.ensure_lookup_table()
        for f in bm.faces:
            f.select_set(False)
        for f in bm.faces:
            if mask[f.index]:
                f.select_set(True)
        bm.select_flush_mode()
        bmesh.update_edit_mesh(me)
        try:
            bpy.ops.uv.unwrap(method='ANGLE_BASED', fill_holes=True, correct_aspect=True, margin=0.001)
        except Exception as e:
            log("WARNING relaxed unwrap failed:", e)
        bpy.ops.object.mode_set(mode='OBJECT')
        log("relaxed unwrap of %s: %d faces, back seam %d edges" % (ob.name, int(mask.sum()), ns))
    # texel weights: scale parts for the island-size normalisation, then pack
    saved = []
    for ob in objs:
        rng = C.part_ranges.get(ob.name, [])
        if not any(abs(getattr(p, 'texel', 1.0) - 1.0) > 1e-6 for (_, _, p) in rng):
            continue
        me = ob.data
        co = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get('co', co)
        co0 = co.copy()
        co = co.reshape(-1, 3)
        for (a, b, p) in rng:
            k = float(getattr(p, 'texel', 1.0))
            if abs(k - 1.0) > 1e-6 and b > a:
                c = co[a:b].mean(0)
                co[a:b] = c + (co[a:b] - c) * k
        me.vertices.foreach_set('co', co.ravel())
        me.update()
        saved.append((me, co0))
    sc.tool_settings.use_uv_select_sync = True
    _edit(objs)
    bpy.ops.mesh.select_all(action='SELECT')
    try:
        bpy.ops.uv.average_islands_scale()
    except Exception as e:
        log("WARNING average_islands_scale failed:", e)
    bpy.ops.object.mode_set(mode='OBJECT')
    for me, co0 in saved:
        me.vertices.foreach_set('co', co0)
        me.update()
    for me, back, k in split_faces:
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        me.uv_layers.active.data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2)
        ls = np.empty(len(me.polygons), dtype=np.int32); me.polygons.foreach_get('loop_start', ls)
        lt = np.empty(len(me.polygons), dtype=np.int32); me.polygons.foreach_get('loop_total', lt)
        idx = np.concatenate([np.arange(s, s + t) for s, t in zip(ls[back], lt[back])]) if back.any() else np.zeros(0, dtype=np.int64)
        if len(idx):
            c = uv[idx].mean(0)
            uv[idx] = c + (uv[idx] - c) * k
            me.uv_layers.active.data.foreach_set('uv', uv.ravel())
            me.update()
            log("uv split: %d faces of the back island scaled x%.2f" % (int(back.sum()), k))
    _edit(objs)
    bpy.ops.mesh.select_all(action='SELECT')
    ok = False
    for kw in (dict(rotate=True, margin_method='FRACTION', margin=margin, shape_method='CONCAVE'), dict(rotate=True, margin=margin), dict(margin=margin)):
        try:
            bpy.ops.uv.pack_islands(**kw)
            ok = True
            break
        except Exception as e:
            log("pack_islands variant failed:", e)
    bpy.ops.object.mode_set(mode='OBJECT')
    if not ok:
        log("WARNING: pack_islands failed, smart-projection layout kept")


# ------------------------------------------------------------------------------------------------ bake
def _rnm(n1, n2):
    """reoriented normal mapping: detail n2 applied on base n1 (both unpacked, (h,w,3))"""
    t = n1 + np.array([0.0, 0.0, 1.0])
    u = n2 * np.array([-1.0, -1.0, 1.0])
    r = t * ((t * u).sum(axis=2, keepdims=True) / np.maximum(t[:, :, 2:3], 1e-4)) - u
    r /= np.maximum(np.linalg.norm(r, axis=2, keepdims=True), 1e-6)
    return r


def _dilate(a, valid, block, iters):
    """spread valid texels of `a` into invalid ones that are not in `block`"""
    a = a.copy(); valid = valid.copy()
    for _ in range(iters):
        acc = np.zeros_like(a); cnt = np.zeros(valid.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            v = np.roll(valid, (dy, dx), (0, 1))
            acc += np.roll(a, (dy, dx), (0, 1)) * v[:, :, None]
            cnt += v
        new = (~valid) & (cnt > 0) & (~block)
        a[new] = acc[new] / cnt[new][:, None]
        valid |= new
    return a, valid


def _hi_object(part, name):
    me = bpy.data.meshes.new(name)
    me.from_pydata(part.v.tolist(), [], part.f)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    thr = math.radians(float(getattr(part, 'smooth', 50.0)))
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        lf = e.link_faces
        e.smooth = bool(len(lf) != 2 or e.calc_face_angle(0.0) < thr)
    bm.to_mesh(me); bm.free()
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    me.materials.append(m)
    return ob


def bake_group(C, gname, objs, size=2048, mask_size=1024, ao_strength=0.5, hi=None, quality=(90, 88, 95, 90), all_objs=None):
    """bake one atlas. hi = {low object name: dense Part}. Returns dict(tex=..., em_lum=array or None, size=...)"""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.render.threads_mode = 'FIXED'
    sc.render.threads = max(1, min(32, int(os.environ.get('ASHEN_BLENDER_THREADS', '12'))))
    try:
        sc.cycles.use_denoising = False
        sc.cycles.use_adaptive_sampling = False
    except Exception:
        pass
    if sc.world is None:
        sc.world = bpy.data.worlds.new("bakeworld")
    names = set(m.name for o in objs for m in o.data.materials)
    mats = [m for m in C.look_mats.values() if m.name in names]
    bk = sc.render.bake
    bk.use_selected_to_active = False
    bk.use_clear = False
    bk.target = 'IMAGE_TEXTURES'

    def target(im, shader_node):
        for m in mats:
            nt = m.node_tree
            n = nt.nodes['BAKE']
            n.image = im
            for x in nt.nodes:
                x.select = False
            n.select = True
            nt.nodes.active = n
            nt.links.new(nt.nodes[shader_node].outputs[0], nt.nodes['OUT'].inputs[0])

    def run(im, shader_node, typ, samples, margin, **kw):
        target(im, shader_node)
        sc.cycles.samples = samples
        bk.margin = margin
        L._select(objs)
        bpy.ops.object.bake(type=typ, margin=margin, use_clear=False, **kw)

    t0 = time.time()
    # ---- stage 1: geometry masks
    mk = L._img("bk_mask", mask_size, (1.0, 1.0, 1.0), True)
    run(mk, 'E_MASK', 'EMIT', 6, 12)
    a = L._np(mk)[:, :, :3]
    ao_w = np.clip(L._blur(a[:, :, 0], 2), 0, 1)
    cav = np.clip((1.0 - L._blur(a[:, :, 1], 1)) * 1.7 - 0.06, 0, 1)
    edge = np.clip((1.0 - L._blur(a[:, :, 2], 1)) * 1.15 - 0.03, 0, 1)
    mbn = "mon_masks_baked_" + gname
    old = bpy.data.images.get(mbn)
    if old:
        bpy.data.images.remove(old)
    mb = bpy.data.images.new(mbn, mask_size, mask_size, alpha=False, float_buffer=True)
    mb.colorspace_settings.name = 'Non-Color'
    b = np.ones((mask_size, mask_size, 4), dtype=np.float32)
    b[:, :, 0] = ao_w; b[:, :, 1] = cav; b[:, :, 2] = edge
    mb.pixels.foreach_set(b.ravel())
    for m in mats:
        n = m.node_tree.nodes.get('MASK')
        if n is not None:
            n.image = mb
    bpy.data.images.remove(mk)
    t1 = time.time()
    # ---- stage 2: colour, ORM, normals, emission
    imgs = dict(col=L._img("bk_col", size, (0.2, 0.2, 0.2), False), orm=L._img("bk_orm", size, (0.0, 0.8, 0.0), True),
                nrm=L._img("bk_nrm", size, (0.5, 0.5, 1.0), True))
    mg = max(8, size // 128)
    run(imgs['col'], 'E_COL', 'EMIT', 3, mg)
    run(imgs['orm'], 'E_ORM', 'EMIT', 3, mg)
    run(imgs['nrm'], 'P', 'NORMAL', 6, mg, normal_space='TANGENT')
    has_em = any('mon_emit' in m.keys() for m in mats)
    if has_em:
        imgs['emi'] = L._img("bk_emi", size, (0.0, 0.0, 0.0), False)
        run(imgs['emi'], 'E_EMI', 'EMIT', 3, mg)
    t2 = time.time()
    col = L._np(imgs['col'])[:, :, :3]
    orm = L._np(imgs['orm'])[:, :, :3].copy()
    nrm = L._np(imgs['nrm'])[:, :, :3].copy()
    n = nrm * 2.0 - 1.0
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    # ---- stage 3: the dense sculpt's geometry into the low mesh's tangent space
    if hi:
        cv = L._img("bk_cov", size, (0.0, 0.0, 0.0), True)
        run(cv, 'E_ONE', 'EMIT', 1, 0)
        cov_all = L._np(cv)[:, :, 0] > 0.5
        bpy.data.images.remove(cv)
        for low_name, hp in hi.items():
            low = bpy.data.objects[low_name]
            ob_h = _hi_object(hp, "hi_" + low_name)
            ng = L._img("bk_ngeo", size, (0.0, 0.0, 0.0), True)
            target(ng, 'P')
            sc.cycles.samples = 4
            bpy.ops.object.select_all(action='DESELECT')
            ob_h.select_set(True); low.select_set(True)
            bpy.context.view_layer.objects.active = low
            try:
                bpy.ops.object.bake(type='NORMAL', margin=0, use_clear=False, normal_space='TANGENT', use_selected_to_active=True,
                                    cage_extrusion=float(getattr(hp, 'cage', 0.003)), max_ray_distance=float(getattr(hp, 'ray', 0.009)))
                g = L._np(ng)[:, :, :3].copy()
                own = g.sum(axis=2) > 0.05
                gn = g * 2.0 - 1.0
                ln = np.linalg.norm(gn, axis=2)
                own &= (ln > 0.5) & (gn[:, :, 2] > 0.05)
                gn /= np.maximum(ln, 1e-6)[:, :, None]
                gn[~own] = (0.0, 0.0, 1.0)
                gn, valid = _dilate(gn, own, cov_all & ~own, mg)
                gn[~valid] = (0.0, 0.0, 1.0)
                gn /= np.maximum(np.linalg.norm(gn, axis=2, keepdims=True), 1e-6)
                dev = np.degrees(np.arccos(np.clip(gn[:, :, 2][own], -1, 1)))
                log("sculpt normals of %s: %.1f%% of the atlas, deviation median %.1f deg, 90th pct %.1f deg, max %.0f" % (
                    low_name, 100.0 * own.mean(), float(np.median(dev)), float(np.percentile(dev, 90)), float(dev.max())))
                n = _rnm(gn, n)
            except Exception as e:
                log("WARNING: sculpt-normal bake failed for %s: %s" % (low_name, e))
            bpy.data.images.remove(ng)
            meh = ob_h.data
            bpy.data.objects.remove(ob_h, do_unlink=True)
            bpy.data.meshes.remove(meh)
        bk.use_selected_to_active = False
    t3 = time.time()
    log("bake %s: masks %.1fs, maps %.1fs, sculpt %.1fs" % (gname, t1 - t0, t2 - t1, t3 - t2))
    # ---- compose (as looks.bake_and_assign)
    f = size // mask_size
    ao = ao_w; cvt = cav
    while f > 1:
        ao = L._up2(ao); cvt = L._up2(cvt); f //= 2
    emk = orm[:, :, 0].copy()
    is_em = np.clip(emk * 4.0, 0, 1)
    k = (1.0 - ao_strength) + ao_strength * np.clip(ao, 0, 1) ** 1.25
    k = k * (1.0 - 0.22 * cvt)
    k = 1.0 - (1.0 - k) * (1.0 - is_em)
    tint = np.stack([k ** 0.94, k, k ** 1.06], axis=2)
    bc = L._lin2srgb(L._srgb2lin(col) * tint)
    occ = np.clip(ao * (1.0 - 0.45 * cvt), 0, 1)
    orm[:, :, 0] = occ
    orm[:, :, 1] = np.clip(orm[:, :, 1] + (1 - k) * 0.12, 0.04, 1)
    nrm = n * 0.5 + 0.5
    dev = np.degrees(np.arccos(np.clip(n[:, :, 2], -1, 1)))
    cover = np.abs(L._np(imgs['col'])[:, :, :3] - 0.2).sum(axis=2) > 1e-3
    if cover.any():
        log("atlas %s: normal deviation median %.1f deg, 90th pct %.1f deg; roughness std %.3f; coverage %.0f%%" % (
            gname, float(np.median(dev[cover])), float(np.percentile(dev[cover], 90)), float(orm[:, :, 1][cover].std()), 100.0 * cover.mean()))
    base = "%s_%s" % (C.id, gname)
    T = dict(bc=L._save_jpg(base + "_bc", bc, False, quality[0]), orm=L._save_jpg(base + "_orm", orm, True, quality[1], full_chroma=True),
             nrm=L._save_jpg(base + "_nrm", nrm, True, quality[2], full_chroma=True))
    em_lum = None
    if has_em:
        e = L._np(imgs['emi'])[:, :, :3].copy()
        if e.max() > 0.02:
            T['em'] = L._save_jpg(base + "_em", e, False, quality[3])
            em_lum = e.max(axis=2)
    for im in imgs.values():
        bpy.data.images.remove(im)
    return dict(tex=T, em_lum=em_lum, size=size)


# ------------------------------------------------------------------------------------------------ slots
def _face_glow(me, em_lum, thr=0.045):
    """per polygon (triangles): does any texel inside it carry emission?"""
    npoly = len(me.polygons)
    out = np.zeros(npoly, dtype=bool)
    if em_lum is None or npoly == 0:
        return out
    size = em_lum.shape[0]
    lum = em_lum
    for _ in range(2):                                             # dilate by 2 px so thin lines are not missed
        lum = np.maximum.reduce([lum, np.roll(lum, 1, 0), np.roll(lum, -1, 0), np.roll(lum, 1, 1), np.roll(lum, -1, 1)])
    uv = np.empty(len(me.loops) * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get('uv', uv)
    uv = uv.reshape(-1, 2)
    ls = np.empty(npoly, dtype=np.int32); me.polygons.foreach_get('loop_start', ls)
    lt = np.empty(npoly, dtype=np.int32); me.polygons.foreach_get('loop_total', lt)
    assert (lt == 3).all(), "mesh must be triangulated"
    A = uv[ls]; B = uv[ls + 1]; Cc = uv[ls + 2]
    K = 9
    ws = []
    for i in range(K + 1):
        for j in range(K + 1 - i):
            ws.append((i / K, j / K, 1 - i / K - j / K))
    ws = np.array(ws, dtype=np.float32)
    best = np.zeros(npoly, dtype=np.float32)
    for (wa, wb, wc) in ws:
        p = A * wa + B * wb + Cc * wc
        x = np.clip((p[:, 0] * size).astype(np.int32), 0, size - 1)
        y = np.clip((p[:, 1] * size).astype(np.int32), 0, size - 1)
        best = np.maximum(best, lum[y, x])
    out = best > thr
    # big triangles: exact scan of their bounding box
    ext = np.maximum(np.maximum(A, np.maximum(B, Cc)).max(axis=1) - np.minimum(A, np.minimum(B, Cc)).min(axis=1), 0) * size
    for i in np.where((ext > 5 * K) & (~out))[0]:
        mn = np.minimum(A[i], np.minimum(B[i], Cc[i])); mx = np.maximum(A[i], np.maximum(B[i], Cc[i]))
        x0, y0 = int(mn[0] * size), int(mn[1] * size); x1, y1 = int(mx[0] * size) + 1, int(mx[1] * size) + 1
        ys, xs = np.mgrid[y0:y1, x0:x1]
        px = (xs + 0.5) / size; py = (ys + 0.5) / size
        d = (B[i][1] - Cc[i][1]) * (A[i][0] - Cc[i][0]) + (Cc[i][0] - B[i][0]) * (A[i][1] - Cc[i][1])
        if abs(d) < 1e-12:
            continue
        l1 = ((B[i][1] - Cc[i][1]) * (px - Cc[i][0]) + (Cc[i][0] - B[i][0]) * (py - Cc[i][1])) / d
        l2 = ((Cc[i][1] - A[i][1]) * (px - Cc[i][0]) + (A[i][0] - Cc[i][0]) * (py - Cc[i][1])) / d
        ins = (l1 >= 0) & (l2 >= 0) & (l1 + l2 <= 1)
        reg = lum[np.clip(ys, 0, size - 1), np.clip(xs, 0, size - 1)]
        if ins.any() and reg[ins].max() > thr:
            out[i] = True
    return out


def assign_slots(C, groups, results):
    """groups = {gname: [objects]}, results = {gname: bake_group result}. Creates the export materials."""
    look_of_mat = {m.name: l for l, m in C.look_mats.items()}
    slot_group = {}
    slot_mats = {}
    report = {}
    for gname, objs in groups.items():
        res = results[gname]
        T = res['tex']
        for ob in objs:
            me = ob.data
            looks = [look_of_mat[m.name] for m in me.materials]
            mi = np.empty(len(me.polygons), dtype=np.int32)
            me.polygons.foreach_get('material_index', mi)
            glow = _face_glow(me, res['em_lum']) if res['em_lum'] is not None else np.zeros(len(mi), dtype=bool)
            fslot = []
            for li, l in enumerate(looks):
                d = C.looks[l]
                base = d['slot']
                emissive = 'mon_emit' in C.look_mats[l].keys()
                es = d['params'].get('eslot', 'emis_' + gname) if emissive else base
                if base.startswith('emis_'):
                    es = base
                fslot.append((base, es))
            slots = []
            new_idx = np.zeros(len(mi), dtype=np.int32)
            for li, (base, es) in enumerate(fslot):
                sel = (mi == li)
                for s, m_ in ((es, sel & glow), (base, sel & ~glow)):
                    if not m_.any():
                        continue
                    if s not in slots:
                        slots.append(s)
                    new_idx[m_] = slots.index(s)
                    report[s] = report.get(s, 0) + int(m_.sum())
            for s in slots:
                if s in slot_group and slot_group[s] != gname:
                    raise RuntimeError("material slot %s is used by two atlas groups (%s, %s)" % (s, slot_group[s], gname))
                slot_group[s] = gname
                if s not in slot_mats:
                    emit = None
                    if s.startswith('emis_'):
                        st = [float(C.look_mats[l]['mon_emit'][3]) for l, d in C.looks.items()
                              if l in C.look_mats and 'mon_emit' in C.look_mats[l].keys() and (d['slot'] == s or d['params'].get('eslot', 'emis_' + gname) == s)]
                        emit = [1.0, 1.0, 1.0, max(st) if st else 3.0]
                    mat = L._slot_material(s, T, emit)
                    slot_mats[s] = mat
            me.materials.clear()
            for s in slots:
                me.materials.append(slot_mats[s])
            me.polygons.foreach_set('material_index', new_idx)
            me.update()
    for m in list(C.look_mats.values()):
        if m.users == 0:
            bpy.data.materials.remove(m)
    C.slot_mats = slot_mats
    ok = ('skin_', 'cloth_', 'leather_', 'metal_', 'bone_', 'rock_', 'emis_', 'spectral_', 'hair_')
    for s in slot_mats:
        if not s.startswith(ok):
            log("WARNING: bad material prefix", s)
    log("materials (faces):", ", ".join("%s %d" % (k, v) for k, v in report.items()))
    return slot_mats
