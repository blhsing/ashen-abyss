r"""Blender entry point: models and renders UI pieces.
   & Y:\tools\blender\run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\tools\uiart\render.py','--','--style','A','--pieces','explore')
   (or Y:\tools\uiart\render.ps1 -Style A -Pieces explore)
Options: --style A[,B,C]   --pieces explore|all|name,name   --skip-existing   --samples N   --scale 0.5 (quick preview)
Output:  Y:\assets_src\ui_gen\render\<style>\<piece>.png (16-bit RGBA; the field is a shadow catcher → alpha shadows)
"""
import sys, os, time, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path: sys.path.insert(0, HERE)
import bpy
import kit, styles, pieces

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

style_keys = str(opt('--style', 'A')).split(',')
which = str(opt('--pieces', 'explore'))
skip = bool(opt('--skip-existing', False))
samples = int(opt('--samples', 0) or 0)
scale = float(opt('--scale', 1.0))
out_root = str(opt('--out', r"Y:\assets_src\ui_gen\render"))

names = pieces.EXPLORE if which == 'explore' else (pieces.ORDER if which == 'all' else which.split(','))
STYLES = {'A': styles.StyleA, 'B': styles.StyleB, 'C': styles.StyleC}

bpy.ops.wm.read_factory_settings(use_empty=True)
kit.setup()
t_all = time.time()
done = failed = 0
for sk in style_keys:
    S = STYLES[sk]()
    kit.MATS.clear()
    try:
        S.mats(kit)
        S.finish(kit)
    except Exception:
        traceback.print_exc(); print("UIART FAIL materials of style", sk); failed += 1; continue
    out_dir = os.path.join(out_root, sk)
    os.makedirs(out_dir, exist_ok=True)
    for n in names:
        if n not in pieces.PIECES:
            print("UIART unknown piece", n); failed += 1; continue
        m = pieces.PIECES[n]
        path = os.path.join(out_dir, n + ".png")
        if skip and os.path.exists(path):
            continue
        t0 = time.time()
        try:
            kit.begin(m['w'], m['h'])
            m['fn'](kit, S, m)
            sc = bpy.context.scene
            sc.render.resolution_percentage = int(round(100 * scale))
            kit.render(path, samples or m.get('samples', 96))
            done += 1
            print("UIART ok %s/%s %dx%d %.1fs" % (sk, n, m['w'], m['h'], time.time() - t0))
        except Exception:
            traceback.print_exc()
            print("UIART FAIL %s/%s" % (sk, n))
            failed += 1
        sys.stdout.flush()
print("UIART done=%d failed=%d seconds=%.0f" % (done, failed, time.time() - t_all))
