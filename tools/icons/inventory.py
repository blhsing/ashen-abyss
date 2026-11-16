"""Writes Y:\tools\icons\icons.json (id, category, what it depicts, size, method, where used) and prints done / missing counts."""
import json, os, sys
sys.path.insert(0, os.path.join(os.environ.get("LOCALAPPDATA", ""), "AshenAbyssTools", "pylibs"))
from PIL import Image
R = r"Y:\game\assets\icons\r"
USE = dict(item="inventory, stash, vendors, loot tooltips, paper doll (Icons.ForItem / ForKind)", misc="currency row, HUD potion, menus (Icons.Get)",
           marker="minimap (HudMinimap), area / world map and legend (MapUi), NPC services", paragon="paragon boards and glyph list (ParagonUi)",
           crest="class selection screen, title screen (MainMenu)", skill="action bar, skill tree, buff row, class screen (Icons.ForSkill)",
           passive="skill tree passive nodes (Icons.ForPassive)", keypassive="skill tree key passives (Icons.ForPassive)", spec="skill tree specialisations (Icons.ForSpec)",
           status="buff row afflictions (HudBuffs)", buff="buff row flag buffs (HudBuffs)", stat="generic buffs, passive nodes by stat, skill category fallbacks",
           affix="elite affixes (nameplates / boss bar; Icons.ForAffix)", shrine="shrine blessings in the buff row (Icons.ForBuff)", hud="HUD action bar evade slot, town portal")
out = []
for j in json.load(open(r"Y:\tools\icons\gen_jobs.json", encoding="utf-8")):
    for cid, sub in zip(j["cells"], j["subjects"]):
        cat, iid = cid.split("/")
        if cat == "crestgen":
            continue
        out.append(dict(id=iid, category=cat, depicts=sub, size=256, method="imagegen", sheet=j["id"], used=USE.get(cat, "")))
B = {"item": ["wpn_%s_%d" % (k, t) for k in ("greatsword", "staff", "crossbow", "scythe", "club", "gauntlet", "swordshield", "grimoire") for t in range(1, 6)]
     + ["%s_%d" % (k, t) for k in ("helm", "chest", "gloves", "pants", "boots", "amulet", "ring") for t in range(1, 6)]
     + ["gem_%s_%d" % (k, t) for k in ("ruby", "sapphire", "topaz", "emerald", "amethyst", "skull") for t in (1, 2, 3)]
     + ["u_regret", "u_stargazer", "u_owlbeak", "u_reaper", "u_snuffer", "u_shovel", "u_candle", "u_emberheart"],
     "misc": "potion gold scrap dust core stash chest key quest map inventory settings lock paragon".split(),
     "marker": "quest objective boss elite bossdoor entrance exit stronghold waypoint beacon brazier portal riftstone event altar shrine chest stash idol tierstatue prisoner elder smith merchant occultist jeweler villager npc start hero".split(),
     "paragon": ["node_" + n for n in "start normal magic rare legendary socket gate".split()] + ["g_" + n for n in "steel kiln bastion orbit tide thrall eye gale venom vigil surge reaper".split()],
     "crest": "emberblade astromancer hunter soulbinder wildsoul forgebody lampknight pactbinder title".split()}
for cat, ids in B.items():
    for iid in ids:
        out.append(dict(id=iid, category=cat, depicts="see the builder docstring in Y:\tools\blender\icons\props_*.py", size=512 if cat == "crest" else 256, method="blender", used=USE.get(cat, "")))
done = {}; miss = {}; bad = []
for e in out:
    p = os.path.join(R, e["category"], e["id"] + ".png")
    ok = False
    if os.path.isfile(p):
        try:
            im = Image.open(p); im.load(); ok = im.size == (e["size"], e["size"]) and im.mode == "RGBA"
        except Exception:
            ok = False
        if not ok:
            bad.append(p)
    e["done"] = ok
    (done if ok else miss).setdefault(e["category"], []).append(e["id"])
json.dump(out, open(r"Y:\tools\icons\icons.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("icons.json: %d icons" % len(out))
for c in sorted(set(e["category"] for e in out)):
    print("  %-10s done %3d  missing %s" % (c, len(done.get(c, [])), miss.get(c, [])))
print("bad files:", bad)
known = {(e["category"], e["id"]) for e in out}
extra = [os.path.join(d, f) for d, _, fs in os.walk(R) for f in fs if f.endswith(".png") and (os.path.basename(d), f[:-4]) not in known]
print("not in inventory:", extra)
