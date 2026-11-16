"""Development build of Osaros v2 into osaros_v2\\out (never the game folder):
   blender -b --factory-startup --python dev_build.py -- [--only=head,body,cloth,weapon] [--nobake] [--size=1024] [--id=name]"""
import sys, os, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE)); sys.path.insert(0, HERE)
import monlib as M

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
fl = {}
for a in args:
    if a.startswith('--'):
        k, _, v = a[2:].partition('=')
        fl[k] = v or '1'
try:
    import ov2_build as B
    only = [x for x in fl.get("only", "head,body,cloth,weapon").split(",") if x]
    M.log("sections:", only, "flags:", fl)
    C, notes = B.build(only=only, cid=fl.get('id', 'boss_osaros'))
    C.export_dir = os.path.join(HERE, "out")
    if 'size' in fl:
        for k in C.sizes:
            C.sizes[k] = int(fl['size'])
    C.finalize(bake='nobake' not in fl, export=True, notes=notes)
    M.log("BUILD OK", C.id)
except Exception:
    traceback.print_exc()
    M.log("BUILD FAILED")
    sys.exit(1)
