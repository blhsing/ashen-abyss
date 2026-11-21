"""Build one gear base:   blender -b --factory-startup --python build.py -- <base> [variants] [--fast] [--nosheet]

variants: digits, e.g. 2 (only v2) or 123 (default: every variant the base defines).
--fast:   no bake / export; flat-colour look-dev sheet to Y:\\shots\\gear\\weapons\\_fast_<base>.jpg
Full run: bakes the shared atlas, exports <prefix>_<base>_v<k>.glb + textures to Y:\\game\\assets\\gear\\weapons,
          writes _manifest\\<prefix>_<base>.json and the contact sheet Y:\\shots\\gear\\weapons\\<base>.jpg
"""
import sys, os, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gearparts import *
import defs
import sheet


def build_one(base, args):
    spec = defs.DEFS[base]
    allv = spec.get("variants", (1, 2, 3))
    variants = [int(c) for c in args[1]] if len(args) > 1 and args[1].isdigit() else list(allv)
    variants = [v for v in variants if v in allv]
    fast = "--fast" in args
    log("==== build %s variants=%s fast=%s" % (base, variants, fast))
    begin()
    builders = []
    for v in variants:
        w = W(base, v)
        w.set(**spec.get("info", {}))
        spec["fn"](w, v)
        builders.append(w)
        log("   built v%d: %d parts, %d tris (before bevels)" % (v, sum(1 for o in PARTS if o["owner"] == w.owner),
                                                                 sum(mk.tri_count(o) for o in PARTS if o["owner"] == w.owner)))
    prefix = spec.get("prefix", "wpn")
    if fast:
        fast_materials()
        groups = []
        for w in builders:
            objs = [o for o in PARTS if o["owner"] == w.owner]
            groups.append(("v%d  %d tris" % (w.v, sum(mk.tri_count(o) for o in objs)), objs))
        sheet.render(groups, os.path.join(GEAR_SHOTS, "_fast_%s.jpg" % base), tmp_tag="fast_" + base, **dict(spec.get("sheet") or {}))
        return
    finish(base, builders, prefix=prefix, texel=spec.get("texel", 0.0011), cap=spec.get("cap", 1024),
           min_size=spec.get("min_size", 256), kind=spec.get("kind", "weapon"), ids=spec.get("ids"), tex_name=spec.get("tex_name"),
           entry_base=spec.get("entry_base"))
    if "--nosheet" not in args:
        sheet.render_exported(base, prefix, spec.get("sheet"))


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    bases = list(defs.DEFS.keys()) if args[0] == "all" else args[0].split(",")
    failed = []
    for b in bases:
        try:
            build_one(b, args)
        except Exception:
            log("BUILD FAILED %s: %s" % (b, traceback.format_exc()))
            failed.append(b)
    if failed:
        log("FAILED BASES: %s" % ",".join(failed))
        raise SystemExit(1)


main()
