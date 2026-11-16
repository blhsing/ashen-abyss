# Assemble Y:\game\assets\env\models\manifest.json and write Y:\credits\env.md.
#   inputs : mdl_spec.py (zones, build category), per-prop build stats (Y:\assets_src\env\models_build\<name>.json: source, flames, parts),
#            glb_measure.json (triangles / bounds / textures measured from the .glb files: run glb_measure.py first),
#            glb_verify.json (optional: Blender re-import check), Poly Haven info.json (authors), tex / sky manifests, fonts, icons.
# run: python -I Y:\tools\env\make_manifests.py      (pure Python; Blender's bundled python.exe works)
import json, os, struct, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mdl_spec import SPEC

MODELS = r"Y:\game\assets\env\models"
STAT = r"Y:\assets_src\env\models_build"
MEAS = r"Y:\assets_src\env\glb_measure.json"
VERIFY = r"Y:\assets_src\env\glb_verify.json"
PH_MODELS = r"Y:\assets_src\env\polyhaven\models"
TEXDIR = r"Y:\game\assets\env\tex"
TEX = TEXDIR + r"\manifest.json"
SKYDIR = r"Y:\game\assets\env\sky"
SKY = SKYDIR + r"\manifest.json"
FONTDIR = r"Y:\game\assets\fonts"
FONTS = FONTDIR + r"\fonts.json"
ICONS = r"Y:\game\assets\icons"
ICON_SRC_LICENCE = r"Y:\assets_src\env\game-icons\icons\license.txt"
CREDITS = r"Y:\credits\env.md"

ZONES = ["town", "wastes", "frost", "crypt", "mire", "sands", "sanctum", "abyss"]
CATS = ["tree", "rock", "rock_big", "clutter", "breakable", "furniture", "light", "pillar", "structure", "foliage", "weapon"]
BUDGET = {"small": 3000, "medium": 8000, "large": 20000}

LIGHT = {  # suggested OmniLight3D per light prop: (colour, range m, energy hint)
    "fire_pit": ("#ff8a3c", 9, "strong"), "candlestick": ("#ffb469", 3, "weak"), "candle_brass": ("#ffb469", 3, "weak"),
    "candelabra_3": ("#ffb469", 5, "medium"), "candelabra_5": ("#ffb469", 6, "medium"), "lantern_oil": ("#ffa550", 6, "medium"),
    "lantern_wood": ("#ffa550", 6, "medium"), "hanging_lamp": ("#ffb060", 5, "medium"), "chandelier": ("#ffc27a", 9, "strong"),
    "chandelier_iron": ("#ffb469", 8, "strong"), "lamp_post": ("#ffa550", 10, "strong"), "wall_lantern": ("#ffa550", 7, "medium"),
    "fire_barrel": ("#ff8a3c", 7, "strong"), "brazier_bowl": ("#ff8a3c", 9, "strong"),
}
NOTES = {
    "pickaxe": "modelled standing upright; rotate to lean or lay flat", "spade": "modelled standing upright", "axe": "modelled standing upright",
    "smith_hammer": "modelled standing upright", "saw": "modelled on edge", "sword": "modelled point-down upright", "war_hammer": "modelled upright",
    "mace": "modelled upright", "dagger": "dagger + scabbard side by side, upright", "shield": "modelled upright (lean against a wall)",
    "broom": "bristles use an alpha texture", "mirror_ornate": "wall mirror modelled upright; origin at its lowest point",
    "wall_lantern": "wall-mounted bracket lantern; origin is the wall mount point, bracket extends along +Z (Godot)",
    "chandelier": "hangs from origin (top); electric-candle style bulbs read as candles", "hanging_lamp": "hangs from origin (top of chain)",
    "chandelier_iron": "hangs from origin (top)", "fire_barrel": "rusty drum brazier; flame point is inside the rim",
    "fire_pit": "ring of stones with ash bed; add fire particles at the flame point",
    "chest": "lid is a separate node 'lid' pivoted on its back-bottom edge: rotate about X to open; meant for the loot-chest interactable, not for random scatter",
    "castle_door": "frame + two leaf nodes (door_left/door_right) pivoted on their outer edges: rotate about Y to swing",
    "iron_gate": "top bar/bolt + two leaf nodes (gate_left/gate_right) pivoted on their outer edges; also usable as fence panels",
    "fort_tower": "very large (backdrop scale); tiling wall/trim/plaster materials", "pier_deck": "flat plank deck tile: boardwalk / bridge segment",
    "pier_section": "deck on tall poles: origin at pole feet, deck is ~2.6 m up; sink poles into water/ground as needed",
    "pier_poles": "pole frame only", "pier_frame": "tall post frame (gallows / crane / gate frame)",
    "dead_tree_big": "source tree is ~19 m; delivered at 0.8 scale", "books_row": "20 books joined as one row",
    "quiver_tree_a": "leaf rosettes kept (desert succulent tree)",
    "brazier_bowl": "wide brass fire bowl (0.95 m); sits on the ground: put it on a procedural plinth / tripod for a standing brazier, add fire particles at the flame point",
    "colossus_head": "3 m marble bust: sanctum monument, or sink / tilt it in the dunes as a fallen colossus",
    "lion_monument": "2.2 m dark bronze lion head on a plinth: gate guardian", "bull_idol": "2.2 m dark bronze bull head with brass horns: cult idol",
    "journal_open": "open leather folio (lay it on a lectern / table)",
    "marsh_weed_a": "single tall stalk: scatter several close together", "marsh_weed_b": "single tall stalk: scatter several close together",
    "candelabra_5": "seven-flame standing candelabra (name kept from the first pass)",
}
for n in ("dead_pine_a", "dead_pine_b", "dead_pine_c"):
    NOTES[n] = "conifer sapling stripped of needles and scaled up ~4x to read as a dead bare conifer"
for n in ("spire_a", "spire_b", "spire_c", "shard_a", "basalt_boulder_a", "basalt_boulder_b"):
    NOTES[n] = "small jagged rock scan scaled up, base colour darkened to basalt; re-material with basalt/obsidian textures (triplanar) if preferred"
for n in ("dead_bush_a", "dead_bush_b", "dead_bush_c"):
    NOTES[n] = "leaves removed -> bare twiggy bush; twig cards are alpha-tested"
for n in ("snow_boulder_a", "snow_boulder_b", "snow_rock_a", "snow_rock_b", "snow_rock_c", "snow_stump", "snow_log"):
    NOTES[n] = "snow is baked into the albedo / normal map of the up-facing surfaces, so keep it upright (yaw rotation only)"
for n in ("mossy_log", "mossy_log_thin", "mossy_stump"):
    NOTES[n] = "moss is baked into the albedo / normal map of the up-facing surfaces, so keep it upright (yaw rotation only)"

# manifest category / kind. Build categories in mdl_spec.py are coarser (tree|rock|plant|breakable|light|furniture|container|structure|clutter).
KIND = {}
for n in ("dead_tree_a", "dead_tree_b", "dead_tree_big", "dead_quiver", "dead_trunk"):
    KIND[n] = ("tree", "dead_tree")
for n in ("dead_pine_a", "dead_pine_b", "dead_pine_c"):
    KIND[n] = ("tree", "dead_conifer")
for n in ("quiver_tree_a", "quiver_tree_b"):
    KIND[n] = ("tree", "desert_tree")
for n in ("fallen_log_a", "fallen_log_b", "snow_log", "mossy_log", "mossy_log_thin"):
    KIND[n] = ("clutter", "log")
for n in ("stump_a", "stump_b", "snow_stump", "mossy_stump"):
    KIND[n] = ("clutter", "stump")
for n in ("roots_big", "roots_flat_a", "roots_flat_b", "root_piece_a", "root_piece_b"):
    KIND[n] = ("clutter", "roots")
for n in ("branch_a", "branch_b", "branch_c", "dead_stub_a", "dead_stub_b"):
    KIND[n] = ("clutter", "branch")
for n in ("cliff_a", "cliff_b", "cliff_c", "cliff_d"):
    KIND[n] = ("rock_big", "cliff")
for n in ("spire_a", "spire_b", "spire_c", "shard_a"):
    KIND[n] = (None, "spire")
for n in ("dry_bush_a", "dry_bush_b", "dry_bush_c", "dry_bush_d", "dead_bush_a", "dead_bush_b", "dead_bush_c"):
    KIND[n] = ("foliage", "bush")
for n in ("fern_a", "fern_b", "fern_c", "fern_d"):
    KIND[n] = ("foliage", "fern")
for n in ("swamp_plant_a", "swamp_plant_b", "swamp_plant_c", "broadleaf_a", "broadleaf_b"):
    KIND[n] = ("foliage", "broadleaf")
for n in ("reed_a", "reed_b", "marsh_weed_a", "marsh_weed_b"):
    KIND[n] = ("foliage", "reed")
for n in ("statue_king", "colossus_head", "lion_monument", "bull_idol"):
    KIND[n] = ("structure", "statue")
for n in ("bust_marble", "bust_horse", "bust_lion", "bust_bull"):
    KIND[n] = ("clutter", "bust")
KIND["castle_door"] = ("structure", "door"); KIND["iron_gate"] = ("structure", "gate")
for n in ("fort_wall_a", "fort_wall_b", "fort_gate", "fort_corner", "fort_wall_end", "fort_stairs", "fort_tower"):
    KIND[n] = ("structure", "fort")
for n in ("pier_deck", "pier_poles", "pier_section", "pier_frame"):
    KIND[n] = ("structure", "pier")
for n in ("barrel", "barrel_old"):
    KIND[n] = ("breakable", "barrel")
for n in ("crate_a", "crate_b"):
    KIND[n] = ("breakable", "crate")
for n in ("vase_blue", "urn_a", "urn_b", "jug", "brass_urn", "brass_ewer", "clay_pot", "lidded_pot"):
    KIND[n] = ("breakable", "pot")
for n in ("basket_a", "basket_b"):
    KIND[n] = ("breakable", "basket")
for n in ("bucket", "tub"):
    KIND[n] = ("breakable", "bucket")
for n in ("barrel_broken_a", "barrel_broken_b", "barrel_stave", "barrel_lid"):
    KIND[n] = ("clutter", "debris")
for n in ("trough_a", "trough_b"):
    KIND[n] = ("furniture", "trough")
KIND["chest"] = ("furniture", "chest")
for n in ("cauldron", "brass_basin", "brass_vial", "brass_mug", "brass_pan", "goblets", "bowl_a", "bowl_b", "plate_wood"):
    KIND[n] = ("clutter", "tableware")
for n in ("books_row", "journal", "journal_open"):
    KIND[n] = ("clutter", "book")
for n in ("pickaxe", "spade", "axe", "smith_hammer", "saw", "broom", "vice"):
    KIND[n] = ("clutter", "tool")
for n in ("shield", "sword", "war_hammer", "mace", "dagger"):
    KIND[n] = ("weapon", "weapon")
for n in ("candlestick", "candle_brass", "candelabra_3", "candelabra_5"):
    KIND[n] = ("light", "candle")
for n in ("lantern_oil", "lantern_wood", "hanging_lamp", "wall_lantern", "lamp_post"):
    KIND[n] = ("light", "lantern")
for n in ("chandelier", "chandelier_iron"):
    KIND[n] = ("light", "chandelier")
for n in ("fire_pit", "fire_barrel", "brazier_bowl"):
    KIND[n] = ("light", "fire")
INTERACTIVE = {"chest": "loot chest", "castle_door": "door", "iron_gate": "gate"}
WALL = ("wall_lantern", "mirror_ornate")

PROCEDURAL = [  # not available as CC0 scans on Poly Haven / ambientCG: build from primitives + the listed texture sets
    {"item": "pillars / columns (round, square pier, broken stump, fallen drum)", "category": "pillar", "zones": ["crypt", "sanctum", "sands", "frost", "abyss", "town"],
     "textures": {"crypt": "big_blocks / gothic_stone", "sanctum": "marble_floor shaft + aged_gold capital, gothic_stone piers", "sands": "sandstone_blocks + tarnished_bronze bands",
                  "frost": "castle_blocks + ice_pale coating", "abyss": "basalt_layered / obsidian", "town": "rough_beam posts"}},
    {"item": "gravestones, grave slabs, broken markers, burial mounds", "category": "structure", "zones": ["wastes", "crypt"], "textures": "mossy_stone / grey_rock / drystone_wall"},
    {"item": "sarcophagi with separate lids, wall niches, coffins", "category": "structure", "zones": ["crypt", "sanctum", "sands"], "textures": "dark_marble / worn_marble / big_blocks; coffins dark_planks + rusty_iron"},
    {"item": "pews, lectern, altar, choir stalls, confessional", "category": "furniture", "zones": ["sanctum"], "textures": "varnished_wood / dark_panel_wood / red_velvet / aged_gold (journal_open sits on the lectern)"},
    {"item": "bells and bell frames, censers", "category": "structure", "zones": ["sanctum"], "textures": "tarnished_bronze / aged_gold / rough_beam"},
    {"item": "market stalls, awnings, tents, handcart, well, signposts, fences, palisade", "category": "structure", "zones": ["town", "wastes", "frost", "sands"],
     "textures": "old_planks / rough_beam / canvas / thatch / rope / log_wall / wicker"},
    {"item": "beacon tower (ember tower) and the great brazier of the hub; standing braziers (tripod / plinth under brazier_bowl), wall torches and sconces", "category": "light",
     "zones": ZONES, "textures": "rusty_iron / iron_plate / tarnished_bronze + fire particles"},
    {"item": "glowing fungus clusters, cattail / reed clumps, lily pads, hanging moss", "category": "foliage", "zones": ["mire"], "textures": "emissive shader for fungus caps; alpha cards or thin blades for reeds; moss"},
    {"item": "icicles, ice crystals and shards, snow drifts, frozen-over ruins", "category": "rock", "zones": ["frost"], "textures": "ice_pale / ice / snow_fresh (snow_* props are the baked snowy rocks)"},
    {"item": "snow / moss / ash overlay on any other prop (shader: blend by world-space up normal)", "category": "shader", "zones": ["frost", "mire", "wastes"], "textures": "snow_fresh / moss / ash_ground"},
    {"item": "obsidian spires and crystal clusters, lava rocks, hanging chains, cages, ember vents", "category": "rock", "zones": ["abyss"], "textures": "obsidian / basalt / lava_crust (emission) / rusty_iron"},
    {"item": "bone piles, skulls, cobwebs, iron bars and grates, wall chains", "category": "clutter", "zones": ["crypt", "wastes", "abyss"], "textures": "worn_marble tinted bone; alpha cards for cobwebs; rusty_iron"},
    {"item": "obelisks, broken column drums, sand drifts, brass gears / pipes / machinery, brass doors", "category": "structure", "zones": ["sands"], "textures": "sandstone_worn / sandstone_blocks / tarnished_bronze / corroded_bronze / sand_fine"},
    {"item": "banners, drapes, carpets and aisle runners, sacks, bedrolls", "category": "clutter", "zones": ["sanctum", "town", "crypt"], "textures": "red_velvet / red_carpet / canvas / burlap"},
    {"item": "boardwalk railings, rope bridges, mooring posts, small boats", "category": "structure", "zones": ["mire", "town"], "textures": "mossy_planks / old_planks / rope (pier_* props are the deck pieces)"},
    {"item": "arches, vaults, buttresses, window tracery, stairs, rubble piles, ruined wall chunks", "category": "structure", "zones": ZONES, "textures": "castle_blocks / gothic_stone / rough_stone_wall / old_brick"},
]


def ph_info(sid):
    p = os.path.join(PH_MODELS, sid, "info.json")
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def font_names(path):
    """name-table strings (copyright, manufacturer, designer, version, licence URL) straight from the font file."""
    data = open(path, "rb").read()
    num = struct.unpack(">H", data[4:6])[0]
    off = None
    for i in range(num):
        tag, _cs, o, ln = struct.unpack(">4sIII", data[12 + 16 * i: 28 + 16 * i])
        if tag == b"name":
            off = o
    out = {}
    if off is None:
        return out
    _fmt, count, so = struct.unpack(">HHH", data[off:off + 6])
    for i in range(count):
        pid, eid, lid, nid, ln, no = struct.unpack(">6H", data[off + 6 + 12 * i: off + 18 + 12 * i])
        raw = data[off + so + no: off + so + no + ln]
        if pid == 3 and lid == 0x409:
            out[nid] = raw.decode("utf-16-be", "replace").strip()
        elif pid == 1 and nid not in out:
            out[nid] = raw.decode("mac_roman", "replace").strip()
    return out


def jpeg_size(b):
    i = 2
    while i < len(b) - 9:
        if b[i] != 0xFF:
            i += 1; continue
        m = b[i + 1]
        if m in (0xC0, 0xC1, 0xC2):
            h, w = struct.unpack(">HH", b[i + 5:i + 9]); return (w, h)
        if m == 0xFF:
            i += 1; continue
        if m in (0xD8, 0xD9) or 0xD0 <= m <= 0xD7:
            i += 2; continue
        i += 2 + struct.unpack(">H", b[i + 2:i + 4])[0]
    return None


def klass(tris):
    return "small" if tris <= BUDGET["small"] else "medium" if tris <= BUDGET["medium"] else "large" if tris <= BUDGET["large"] else "over"


def main():
    meas = json.load(open(MEAS, encoding="utf-8"))
    ver = json.load(open(VERIFY, encoding="utf-8")) if os.path.exists(VERIFY) else {}
    props = []; issues = []; missing = []
    for sp in SPEC:
        name = sp["name"]
        p = os.path.join(STAT, name + ".json")
        fp = os.path.join(MODELS, name, name + ".glb")
        if not os.path.exists(p) or not os.path.exists(fp) or name not in meas:
            missing.append(name); continue
        st = json.load(open(p, encoding="utf-8"))
        g = meas[name]
        for pr in g["problems"]:
            issues.append("%s: %s" % (name, pr))
        if ver and name not in ver:
            issues.append("%s: not re-imported by glb_verify.py" % name)
        for pr in ver.get(name, {}).get("problems", []):
            issues.append("%s: (import) %s" % (name, pr))
        if ver.get(name) and ver[name]["triangles"] != g["triangles"]:
            issues.append("%s: importer sees %d triangles, file has %d" % (name, ver[name]["triangles"], g["triangles"]))
        if st["triangles"] != g["triangles"]:
            issues.append("%s: build stats say %d triangles, file has %d" % (name, st["triangles"], g["triangles"]))
        if any(abs(a - b) > 0.02 + 0.015 * b for a, b in zip(st["size"], g["size"])):   # the file is authoritative; stats may include a stray vertex
            issues.append("%s: build stats size %s != measured %s" % (name, st["size"], g["size"]))
        size = g["size"]
        cat, kind = KIND.get(name, (None, None))
        if cat is None:
            base = {"plant": "foliage", "container": "clutter"}.get(sp["cat"], sp["cat"])
            if base == "rock":
                base = "rock_big" if (max(size[0], size[2]) >= 2.7 or size[1] >= 1.7) else "rock"
            cat = base
        if kind is None:
            kind = {"rock": "boulder", "rock_big": "boulder", "furniture": "furniture"}.get(cat, cat)
            if cat == "rock" and max(size) < 0.7:
                kind = "stone"
        assert cat in CATS, (name, cat)
        info = ph_info(sp["src"])
        authors = ", ".join(info.get("authors", {}).keys())
        if not authors:
            issues.append("%s: no author recorded for %s" % (name, sp["src"]))
        hang = st["origin"].startswith("top")
        small_top = kind in ("tableware", "book", "bust") or (cat == "light" and max(size) < 0.6) or name in ("smith_hammer", "vice", "dagger")
        mount = "ceiling" if hang else "wall" if name in WALL else "surface" if small_top else "floor"
        blocks = (cat in ("tree", "rock_big", "pillar", "structure")
                  or (cat in ("furniture", "breakable") and max(size) >= 0.5)
                  or (cat == "rock" and max(size[0], size[2]) >= 1.0)
                  or (cat == "clutter" and kind in ("log", "stump", "roots") and max(size[0], size[2]) >= 1.3 and size[1] >= 0.5)
                  or name in ("fire_pit", "fire_barrel", "brazier_bowl", "lamp_post"))
        if mount != "floor" or name in ("pier_deck",):
            blocks = False
        alpha_modes = sorted(set(m["alpha"] for m in g["materials"]))
        e = {
            "name": name, "path": "res://assets/env/models/%s/%s.glb" % (name, name),
            "source": "Poly Haven", "source_id": sp["src"], "source_url": "https://polyhaven.com/a/" + sp["src"],
            "author": authors, "licence": "CC0 1.0",
            "category": cat, "kind": kind, "zones": [z for z in ZONES if z in sp["zones"]],
            "triangles": g["triangles"], "budget": klass(g["triangles"]),
            "size": size, "bounds": {"min": [round(v, 3) for v in g["min"]], "max": [round(v, 3) for v in g["max"]]},
            "origin": st["origin"], "below_ground": round(max(0.0, -g["min"][1]), 3) if not hang and name not in WALL else 0.0,
            "mount": mount, "blocks": bool(blocks), "radius": round(max(size[0], size[2]) / 2, 2),
            "texture_px": g["max_texture_px"], "materials": len(g["materials"]),
            "alpha": "mask" if "MASK" in alpha_modes else "blend" if "BLEND" in alpha_modes else "opaque",
            "casts_light": bool(st.get("casts_light")), "bytes": g["bytes"],
        }
        if e["casts_light"]:
            col, rng, en = LIGHT.get(name, ("#ffb060", 6, "medium"))
            fl = [[0.0 if abs(v) < 5e-4 else v for v in f] for f in (st.get("flames") or [])]
            pos = fl[0] if len(fl) == 1 else [round(sum(f[i] for f in fl) / len(fl), 3) for i in range(3)] if fl else None
            e["light"] = {"color": col, "range_m": rng, "energy": en, "flame_pos": pos, "flame_positions": fl}
            if not fl:
                issues.append("%s: light prop without a flame position" % name)
            for f in fl:
                if any(f[k] < g["min"][k] - 0.35 or f[k] > g["max"][k] + 0.35 for k in range(3)):
                    issues.append("%s: flame %s lies outside the bounds" % (name, f))
        if name in INTERACTIVE:
            e["interactive"] = INTERACTIVE[name]
        if st.get("parts"):
            e["parts"] = st["parts"]
        if st.get("derived"):
            e["derived"] = st["derived"]
        if st.get("removed"):
            e["removed"] = st["removed"]
        if st.get("rescaled"):
            e["rescaled"] = True
        if st.get("cap"):
            e["baked_layer"] = st["cap"]["kind"]
            if st["cap"]["kind"] == "moss":
                e["also_uses"] = {"source": "ambientCG", "source_id": "Moss002", "source_url": "https://ambientcg.com/view?id=Moss002", "author": "Lennart Demes (ambientCG)", "licence": "CC0 1.0"}
        if any(c["attr"] == "COLOR_0" and min(c["min"][:3]) < 0.98 for c in g.get("vertex_colors", [])):
            e["vertex_color"] = "COLOR_0 tints the albedo (authored darkening)"
        if name in NOTES:
            e["note"] = NOTES[name]
        if g["triangles"] > BUDGET["large"]:
            issues.append("%s: %d triangles over the large budget" % (name, g["triangles"]))
        props.append(e)
    extra = sorted(d for d in os.listdir(MODELS) if os.path.isdir(os.path.join(MODELS, d)) and d not in set(s["name"] for s in SPEC))
    for d in extra:
        issues.append("%s: folder exists but is not in mdl_spec.py" % d)
    by_zone = {z: {c: [p["name"] for p in props if z in p["zones"] and p["category"] == c] for c in CATS} for z in ZONES}
    tex2k = [p["name"] for p in props if p["texture_px"] > 1024]
    doc = {
        "about": "Game-ready props for Ashen Abyss (glTF binary, +Y up, metres, textures embedded; Godot uses glTF axes as they are). "
                 "triangles / size / bounds are measured from the .glb files. size = bounding box [x, y(height), z] in metres; bounds.min/max are local Godot coordinates. "
                 "origin: base centre on the ground unless stated ('top centre' = hang point, 'as authored' = wall mount point); below_ground = how far the mesh extends under y=0 (natural props are meant to be sunk). "
                 "category is one of categories[]; kind is a finer label. mount: floor | surface (table top or floor) | wall | ceiling. blocks = suggested nav obstacle, radius = half the larger horizontal extent. "
                 "casts_light props carry light{color, range_m, energy, flame_pos, flame_positions}: local Godot coordinates for OmniLight3D + fire particles (flame_pos = centre of all flames). "
                 "alpha 'mask' = alpha scissor, 'blend' = glass. interactive = meant for a gameplay object, keep out of random scatter. parts = separate child nodes with their pivots. "
                 "by_zone[zone][category] lists prop names for the level themes; build_procedurally lists what the CC0 sources do not offer.",
        "count": len(props), "categories": CATS, "zones": ZONES, "budget_triangles": BUDGET,
        "totals": {"bytes": sum(p["bytes"] for p in props), "triangles": sum(p["triangles"] for p in props),
                   "by_category": {c: sum(1 for p in props if p["category"] == c) for c in CATS},
                   "by_zone": {z: sum(1 for p in props if z in p["zones"]) for z in ZONES},
                   "by_budget": {b: sum(1 for p in props if p["budget"] == b) for b in ("small", "medium", "large", "over")},
                   "textures_over_1024px": tex2k},
        "by_zone": by_zone, "build_procedurally": PROCEDURAL, "props": props,
    }
    json.dump(doc, open(os.path.join(MODELS, "manifest.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print("props: %d  missing: %s  total %.1f MB  tris max %d" % (len(props), missing, doc["totals"]["bytes"] / 1e6, max(p["triangles"] for p in props)))
    print("by category:", doc["totals"]["by_category"])
    print("by zone:", doc["totals"]["by_zone"])
    print("by budget:", doc["totals"]["by_budget"], " 2K textures:", tex2k)
    print("issues: %d" % len(issues))
    for i in issues:
        print("   ", i)

    # ---------------------------------------------------------------- texture manifest check
    tdoc = json.load(open(TEX, encoding="utf-8"))
    tex = tdoc["sets"]
    tnames = [t["name"] for t in tex]
    tdirs = sorted(d for d in os.listdir(TEXDIR) if os.path.isdir(os.path.join(TEXDIR, d)))
    tprob = []
    for d in tdirs:
        if d not in tnames:
            tprob.append("%s: folder not in manifest" % d)
        for mp in ("albedo", "normal", "arm", "height"):
            f = os.path.join(TEXDIR, d, mp + ".jpg")
            if not os.path.isfile(f) or os.path.getsize(f) < 1024 or open(f, "rb").read(2) != b"\xff\xd8":
                tprob.append("%s: %s.jpg missing or not a JPEG" % (d, mp)); continue
            b = open(f, "rb").read()
            wh = jpeg_size(b)
            want = next((t.get("resolution") for t in tex if t["name"] == d), None)
            if wh is None or (want and wh != (want, want)):
                tprob.append("%s: %s.jpg is %s, manifest says %s px" % (d, mp, wh, want))
            if b[-2:] != b"\xff\xd9" or any(not any(b[i:i + 4096]) for i in range(0, len(b) - 4095, 4096)):
                tprob.append("%s: %s.jpg looks truncated / zero-filled" % (d, mp))
    for t in tex:
        if t["name"] not in tdirs:
            tprob.append("%s: in manifest but no folder" % t["name"])
        for k in ("source", "source_id", "source_url", "author", "licence"):
            if not t.get(k):
                tprob.append("%s: manifest field %s empty" % (t["name"], k))
    print("texture sets: %d folders, %d manifest entries, %.1f MB, problems: %s" % (
        len(tdirs), len(tex), sum(os.path.getsize(os.path.join(r, f)) for r, _, fs in os.walk(TEXDIR) for f in fs) / 1e6, tprob or "none"))

    # ---------------------------------------------------------------- credits
    L = []
    A = L.append
    sky = json.load(open(SKY, encoding="utf-8"))["skies"]
    fonts = json.load(open(FONTS, encoding="utf-8"))
    icon_lines = [l for l in open(os.path.join(ICONS, "index.txt"), encoding="utf-8").read().splitlines() if l.strip()]
    A("# Environment, font and icon credits (燼淵 Ashen Abyss)\n")
    A("Third-party assets used for the environment art, UI fonts and UI icons. Everything listed here is CC0 1.0, SIL OFL 1.1 or CC BY 3.0. "
      "Raw downloads (with their licence files and API metadata) are kept under `Y:\\assets_src\\env\\`.\n")
    A("| section | count | licence | location |\n|---|---|---|---|")
    A("| PBR texture sets | %d | CC0 1.0 | `game/assets/env/tex/<name>/` |" % len(tex))
    A("| props | %d | CC0 1.0 | `game/assets/env/models/<name>/<name>.glb` |" % len(props))
    A("| skies (HDRI) | %d | CC0 1.0 | `game/assets/env/sky/<name>.hdr` |" % len(sky))
    A("| fonts | %d files, 3 families | SIL OFL 1.1 | `game/assets/fonts/` |" % len(fonts))
    A("| UI icons | %d SVG | CC BY 3.0 (two authors CC0) | `game/assets/icons/gi/<author>/` |" % len(icon_lines))
    A("\nLicence texts: CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/ · Poly Haven licence page https://polyhaven.com/license · "
      "ambientCG licence page https://docs.ambientcg.com/license/ · SIL OFL 1.1 https://openfontlicense.org · CC BY 3.0 https://creativecommons.org/licenses/by/3.0/\n")
    A("## PBR texture sets — `game/assets/env/tex/<name>/` (%d sets)\n" % len(tex))
    A("Sources: Poly Haven (%d sets) and ambientCG (%d sets), all CC0 1.0. Modifications: maps resized to 2048 px (%d sets) or 1024 px (%d sets) and re-encoded as JPEG; "
      "the OpenGL normal maps are used; ambientCG AO / roughness / metalness packed into `arm.jpg`; %d normal maps had a tilt or gamma bias removed "
      "(`normal_fix` in `tex/manifest.json`).\n" % (
          sum(1 for t in tex if t["source"] == "Poly Haven"), sum(1 for t in tex if t["source"] == "ambientCG"),
          sum(1 for t in tex if t.get("resolution") == 2048), sum(1 for t in tex if t.get("resolution") == 1024), sum(1 for t in tex if t.get("normal_fix"))))
    A("| set | source | source asset | author | source URL | licence |\n|---|---|---|---|---|---|")
    for t in tex:
        A("| %s | %s | %s | %s | %s | %s |" % (t["name"], t["source"], t["source_id"], t["author"], t["source_url"], t["licence"]))
    A("\n## Props — `game/assets/env/models/<name>/<name>.glb` (%d props)\n" % len(props))
    A("All meshes and their textures come from Poly Haven (CC0 1.0). Modifications: objects joined, decimated, re-origined and re-exported as GLB with textures reduced to "
      "at most 1024 px (2048 px for %s); where noted the foliage was removed, the scan was rescaled or re-tinted, or a snow / moss layer was baked onto the up-facing surfaces "
      "(the moss layer uses the ambientCG Moss002 texture, CC0 1.0, by Lennart Demes).\n" % ", ".join(tex2k))
    A("| prop | source asset | author | source URL | licence | changes |\n|---|---|---|---|---|---|")
    for p in props:
        ch = []
        if p.get("removed"):
            ch.append("removed " + p["removed"].split(" (")[0])
        if p.get("derived"):
            ch.append(p["derived"])
        elif p.get("rescaled"):
            ch.append("rescaled")
        A("| %s | %s | %s | %s | %s | %s |" % (p["name"], p["source_id"], p["author"], p["source_url"], p["licence"], ("; ".join(ch) or "—").replace("|", " + ")))
    A("\n## Skies — `game/assets/env/sky/` (%d HDRIs, 2K Radiance .hdr)\n" % len(sky))
    A("| file | source | source asset | author | source URL | licence |\n|---|---|---|---|---|---|")
    for s in sky:
        A("| %s | %s | %s | %s | %s | %s |" % (s["file"], s.get("source", "Poly Haven"), s["source_id"], s["author"], s["source_url"], s["licence"]))
    A("\n## Fonts — `game/assets/fonts/`\n")
    A("All three families are licensed under the **SIL Open Font License, Version 1.1**; the licence text of each family is shipped next to the fonts "
      "(`OFL_NotoSerifTC.txt`, `OFL_NotoSansTC.txt`, `OFL_Cinzel.txt`). The fonts are used unmodified.\n")
    A("| file | family / style | copyright (from the font's name table) | source URL | licence |\n|---|---|---|---|---|")
    for f in fonts:
        nm = font_names(os.path.join(FONTDIR, f["file"]))
        if f["family"].startswith("Noto Serif"):
            url, lic = "https://github.com/notofonts/noto-cjk/tree/main/Serif/SubsetOTF/TC", "SIL OFL 1.1 (`OFL_NotoSerifTC.txt`)"
        elif f["family"].startswith("Noto Sans"):
            url, lic = "https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/TC", "SIL OFL 1.1 (`OFL_NotoSansTC.txt`)"
        else:
            url, lic = "https://github.com/google/fonts/tree/main/ofl/cinzel", "SIL OFL 1.1 (`OFL_Cinzel.txt`)"
        cr = (nm.get(0, "") or "").replace("|", "/").replace("\n", " ")
        A("| %s | %s %s%s | %s | %s | %s |" % (f["file"], f["family"], f["style"], " (variable wght)" if f["variable"] else "", cr or "see licence file", url, lic))
    A("\n## UI icons — `game/assets/icons/gi/<author>/<name>.svg` (%d SVGs)\n" % len(icon_lines))
    A("Source: the game-icons.net icon set, https://game-icons.net — repository https://github.com/game-icons/icons. "
      "Licence: **Creative Commons Attribution 3.0 Unported (CC BY 3.0)**, https://creativecommons.org/licenses/by/3.0/ ; "
      "the icons by Viscious Speed and Zeromancer are CC0. The repository's `license.txt` is shipped as `game/assets/icons/gi/license.txt`.\n")
    A("**Attribution text to show in the game's credits screen** (required by CC BY 3.0, wording asked for by the icon set):\n")
    lic_lines = open(ICON_SRC_LICENCE, encoding="utf-8", errors="replace").read().splitlines()
    authors = [l.strip()[2:].strip() for l in lic_lines if l.strip().startswith("- ")]
    plain = [a.split(",")[0].replace(" - CC0", "").strip() for a in authors]
    A("> Icons made by %s. Available on https://game-icons.net — licensed under CC BY 3.0 (https://creativecommons.org/licenses/by/3.0/).\n" % ", ".join(plain))
    A("The author of an icon is the name of its folder; if the shipped game uses only some icons, the list may be cut down to the authors actually used. "
      "Modification made: the opaque black 512×512 background square was removed from each SVG (white glyph on transparent) so icons can be tinted. "
      "Untouched originals: `Y:\\assets_src\\env\\game-icons\\icons`.\n")
    A("Licence file of the repository (`license.txt`), quoted:\n")
    A("```")
    for l in lic_lines:
        A(l.rstrip())
    A("```\n")
    per = {}
    for l in icon_lines:
        parts = l.replace("\\", "/").split("/")
        if len(parts) >= 3:
            per[parts[1]] = per.get(parts[1], 0) + 1
    A("Contributing authors as listed in `license.txt` (%d), with the number of icons shipped per author folder:\n" % len(authors))
    A("| author | link / note | folder | icons |\n|---|---|---|---|")
    used = set()
    for a in authors:
        nm = a.split(",")[0].replace(" - CC0", "").strip()
        rest = a[len(a.split(",")[0]):].lstrip(", ").strip() if "," in a else ("CC0" if "CC0" in a else "")
        key = nm.lower().replace(" ", "")
        folder = next((d for d in per if d.replace("-", "") == key), None)
        if folder is None:
            folder = next((d for d in per if d.replace("-", "").startswith(key[:5])), None)
        if folder:
            used.add(folder)
        A("| %s | %s | %s | %s |" % (nm, rest or "—", "`gi/%s/`" % folder if folder else "—", per.get(folder, 0) if folder else 0))
    other = sorted(d for d in per if d not in used)
    if other:
        A("\nFolders in the repository without a line of their own in `license.txt`: %s — credit them as game-icons.net contributors (CC BY 3.0).\n" % ", ".join(
            "`gi/%s/` (%d icons)" % (d, per[d]) for d in other))
    os.makedirs(os.path.dirname(CREDITS), exist_ok=True)
    open(CREDITS, "w", encoding="utf-8").write("\n".join(L) + "\n")
    print("credits written:", CREDITS, len(L), "lines;", "icons per folder:", len(per), "authors in licence:", len(authors), "unmatched folders:", other)


main()
