r"""Quick look at a render: composite over a panel-like backdrop, crop, zoom.
   python.exe -I view.py <png> <out.png> [x,y,w,h] [zoom] [bg r,g,b]"""
import sys, os
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
im = Image.open(src).convert("RGBA")
if len(sys.argv) > 3 and sys.argv[3] != '-':
    x, y, w, h = [int(v) for v in sys.argv[3].split(',')]
    im = im.crop((x, y, x + w, y + h))
zoom = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
bg = tuple(int(v) for v in sys.argv[5].split(',')) if len(sys.argv) > 5 else (38, 35, 36)
back = Image.new("RGBA", im.size, bg + (255,))
# left third: game-world-like mid tone, to judge the outer shadow
for yy in range(0, im.size[1], 16):
    for xx in range(0, im.size[0] // 4, 16):
        if (xx // 16 + yy // 16) % 2 == 0:
            back.paste((70, 62, 55, 255), (xx, yy, xx + 16, yy + 16))
back.alpha_composite(im)
if zoom != 1.0:
    back = back.resize((int(back.size[0] * zoom), int(back.size[1] * zoom)), Image.LANCZOS if zoom < 1 else Image.NEAREST)
back.convert("RGB").save(out)
print(back.size)
