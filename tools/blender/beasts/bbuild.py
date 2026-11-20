"""Beast build driver.
  run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\\tools\\blender\\beasts\\bbuild.py','--','<id>',...,['--nobake'],['--path=<extra dir>'],['--spec=<module>'])
ids are bestiary type ids (dusk_hound) or model names (mon_dusk_hound). Spec modules export BUILD = {id: fn(bake)}.
--nobake writes out\\<model>.glb with flat preview materials (fast; for rig / clip debugging)."""
import sys, os, importlib, traceback, time
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
flags = [a for a in args if a.startswith('--')]
ids = [a[4:] if a.startswith('mon_') else a for a in args if not a.startswith('--')]
bake = '--nobake' not in flags
mods = ['spec_b', 'spec_c', 'spec_d', 'spec_d2', 'spec_e', 'spec_f']
for f in flags:
    if f.startswith('--path='):
        sys.path.insert(0, f[7:])
    if f.startswith('--spec='):
        mods = f[7:].split(',') + mods
import blib as B
import bparts
if any(f.startswith('--out=') for f in flags):
    B.set_work([f[6:] for f in flags if f.startswith('--out=')][0])
BUILD = {}
for m in mods:
    try:
        mod = importlib.import_module(m)
    except ModuleNotFoundError as e:
        if e.name != m:
            raise
        continue
    for k, v in mod.BUILD.items():
        BUILD.setdefault(k, v)
if not ids:
    print("[beast] available:", sorted(BUILD))
ok = []
bad = []
for i in ids:
    t = time.time()
    try:
        BUILD[i](bake)
        ok.append(i)
        print("[beast] BUILD OK %s %.1fs" % (i, time.time() - t))
    except Exception:
        traceback.print_exc()
        bad.append(i)
        print("[beast] BUILD FAILED", i)
    sys.stdout.flush()
print("[beast] SUMMARY ok=%s failed=%s" % (ok, bad))
