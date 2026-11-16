"""verify.py for assembler ids (scratch ids are checked in asm_out, the manifest is never written):
   blender -b --factory-startup --python asm_verify.py -- <id> [<id> ...]"""
import sys, os, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import monlib as M
import asm_specs

ids = sys.argv[sys.argv.index('--') + 1:]
scr = [i for i in ids if asm_specs.SPECS.get(i, {}).get('scratch')]
real = [i for i in ids if i not in scr]
for group, out in ((real, M.OUT_DIR), (scr, asm_specs.SCRATCH)):
    if group:
        M.OUT_DIR = out
        sys.argv = [sys.argv[0], '--'] + group + ['--nomanifest']
        runpy.run_path(os.path.join(HERE, 'verify.py'), run_name='__main__')
