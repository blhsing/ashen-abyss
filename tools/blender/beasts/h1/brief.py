"""print the art briefs of bestiary types:  python.exe -I brief.py <id> ..."""
import json, sys
sys.stdout.reconfigure(encoding="utf8")
ids = sys.argv[1:]
d = json.load(open(r"Y:\game\data\bestiary.json", encoding="utf8"))
for t in d["types"]:
    if t["id"] in ids or (not ids and t.get("rig") == "C"):
        print("=" * 100)
        for k in ("id", "name_en", "name_zh", "family", "rig", "scale", "speed", "role", "ai_traits", "anims", "variant_of", "silhouette", "model"):
            if k in t:
                print("%-10s %s" % (k, t[k]))
