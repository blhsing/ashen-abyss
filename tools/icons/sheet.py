"""Contact sheets of the icon masters on the game's dark UI background.

  python.exe -I Y:\\tools\\icons\\sheet.py <name> <px> <cols> <id-or-glob> [...]     e.g.  sheet.py items 64 12 item/*
Writes Y:\\shots\\icons\\sheet_<name>.png and sheet_<name>.txt (number -> icon id legend). Each cell is drawn like a
slot of the game (near-black well, thin bronze frame) at exactly <px> pixels, with its number above it.
`pilot` mode:  sheet.py pilot <id> [...]  shows every icon at 64 and at 128 px.
"""
import sys, os, glob
sys.path.insert(0, os.path.join(os.environ.get("LOCALAPPDATA", ""), "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw

SRC = r"Y:\game\assets\icons\r"
OUT = r"Y:\shots\icons"
BG = (16, 14, 15); WELL = (8, 7, 8); FRAME = (86, 70, 46); NUM = (150, 140, 120)


def expand(sels):
    ids = []
    for s in sels:
        if "*" in s:
            for p in sorted(glob.glob(os.path.join(SRC, s.replace("/", os.sep) + ".png"))):
                ids.append(os.path.relpath(p, SRC)[:-4].replace(os.sep, "/"))
        else:
            ids.append(s)
    return ids


def cell(im, d, iid, x, y, px, n):
    d.rectangle([x - 1, y - 1, x + px, y + px], fill=WELL, outline=FRAME)
    p = os.path.join(SRC, iid.replace("/", os.sep) + ".png")
    if os.path.isfile(p):
        ic = Image.open(p).convert("RGBA").resize((px, px), Image.LANCZOS)
        im.paste(ic, (x, y), ic)
    else:
        d.line([x, y, x + px, y + px], fill=(200, 40, 40)); d.line([x + px, y, x, y + px], fill=(200, 40, 40))
    d.text((x, y - 11), str(n), fill=NUM)


def sheet(name, px, cols, ids):
    os.makedirs(OUT, exist_ok=True)
    gap = 14
    rows = (len(ids) + cols - 1) // cols
    W = cols * (px + gap) + gap; H = rows * (px + gap + 8) + gap
    im = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(im)
    for i, iid in enumerate(ids):
        cell(im, d, iid, gap + (i % cols) * (px + gap), gap + 8 + (i // cols) * (px + gap + 8), px, i + 1)
    im.save(os.path.join(OUT, "sheet_%s.png" % name))
    with open(os.path.join(OUT, "sheet_%s.txt" % name), "w", encoding="utf-8") as f:
        for i, iid in enumerate(ids):
            f.write("%d\t%s\n" % (i + 1, iid))
    print("sheet_%s.png  %d icons  %dx%d" % (name, len(ids), W, H))


def pilot(ids):
    os.makedirs(OUT, exist_ok=True)
    gap = 16
    W = len(ids) * (128 + gap) + gap; H = 64 + 128 + gap * 3 + 16
    im = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(im)
    for i, iid in enumerate(ids):
        x = gap + i * (128 + gap)
        cell(im, d, iid, x + 32, gap + 8, 64, i + 1)
        cell(im, d, iid, x, gap * 2 + 64 + 16, 128, i + 1)
    im.save(os.path.join(OUT, "sheet_pilot.png"))
    print("sheet_pilot.png", W, H)


if __name__ == "__main__":
    if sys.argv[1] == "pilot":
        pilot(expand(sys.argv[2:]))
    else:
        sheet(sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), expand(sys.argv[4:]))
