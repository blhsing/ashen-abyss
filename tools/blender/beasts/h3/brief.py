"""print the art briefs of bestiary types:  python -I brief.py <id> [<id>...]"""
import json, sys
sys.stdout.reconfigure(encoding='utf8')
d = json.load(open(r"Y:\game\data\bestiary.json", encoding="utf8"))
ids = sys.argv[1:]
for t in d["types"]:
    if t["id"] in ids:
        for k in ("id", "model", "name_zh", "name_en", "family", "rig", "variant_of", "scale", "speed", "role", "ai_traits", "anims", "silhouette"):
            print("%s: %s" % (k, t.get(k)))
        print(sorted(t.keys()))
        print("-" * 40)
