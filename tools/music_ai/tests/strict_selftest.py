"""Self-test of the strict bar: finalize an existing candidate into stage/selftest/strict and print the problems.
    python tests/strict_selftest.py <track> <seed> [strict_pc]"""
import json, os, sys
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import produce as P, tracks as T
tr = dict(T.BY_NAME[sys.argv[1]], strict=True)
stage = os.path.join(P.MA_ROOT, "stage")
flac = os.path.join(stage, "ace_xl", "cand", "%s__s%s.flac" % (tr["name"], sys.argv[2]))
ev = {"strict_pc": int(sys.argv[3]) if len(sys.argv) > 3 else None}
info = P.finalize(tr, flac, ev, os.path.join(stage, "selftest", "strict"), "selftest")
print(json.dumps(dict(key=info["ogg"]["key_est"], dynamics=info.get("dynamics"), problems=info["problems"])))
