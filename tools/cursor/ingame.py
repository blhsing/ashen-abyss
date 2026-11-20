r"""The cursor at its real size in the real game: a screenshot never contains the operating system's cursor, so this
pastes the cursor picture into the test's screenshots at the place the pointer was (pointer position minus hotspot).
It is a reconstruction, not a photograph of the screen: it shows size, contrast and where the hotspot sits, 1:1.

   python.exe -I Y:\tools\cursor\ingame.py <log> [<log> ...] [--out Y:\shots\cursor\ingame]

Reads the "[cursortest] SHOT <png> pointer=x,y picture=<kind>_<size> hotspot=hx,hy" lines of test logs
(CursorTest.cs, --cursor-shots=<folder>). Writes <out>\<name>.png (the whole frame with the cursor) and
<out>\..\ingame_sheet.png: one 1:1 crop per moment (the cyan tick marks the hotspot = the pixel that clicks).
"""
import sys, os, re
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw, ImageFont

argv = sys.argv[1:]
out = "Y:/shots/cursor/ingame"
if "--out" in argv:
    i = argv.index("--out"); out = argv[i + 1]; del argv[i:i + 2]
ART = "Y:/game/assets/ui/cursor/"
pat = re.compile(r"\[cursortest\] SHOT (\S+) pointer=(-?\d+),(-?\d+) picture=(\w+)_(\d+) hotspot=(\d+),(\d+)")
shots = []
for log in argv:
    with open(log, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = pat.search(line)
            if m: shots.append((m.group(1), int(m.group(2)), int(m.group(3)), m.group(4), int(m.group(5)), int(m.group(6)), int(m.group(7))))
os.makedirs(out, exist_ok=True)
CW, CH, LAB = 400, 250, 26
cells = []
for path, px, py, kind, size, hx, hy in shots:
    if not os.path.exists(path):
        print("ingame: screenshot missing", path); continue
    frame = Image.open(path).convert("RGBA")
    cur = Image.open(ART + "%s_%d.png" % (kind, size)).convert("RGBA")
    name = os.path.splitext(os.path.basename(path))[0]
    ox, oy = px - hx, py - hy
    if 0 <= ox <= frame.size[0] - size and 0 <= oy <= frame.size[1] - size: frame.alpha_composite(cur, (ox, oy))
    else: print("ingame: %s: the cursor would leave the frame at %d,%d (not pasted)" % (name, ox, oy))
    frame.convert("RGB").save(os.path.join(out, name + ".png"))
    x0 = min(max(0, px - CW // 2), frame.size[0] - CW); y0 = min(max(0, py - CH // 2), frame.size[1] - CH)
    crop = frame.crop((x0, y0, x0 + CW, y0 + CH)).convert("RGB")
    d = ImageDraw.Draw(crop)
    d.line((px - x0 - 9, py - y0, px - x0 - 4, py - y0), fill=(0, 220, 255)); d.line((px - x0, py - y0 - 9, px - x0, py - y0 - 4), fill=(0, 220, 255))
    cells.append((name, "%s %d px" % (kind, size), crop))
if not cells:
    print("ingame: no SHOT lines with screenshots found"); sys.exit(0)
try: font = ImageFont.truetype("Y:/game/assets/fonts/NotoSansTC-Medium.otf", 15)
except Exception: font = None
cols = 4
rows = (len(cells) + cols - 1) // cols
sheet = Image.new("RGB", (cols * (CW + 6) + 6, rows * (CH + LAB + 6) + 6), (20, 18, 19))
dr = ImageDraw.Draw(sheet)
for i, (name, what, crop) in enumerate(cells):
    x, y = 6 + (i % cols) * (CW + 6), 6 + (i // cols) * (CH + LAB + 6)
    sheet.paste(crop, (x, y))
    dr.text((x + 2, y + CH + 3), "%s  (%s)" % (name, what), fill=(232, 222, 200), font=font)
sheet_path = os.path.join(os.path.dirname(os.path.abspath(out)), "ingame_sheet.png")
sheet.save(sheet_path)
print("ingame: %d moments -> %s, sheet %s %s" % (len(cells), out, sheet_path, sheet.size))
