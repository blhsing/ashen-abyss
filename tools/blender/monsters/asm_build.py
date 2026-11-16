"""Build creatures from their specs, one after another in ONE Blender process (fewer waits in the 2-process queue):
   blender -b --factory-startup --python asm_build.py -- <id> [<id> ...] [--nobake] [--noexport] [--verify] [--shots=<asm_shots args, '+' separated>]
(always through run_blender.ps1 -- use asm_batch.ps1).  Scratch specs (scratch: True) are written to asm_out.
--verify runs verify.py on the ids that built; --shots=--family=bone+--fresh=a,b then renders tiles and family sheets."""
import sys, os, traceback, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import monlib as M

args = sys.argv[sys.argv.index('--') + 1:]
ids = [a for a in args if not a.startswith('--')]
OUT0 = M.OUT_DIR
built = []
for cid in ids:
    try:
        import asm, asm_specs
        spec = asm_specs.get(cid)
        M.OUT_DIR = asm.SCRATCH if spec.get('scratch') else OUT0
        C, notes = asm.build(spec)
        st = C.finalize(bake='--nobake' not in args, export='--noexport' not in args, notes=notes)
        M.log("BUILD OK", cid, "tris", st['triangles'], "MB %.2f" % (st.get('bytes', 0) / 1048576.0))
        built.append(cid)
    except Exception:
        traceback.print_exc()
        M.log("BUILD FAILED", cid)
M.OUT_DIR = OUT0
argv0 = sys.argv[0]
if '--verify' in args and built and '--noexport' not in args:
    try:
        sys.argv = [argv0, '--'] + built
        runpy.run_path(os.path.join(HERE, 'asm_verify.py'), run_name='__main__')
    except Exception:
        traceback.print_exc()
    M.OUT_DIR = OUT0
for a in args:
    if a.startswith('--shots=') and '--noexport' not in args:
        try:
            sys.argv = [argv0, '--'] + a[len('--shots='):].split('+')
            runpy.run_path(os.path.join(HERE, 'asm_shots.py'), run_name='__main__')
        except Exception:
            traceback.print_exc()
if len(built) != len(ids):
    sys.exit(1)
