"""Creature subclass for Osaros v2: several texture atlases (head / body / cloth), relaxed head unwrap, dense-sculpt
normals, emissive faces split off after the bake. Everything else (rig, parts, skinning, export) is monlib.Creature."""
import os, json, time
import numpy as np
import bpy, bmesh, math
import monlib as M
import looks as L
import ov2_looks as OL
import ov2_bake as BK

HERE = os.path.dirname(os.path.abspath(__file__))


class Creature2(M.Creature):
    # atlas group -> mesh objects (in bake order)
    GROUPS = [('head', ['body_face', 'body_crown']), ('body', ['body_main', 'wpn_1']), ('cloth', ['body_cloth'])]

    def __init__(self, cid, tri_budget=45000):
        super().__init__(cid, tri_budget)
        self.hi = {}                 # low object name -> dense Part (sculpt normals)
        self.relaxed = {}            # object name -> [looks unwrapped as one relaxed island]
        self.sizes = dict(head=2048, body=2048, cloth=2048)
        self.quality = dict(head=(92, 86, 93, 90), body=(85, 80, 90, 86), cloth=(82, 76, 86, 84))
        self.export_dir = M.OUT_DIR
        self.part_ranges = {}
        self.weld = {}               # object name -> weld distance (parts of one surface)
        self.obj_texel = {}          # texel weight of a welded object
        self.uv_split = {}           # object name -> (function(face centres) -> mask of the second island, uv scale of that island)

    def _make_objects(self):
        objs = super()._make_objects()
        self.part_ranges = {}
        for ob in objs:
            me = ob.data
            off = 0
            rng = []
            for p in self.parts:
                if p.obj == ob.name:
                    rng.append((off, off + len(p.v), p))
                    off += len(p.v)
            # hem distances handed over by solidified cloth (its sheet had open borders, the shell has none)
            if any(hasattr(p, 'hem') for (_, _, p) in rng):
                at = me.attributes.get('hem_d')
                vals = np.zeros(len(me.vertices), dtype=np.float32)
                at.data.foreach_get('value', vals)
                for (a, b, p) in rng:
                    if hasattr(p, 'hem') and len(p.hem) == b - a:
                        vals[a:b] = p.hem
                at.data.foreach_set('value', vals)
            if ob.name in self.weld:
                # parts that are one surface (the head skin, split by look): weld them so the normals are consistent
                # across the whole shell (the hollow's wall must face into the hollow), sharp where the looks meet
                bm = bmesh.new(); bm.from_mesh(me)
                n0 = len(bm.verts)
                bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=self.weld[ob.name])
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
                thr = math.radians(50.0)
                for e in bm.edges:
                    lf = e.link_faces
                    if len(lf) == 2:
                        e.smooth = bool(lf[0].material_index == lf[1].material_index and e.calc_face_angle(0.0) < thr)
                    else:
                        e.smooth = True
                bm.to_mesh(me); bm.free()
                me.update()
                M.log("welded %s: %d -> %d vertices" % (ob.name, n0, len(me.vertices)))
                dummy = M.Part(np.zeros((1, 3)), [])
                dummy.texel = float(self.obj_texel.get(ob.name, 1.0))
                rng = [(0, len(me.vertices), dummy)]
            if me.validate(verbose=False):
                M.log("NOTE: mesh %s had invalid geometry, fixed by validate()" % ob.name)
                if ob.name not in self.weld:
                    rng = []
            self.part_ranges[ob.name] = rng
        return objs

    def finalize(self, bake=True, export=True, size=2048, notes=""):
        per = self.tri_report()
        tot = sum(per.values())
        M.log("triangles per object:", per, "total", tot, "(budget %d)" % self.budget)
        if tot > self.budget:
            M.log("WARNING: OVER TRIANGLE BUDGET by", tot - self.budget)
        objs = self._make_objects()
        st = self.stats()
        M.log("bbox", st['bbox_min'], st['bbox_max'])
        byname = {o.name: o for o in objs}
        groups = {}
        for g, names in self.GROUPS:
            go = [byname[n] for n in names if n in byname]
            if go:
                groups[g] = go
        left = [o.name for o in objs if not any(o in go for go in groups.values())]
        if left:
            raise RuntimeError("objects without an atlas group: %s" % left)
        for g, go in groups.items():
            BK.unwrap_group(self, go, relaxed=self.relaxed)
        if bake:
            results = {}
            for g, go in groups.items():
                hi = {n: p for n, p in self.hi.items() if n in [o.name for o in go]}
                results[g] = BK.bake_group(self, g, go, size=self.sizes.get(g, 2048), hi=hi or None, quality=self.quality.get(g, (90, 88, 95, 90)))
            BK.assign_slots(self, groups, results)
            for s, m in self.slot_mats.items():                      # cloth is two-sided in Blender renders only; the game culls back faces
                pass
        else:
            L.assign_preview_slots(self, objs)
        st = self.stats()
        st['notes'] = notes
        st['looks'] = {k: v['slot'] for k, v in self.looks.items() if k in self.look_mats}
        st['atlases'] = {g: [o.name for o in go] for g, go in groups.items()}
        if export and bake:
            os.makedirs(self.export_dir, exist_ok=True)
            path = os.path.join(self.export_dir, self.id + ".glb")
            M.export_glb(path, self.arm, objs)
            st['file'] = path
            st['bytes'] = os.path.getsize(path)
            M.log("WROTE", path, st['bytes'], "bytes  (%.2f MB)" % (st['bytes'] / 1048576.0))
        with open(os.path.join(M.WORK, self.id + ".json"), 'w', encoding='utf8') as f:
            json.dump(st, f, indent=1)
        M.log("done %s in %.1fs  tris=%d  height=%.2f" % (self.id, time.time() - self.t0, st['triangles'], st['height']))
        return st
