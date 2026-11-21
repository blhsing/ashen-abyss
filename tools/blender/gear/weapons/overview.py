"""Montage of already rendered sheet views:  blender -b --python overview.py -- <out name> <cell height> <kind> <tag> [<tag> ...]
kind: face | quarter | icon_0 ... | icons (all icons of each tag)   tags: the tmp tags of sheet.render (e.g. fast_axe1 or axe1). Output: shots\gear\weapons\<out name>.jpg"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gearlib import *
import sheet

args = sys.argv[sys.argv.index('--') + 1:]
out, ch, kind, tags = args[0], int(args[1]), args[2], args[3:]
cells = []
import glob
paths = []
for t in tags:
    if kind == "icons":            # every icon of the tag (icon_0, icon_1, ...)
        paths += sorted(glob.glob(os.path.join(GEAR_SHOTS, "_tmp", "%s_icon_*.png" % t)))
    else:
        paths.append(os.path.join(GEAR_SHOTS, "_tmp", "%s_%s.png" % (t, kind)))
for p in paths:
    if not os.path.isfile(p):
        log("overview: missing", p)
        continue
    img = bpy.data.images.load(p, check_existing=False)
    w, h = img.size
    bpy.data.images.remove(img)
    cw = max(8, int(round(w * ch / h)))
    cells.append(mats.read_img(p, (cw, ch))[..., :3])
rows, row, width = [], [], 0
for c in cells:
    if row and width + c.shape[1] > 1900:
        rows.append(sheet._hcat(row))
        row, width = [], 0
    row.append(c)
    width += c.shape[1]
if row:
    rows.append(sheet._hcat(row))
mats.save_rgb(sheet._stack(rows), os.path.join(GEAR_SHOTS, out + ".jpg"), 88)
log("OVERVIEW", os.path.join(GEAR_SHOTS, out + ".jpg"))
