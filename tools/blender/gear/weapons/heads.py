"""Head comparison of one-hand / two-hand pairs, as an icon that crops the haft would show them:

    blender -b --python heads.py -- [pair ...]        default pairs: axe1:axe2 mace1:mace2 scythe1:scythe2 sword1:sword2

For every pair and variant both models are framed on their striking end (the top `H` metres; whole weapon for swords)
at ONE common scale, so the picture shows the real difference in head size as well as in shape.
Writes Y:\\shots\\gear\\weapons\\heads.jpg (rows = v1, v2, v3; columns = the pairs, 1H left / 2H right).
"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gearlib import *
import sheet

PAIRS = [("axe1", "axe2"), ("mace1", "mace2"), ("scythe1", "scythe2"), ("sword1", "sword2")]
HEAD = {"axe": 0.62, "mace": 0.50, "scythe": 0.86, "sword": None}       # metres from the top; None = whole weapon


def head_box(objs, H):
    co = np.concatenate([get_co(o) @ np.array([list(r) for r in o.matrix_world])[:3, :3].T + np.array(o.matrix_world.translation) for o in objs])
    top = co[:, 2].max()
    if H is not None:
        co = co[co[:, 2] > top - H]
    return co.min(0), co.max(0)


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    pairs = [tuple(a.split(":")) for a in args] or PAIRS
    tmp = os.path.join(GEAR_SHOTS, "_tmp")
    rows = []
    for v in (1, 2, 3):
        cells = []
        for (a, b) in pairs:
            H = HEAD[a.rstrip("12")]
            boxes, groups = [], []
            for base in (a, b):
                reset_scene()
                objs = sheet.load_glb("wpn_%s_v%d" % (base, v))
                boxes.append(head_box(objs, H))
                groups.append(base)
            scale = max(max(float(mx[0] - mn[0]), float(mx[2] - mn[2])) for mn, mx in boxes) * 1.10
            for base, (mn, mx) in zip(groups, boxes):
                reset_scene()
                sheet.load_glb("wpn_%s_v%d" % (base, v))
                setup_render(300, 300, samples=24)
                setup_stage(floor=False, hdri="overcast", hdri_strength=0.5)
                k = bpy.data.objects.get(bpy.context.scene.get("stage_key", ""))
                if k is not None:
                    k.data.color = (1.0, 0.95, 0.90)
                cam = make_camera()
                c = (mn + mx) / 2
                aim_camera(cam, (float(c[0]), 0.0, float(c[2])), 180.0, 0.0, 12.0, ortho_scale=scale)
                aim_key(180.0)
                p = os.path.join(tmp, "head_%s_v%d.png" % (base, v))
                render_to(p)
                cells.append(mats.read_img(p)[..., :3])
        rows.append(sheet._hcat(cells, pad=4))
    mats.save_rgb(sheet._stack(rows, pad=6), os.path.join(GEAR_SHOTS, "heads.jpg"), 90)
    log("HEADS", os.path.join(GEAR_SHOTS, "heads.jpg"))


main()
