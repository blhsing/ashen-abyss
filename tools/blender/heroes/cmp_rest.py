import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import rigio

for fn in ("base/Superhero_Male_FullBody.gltf", "base/Superhero_Female_FullBody.gltf", "Mannequin_F.glb", "hair/Hair_Long.gltf", "hair/Hair_Beard.gltf"):
    reset_scene()
    arm = import_rig()
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, fn))
    other = next(o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE')
    log("=====", fn, "bones", len(other.data.bones), "obj scale", tuple(other.scale), "loc", tuple(other.location))
    names_a = [b.name for b in arm.data.bones]
    names_b = [b.name for b in other.data.bones]
    log("same names/order:", names_a == names_b, "set equal:", set(names_a) == set(names_b))
    nbad = 0
    for b in arm.data.bones:
        b2 = other.data.bones.get(b.name)
        if b2 is None:
            log("missing", b.name)
            continue
        dh = (b.head_local - b2.head_local).length
        dt = (b.tail_local - b2.tail_local).length
        # axis difference
        m1 = b.matrix_local.to_3x3(); m2 = b2.matrix_local.to_3x3()
        dax = max((m1.col[i] - m2.col[i]).length for i in range(3))
        par_ok = (b.parent.name if b.parent else None) == (b2.parent.name if b2.parent else None)
        if dh > 1e-4 or dax > 1e-3 or not par_ok:
            nbad += 1
            if nbad <= 80:
                log("  DIFF %-18s dhead=%.4f dtail=%.4f daxis=%.4f parent_ok=%s head_ual=%s head_other=%s" % (
                    b.name, dh, dt, dax, par_ok, tuple(round(v, 3) for v in b.head_local), tuple(round(v, 3) for v in b2.head_local)))
    log("bones differing:", nbad)
