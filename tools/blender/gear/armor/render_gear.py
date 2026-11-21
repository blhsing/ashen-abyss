"""render_gear.py -- contact sheets of assembled heroes, built from the exported files exactly as the game does it:
base body + armour pieces on one skeleton, body regions hidden per the piece's `hides`.

  render_gear.py <class> <sheet name> <outfit> [<outfit> ...] [--poses rest,idle,atk,run,back] [--cell 560x640]
  outfit:  base | t1..t4 (full set) | helm=t1,chest=t3,gloves=t2,pants=t4,boots=t1 (any subset, any mix)
           optionally prefixed "label:"
Writes Y:\\shots\\gear\\armor\\<class>_<sheet name>.jpg (+ the single frames in _frames\\).
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
import gearfin as gf

ATK = {"emberblade": ("Sword_Regular_A", 0.55), "astromancer": ("Spell_Simple_Shoot", 0.45), "hunter": ("Pistol_Shoot", 0.4),
       "soulbinder": ("Spell_Simple_Shoot", 0.45), "wildsoul": ("Sword_Regular_A", 0.55), "forgebody": ("Punch_Cross", 0.45),
       "lampknight": ("Sword_Regular_A", 0.55), "pactbinder": ("Spell_Simple_Shoot", 0.45)}
IDLE = {"emberblade": "Sword_Idle", "astromancer": "Spell_Simple_Idle_Loop", "hunter": "Pistol_Idle_Loop",
        "soulbinder": "Spell_Simple_Idle_Loop", "wildsoul": "Sword_Idle", "forgebody": "Idle_Loop",
        "lampknight": "Idle_Shield_Loop", "pactbinder": "Spell_Simple_Idle_Loop"}
# pose name -> (action or None, t, camera preset)
POSES = {
    "rest": (None, 0, "rest"), "restback": (None, 0, "restback"), "idle": ("IDLE", 0.5, "front"), "back": ("IDLE", 0.5, "back"),
    "atk": ("ATK", None, "front"), "atk2": ("ATK", 0.3, "side"), "run": ("Jog_Fwd_Loop", 0.3, "side"), "run2": ("Jog_Fwd_Loop", 0.8, "front"),
    "sprint": ("Sprint_Loop", 0.3, "side"), "roll": ("Roll", 0.45, "front"), "close": ("IDLE", 0.5, "close"), "game": ("IDLE", 0.5, "game"),
    "sword": ("Sword_Regular_A", 0.55, "front"), "sword2": ("Sword_Regular_A", 0.3, "back"), "cast": ("Spell_Simple_Shoot", 0.45, "front"),
    "punch": ("Punch_Cross", 0.45, "front"), "block": ("Sword_Block", 0.5, "front"), "restclose": (None, 0, "restclose"),
    "idleside": ("Idle_Loop", 0.5, "side"), "crouch": ("Crouch_Idle_Loop", 0.5, "front"), "legs": ("Jog_Fwd_Loop", 0.3, "legs"),
    "top": ("ATK", None, "top"),
}


def load_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH' and o.parent is arm]
    for o in objs:
        if o is not arm and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    for pb in arm.pose.bones:
        pb.custom_shape = None
    for o in meshes:                      # the game's character shader culls back faces: show the same here
        for m in o.data.materials:
            if m is not None:
                m.use_backface_culling = True
    return arm, meshes


def manifest():
    p = os.path.join(r"Y:\game\assets\gear", "gear_manifest_armor.json")
    if not os.path.isfile(p):
        return {}
    with open(p, "r", encoding="utf8") as f:
        d = json.load(f)
    ent = d.get("entries", d) if isinstance(d, dict) else d
    return {e["id"]: e for e in ent if isinstance(e, dict) and "id" in e}


def hides_of(pid, path, mani):
    if pid in mani and mani[pid].get("hides") is not None:
        return list(mani[pid]["hides"])
    js, _ = gf.glb_read(path)
    for sc in js.get("scenes", []):
        v = (sc.get("extras") or {}).get("hide_body_regions")
        if v is not None:
            return [s for s in str(v).replace(";", ",").split(",") if s]
    return []


def parse_outfit(s):
    label = None
    if ":" in s:
        label, s = s.split(":", 1)
    if s == "base":
        return label or "base", {}
    if s in ("t1", "t2", "t3", "t4"):
        return label or s, {sl: s for sl in SLOTS}
    d = {}
    for kv in s.split(","):
        k, v = kv.split("=")
        d[k] = v
    return label or s, d


class Hero:
    def __init__(self, cls, outfit, mani):
        self.cls = cls
        self.arm, self.body = load_glb(os.path.join(OUT_CHARS, "hero_%s_base.glb" % cls))
        self.gear = []
        self.missing = []
        hidden = set()
        for slot, look in outfit.items():
            pid = "arm_%s_%s_%s" % (cls, slot, look)
            path = os.path.join(OUT_ARMOR, pid + ".glb")
            if not os.path.isfile(path):
                self.missing.append(pid)
                continue
            a2, ms = load_glb(path)
            for o in ms:
                rebind(o, self.arm)
                self.gear.append(o)
            bpy.data.objects.remove(a2, do_unlink=True)
            for h in hides_of(pid, path, mani):
                hidden.add(h[5:] if h.startswith("body_") else h)
        for o in self.body:
            n = o.name.split(".")[0]
            n = n[5:] if n.startswith("body_") else n
            if any(n.startswith(h) for h in hidden):
                o.hide_render = True
                o.hide_viewport = True
        self.hidden = sorted(hidden)

    def meshes(self):
        return [o for o in self.body + self.gear if not o.hide_render]

    def remove(self):
        for o in self.body + self.gear:
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.objects.remove(self.arm, do_unlink=True)


def frame_cam(cam, hero, preset):
    mn, mx = world_bbox(hero.meshes())
    c = (mn + mx) / 2
    size = float(np.linalg.norm(mx - mn))
    yaw = 28
    if preset == "game":
        aim_camera(cam, (c[0], c[1], 0.85), 35, 52, 20.0, lens=165.0)
    elif preset == "rest":
        aim_camera(cam, (0, 0, 0.93), 22, 8, 4.5, lens=62)
    elif preset == "restback":
        aim_camera(cam, (0, 0, 0.93), 202, 8, 4.5, lens=62)
        yaw = 202
    elif preset == "restclose":
        aim_camera(cam, (0, 0, 1.36), 18, 4, 2.6, lens=62)
        yaw = 18
    elif preset == "close":
        h = hero.arm.pose.bones["Head"].head
        aim_camera(cam, (h[0], h[1], h[2] - 0.16), 22, 6, 1.9, lens=62)
        yaw = 22
    elif preset == "legs":
        aim_camera(cam, (c[0], c[1], 0.5), 50, 14, 2.6, lens=62)
        yaw = 50
    elif preset == "top":
        aim_camera(cam, c, 20, 48, max(3.4, size * 2.0), lens=62)
        yaw = 20
    else:
        yaw = {"front": 28, "side": 62, "back": 207}.get(preset, 28)
        aim_camera(cam, c, yaw, 12, max(3.2, size * 1.9), lens=62)
    aim_key(yaw)


def montage(rows, out, cell):
    cols = max(len(r) for r in rows)
    can = np.zeros((len(rows) * cell[1], cols * cell[0], 3), dtype=np.float32)
    for r, row in enumerate(rows):
        for c, p in enumerate(row):
            a = mats.read_img(p, (cell[0], cell[1]))[..., :3]
            y0 = (len(rows) - 1 - r) * cell[1]
            can[y0:y0 + cell[1], c * cell[0]:(c + 1) * cell[0]] = a
    mats.save_rgb(can, out, 90)


def main():
    a = args()
    cls, sheet = a[0], a[1]
    poses = "rest,idle,atk,run,back"
    cell = (560, 640)
    outfits = []
    i = 2
    while i < len(a):
        if a[i] == "--poses":
            poses = a[i + 1]
            i += 2
        elif a[i] == "--cell":
            w, h = a[i + 1].split("x")
            cell = (int(w), int(h))
            i += 2
        else:
            outfits.append(parse_outfit(a[i]))
            i += 1
    poses = poses.split(",")
    reset_scene()
    acts = load_actions()
    setup_render(cell[0], cell[1], samples=16)
    setup_stage()
    cam = make_camera()
    mani = manifest()
    fdir = os.path.join(SHOTS_GEAR, "_frames")
    os.makedirs(fdir, exist_ok=True)
    rows = []
    for label, outfit in outfits:
        h = Hero(cls, outfit, mani)
        log("outfit %s: gear meshes %d, hidden regions %s%s" % (label, len(h.gear), h.hidden, ("  MISSING " + str(h.missing)) if h.missing else ""))
        row = []
        for pn in poses:
            act, t, preset = POSES[pn]
            if act == "IDLE":
                act = IDLE[cls]
            elif act == "ATK":
                act, t0 = ATK[cls]
                t = t0 if t is None else t
            if act is not None and act not in acts:
                log("no action", act, "- using Idle_Loop")
                act = "Idle_Loop"
            set_pose(h.arm, acts[act] if act else None, t or 0)
            frame_cam(cam, h, preset)
            p = os.path.join(fdir, "%s_%s_%s_%s.png" % (cls, sheet, label.replace("=", "").replace(",", "_"), pn))
            render_to(p)
            row.append(p)
        rows.append(row)
        h.remove()
    out = os.path.join(SHOTS_GEAR, "%s_%s.jpg" % (cls, sheet))
    montage(rows, out, cell)
    log("sheet", out)


main()
