r"""Contact sheet of the cursor set: every kind on a dark game ground, a lit game ground, the interface and a bright
ground, at 1x for each size, plus a 2x and a 4x pixel zoom of the 32 px cursor (nearest neighbour: what a screen shows).
The cyan square on the bright ground's 4x zoom marks the hotspot pixel.

   python.exe -I Y:\tools\cursor\sheet.py [--src Y:\game\assets\ui\cursor] [--out Y:\shots\cursor\sheet.png] [--kinds a,b]
"""
import sys, os, json
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw, ImageFont

argv = sys.argv[1:]
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

src = str(opt('--src', r"Y:\game\assets\ui\cursor"))
out = str(opt('--out', r"Y:\shots\cursor\sheet.png"))
man = json.load(open(os.path.join(src, "cursors.json"), encoding="utf-8"))
which = opt('--kinds', None)
kinds = which.split(',') if isinstance(which, str) else list(man["kinds"].keys())
sizes = man["sizes"]
LABEL = {'default': '預設 default', 'attack': '攻擊 attack', 'talk': '交談 talk', 'pickup': '拾取 pickup', 'interact': '互動 interact',
         'forbid': '不可 forbid', 'ui': '介面 ui', 'ui_hot': '介面(可按) ui_hot', 'caret': '文字 caret', 'busy': '載入 busy'}
SHOTS = "Y:\\shots\\readme\\"
# grounds: a list of (screenshot, crop origin) stacked top to bottom, or a flat colour
GROUNDS = [
    ("dark scene", [(SHOTS + "01-wastes-emberblade.jpg", (40, 150)), (SHOTS + "09-dungeon-pactbinder.jpg", (40, 60))]),
    ("lit scene", [(SHOTS + "02-frost-astromancer.jpg", (540, 150)), (SHOTS + "05-sands-forgebody.jpg", (480, 60))]),
    ("interface", [(SHOTS + "13-inventory-tooltip.jpg", (610, 110)), (SHOTS + "14-skill-tree.jpg", (300, 60))]),
    ("bright", (226, 216, 196)),
]
# cells per ground: (size, zoom)
CELLS = [(32, 1), (32, 2), (32, 4)] + [(s, 1) for s in sizes if s != 32]
PAD = 10
cellw = [s * z + PAD * 2 for s, z in CELLS]
groundw = sum(cellw) + 8
rowh = max(s * z for s, z in CELLS) + PAD * 2
labw, top = 190, 58
W = labw + groundw * len(GROUNDS)
H = top + rowh * len(kinds)
sheet = Image.new("RGB", (W, H), (24, 22, 23))
dr = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype(r"Y:\game\assets\fonts\NotoSansTC-Medium.otf", 17)
    small = ImageFont.truetype(r"Y:\game\assets\fonts\NotoSansTC-Medium.otf", 13)
except Exception:
    font = small = None

for gi, (gname, gsrc) in enumerate(GROUNDS):
    gx = labw + gi * groundw
    need = (groundw - 8, rowh * len(kinds))
    if isinstance(gsrc, list):
        tile = Image.new("RGB", need)
        y, i = 0, 0
        while y < need[1]:                       # one screenshot is shorter than the sheet: the next one continues it
            path, org = gsrc[i % len(gsrc)]
            im = Image.open(path).convert("RGB")
            part = im.crop((org[0], org[1], min(im.size[0], org[0] + need[0]), im.size[1] - 40))
            tile.paste(part, (0, y))
            y += part.size[1]; i += 1
    else:
        tile = Image.new("RGB", need, gsrc)
    sheet.paste(tile, (gx, top))
    dr.text((gx + 6, 6), gname, fill=(232, 222, 200), font=font)
    x = gx
    for (s, z), cw in zip(CELLS, cellw):
        dr.text((x + PAD, 32), "%d px%s" % (s, "" if z == 1 else "  x%d" % z), fill=(170, 160, 145), font=small)
        x += cw

for ki, kind in enumerate(kinds):
    y0 = top + ki * rowh
    dr.text((10, y0 + rowh // 2 - 22), LABEL.get(kind, kind), fill=(232, 222, 200), font=font)
    h32 = man["kinds"][kind]["hot"]["32"]
    dr.text((10, y0 + rowh // 2 + 2), "hotspot@32: %d,%d" % (h32[0], h32[1]), fill=(150, 142, 130), font=small)
    for gi in range(len(GROUNDS)):
        x = labw + gi * groundw
        for (s, z), cw in zip(CELLS, cellw):
            im = Image.open(os.path.join(src, "%s_%d.png" % (kind, s))).convert("RGBA")
            if z != 1: im = im.resize((s * z, s * z), Image.NEAREST)
            cy = y0 + (rowh - s * z) // 2
            sheet.paste(im, (x + PAD, cy), im)
            if gi == len(GROUNDS) - 1 and z == 4:
                hx, hy = h32
                px, py = x + PAD + hx * z, cy + hy * z
                dr.rectangle((px, py, px + z - 1, py + z - 1), outline=(0, 200, 255))
            x += cw
os.makedirs(os.path.dirname(out), exist_ok=True)
sheet.save(out)
print("sheet:", out, sheet.size)
