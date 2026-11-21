"""Merge _manifest/*.json (one per built base) into gear_manifest_weapons.json (contract section 6: one entry per model).
Plain Python (no Blender needed):  python merge_manifest.py
Written to game/assets/gear/weapons/ (the ART-W brief) and game/assets/gear/ (next to where the contract puts
gear_manifest.json), identical content."""
import os, json, glob
HERE = os.path.dirname(os.path.abspath(__file__))
ORDER = ["sword1", "axe1", "mace1", "dagger", "wand", "scythe1", "flail", "fist", "sceptre",
         "sword2", "axe2", "mace2", "staff", "polearm", "scythe2", "bow", "crossbow",
         "shield", "focus", "totem", "grimoire", "lantern", "ring", "amulet", "gem"]
OFF = ("shield", "focus", "totem", "grimoire", "lantern")
bases = {}
for p in glob.glob(os.path.join(HERE, "_manifest", "*.json")):
    with open(p, encoding="utf8") as f:
        d = json.load(f)
    bases[d["base"]] = d
entries = []
for b in sorted(bases, key=lambda k: (ORDER.index(k) if k in ORDER else 99, k)):
    for e in bases[b]["entries"]:
        if os.path.isfile(os.path.join(r"Y:\game\assets\gear\weapons", e["id"] + ".glb")):
            e = dict(e)
            # additive convenience field: which equipment slot family the model belongs to
            e["slot"] = ("offhand" if b in OFF else "mainhand") if e["kind"] == "weapon" else (b if e["kind"] == "jewel" else "gem")
            entries.append(e)
txt = "[\n" + ",\n".join(" " + json.dumps(e, ensure_ascii=False) for e in entries) + "\n]\n"
for out in (r"Y:\game\assets\gear\weapons\gear_manifest_weapons.json", r"Y:\game\assets\gear\gear_manifest_weapons.json"):
    with open(out, "w", encoding="utf8", newline="\n") as f:
        f.write(txt)
print("manifest: %d entries, %d bases" % (len(entries), len(bases)))
for b in sorted(bases, key=lambda k: (ORDER.index(k) if k in ORDER else 99, k)):
    d = bases[b]
    print("  %-9s atlas %4d  %s" % (b, d["atlas_px"], "  ".join("%s:%d" % (e["id"].split("_")[-1], e["tris"]) for e in d["entries"])))
