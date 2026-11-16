"""Batch icon renderer.  ONE Blender run renders many icons:

  & Y:\\tools\\blender\\run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\\tools\\blender\\icons\\render_icons.py','--','<sel>',...)

<sel> is an icon id (helm_3), a prefix with * (gem_*), or a category with a colon (item:  marker:  all:).  Add
`force` to re-render icons whose raw PNG already exists, `res=1024` to override the raw size.
Raw renders go to Y:\\assets_src\\icons_render\\<category>\\<id>.png; Y:\\tools\\icons\\post.py turns them into the masters.
"""
import bpy, sys, os, time, fnmatch, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import iconlib as I
import registry as REG
import importlib
for _m in ('props_items', 'props_misc', 'props_fx', 'props_armor', 'props_tokens', 'props_markers'):
    try:
        importlib.import_module(_m)             # each module fills REG.ICONS
    except Exception:
        print('ICON-MODULE-FAIL %s' % _m); traceback.print_exc()

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
force = "force" in args
res_over = next((int(a[4:]) for a in args if a.startswith("res=")), None)
sels = [a for a in args if a != "force" and not a.startswith("res=")]
todo = []
for iid, (cat, fn, opt) in REG.ICONS.items():
    for s in sels:
        if s == "all:" or s == cat + ":" or fnmatch.fnmatch(iid, s):
            todo.append(iid); break
print("ICONS to render: %d" % len(todo))
I.reset()
t0 = time.time(); done = 0; failed = []
for iid in todo:
    cat, fn, opt = REG.ICONS[iid]
    out = os.path.join(I.RAW, cat, iid + ".png")
    if os.path.isfile(out) and not force:
        continue
    try:
        I.clear()
        r = fn() or {}
        o = dict(opt); o.update(r)
        I.shoot(cat, iid, res=res_over or o.get("res", 512), fill=o.get("fill", 0.80), samples=o.get("samples", 40))
        done += 1
    except Exception:
        failed.append(iid)
        print("ICON-FAIL %s\n%s" % (iid, traceback.format_exc()))
print("ICONS rendered %d, failed %d %s, %.1f s" % (done, len(failed), failed, time.time() - t0))
