"""Preview sheet of the application icon: every size the .ico carries (256/48/32/24/16) at 1:1 on a dark and a light
background, and the small sizes again enlarged x4 (nearest neighbour) to judge their pixels.

    python icon_sheet.py <AshenAbyss.ico> <out.png>
"""
import sys
from PIL import Image, ImageDraw

ico = Image.open(sys.argv[1])
frames = {}
for s in ico.info.get("sizes", []):
    ico.size = s
    frames[s[0]] = ico.convert("RGBA").copy()
SIZES = [256, 48, 32, 24, 16]
BGS = [((32, 32, 36), (200, 200, 200)), ((238, 238, 240), (40, 40, 40))]
W, ROW = 1280, 300
sheet = Image.new("RGB", (W, ROW * 4), (0, 0, 0))
d = ImageDraw.Draw(sheet)
for bi, (bg, fg) in enumerate(BGS):
    for half in range(2):
        y0 = (bi * 2 + half) * ROW
        d.rectangle((0, y0, W, y0 + ROW), fill=bg)
        x = 24
        for s in SIZES:
            im = frames.get(s)
            if im is None:
                continue
            if half == 1:
                if s > 48:
                    continue
                im = im.resize((s * 4, s * 4), Image.NEAREST)
            sheet.paste(im, (x, y0 + (ROW - im.height) // 2), im)
            d.text((x, y0 + ROW - 22), "%d px%s" % (s, " x4" if half else ""), fill=fg)
            x += im.width + 40
sheet.save(sys.argv[2])
print("ok", sys.argv[2], sorted(frames))
