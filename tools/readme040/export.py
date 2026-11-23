# usage: export.py <map.txt>  lines "out_name.jpg|raw/path.png" -> Y:/shots/readme040/out_name.jpg (quality 88)
import sys
from PIL import Image
base = r"Y:/shots/readme040/"
for line in open(sys.argv[1], encoding="utf-8"):
    line = line.strip()
    if not line or line.startswith("#"): continue
    out, src = line.split("|")
    im = Image.open(base + src).convert("RGB")
    im.save(base + out, quality=88, optimize=True, progressive=True)
    print(out, im.size)
