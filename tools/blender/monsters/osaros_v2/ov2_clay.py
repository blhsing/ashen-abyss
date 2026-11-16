"""Quick clay renders of Parts (look-dev of the sculpt, no textures): EEVEE, grey material, raking key light."""
import bpy, bmesh, math, os
import numpy as np
import monlib as M
import stage as S


def part_object(part, name="clay", color=(0.55, 0.53, 0.50), rough=0.55, smooth=True, auto=50.0, emit=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata(part.v.tolist(), [], part.f)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        f.smooth = smooth
    for e in bm.edges:
        lf = e.link_faces
        e.smooth = bool(len(lf) != 2 or e.calc_face_angle(0.0) < math.radians(auto))
    bm.to_mesh(me); bm.free()
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = tuple(color) + (1,)
    b.inputs["Roughness"].default_value = rough
    if emit is not None:
        b.inputs["Emission Color"].default_value = tuple(emit) + (1,)
        b.inputs["Emission Strength"].default_value = 3.0
    me.materials.append(m)
    return ob


def views(out_prefix, center, size, shots, res=800, rake=True, graze=24.0):
    """shots: [(tag, yaw, pitch)], crop = cube of half-size `size` around center"""
    S.setup_render(res, res, samples=24)
    S.setup_rake_stage(floor=False)
    cam = S.make_camera()
    c = np.asarray(center, dtype=float)
    pts = np.array([c + size, c - size])
    out = []
    for tag, yaw, pitch in shots:
        S.frame(cam, [], yaw, pitch, 85.0, pts=pts)
        S.rake_lights(yaw, pitch, graze=graze, side=(-1.0 if yaw < 0 else 1.0), key=3.0, fill=0.5, rim=1.2)
        p = out_prefix + "_" + tag + ".png"
        S.render(p)
        out.append(p)
    return out
