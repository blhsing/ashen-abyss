# Side-by-side crop of two screenshots (before | after), scaled up, for the texture A/B.
#   uv run --with pillow python -I sbs.py <before.png> <after.png> <out.png> <x0> <y0> <x1> <y1> [scale]
import sys
from PIL import Image
a, b, out = sys.argv[1:4]; x0, y0, x1, y1 = map(int, sys.argv[4:8]); k = float(sys.argv[8]) if len(sys.argv) > 8 else 2
A = Image.open(a).crop((x0, y0, x1, y1)); B = Image.open(b).crop((x0, y0, x1, y1))
w, h = int(A.width * k), int(A.height * k)
im = Image.new("RGB", (w * 2 + 8, h), (255, 255, 255))
im.paste(A.resize((w, h), Image.LANCZOS), (0, 0)); im.paste(B.resize((w, h), Image.LANCZOS), (w + 8, 0))
im.save(out)
