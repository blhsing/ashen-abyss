"""Build one creature:  blender -b --factory-startup --python build.py -- <id> [--nobake] [--noexport]
Each creature lives in <id>.py and exposes build() -> (Creature, notes)."""
import sys, os, importlib, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import monlib as M

args = sys.argv[sys.argv.index('--') + 1:]
cid = args[0]
flags = args[1:]
try:
    mod = importlib.import_module(cid)
    C, notes = mod.build()
    C.finalize(bake='--nobake' not in flags, export='--noexport' not in flags, notes=notes)
    M.log("BUILD OK", cid)
except Exception:
    traceback.print_exc()
    M.log("BUILD FAILED", cid)
    sys.exit(1)
