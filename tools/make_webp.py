"""Encode numbered PNG frames into an animated WebP.
   python.exe -I make_webp.py <frames dir> <out.webp> <fps> [quality]
(run with Blender's bundled python; Pillow lives in %LOCALAPPDATA%\\AshenAbyssTools\\pylibs)"""
import sys, os, glob
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image

src, out, fps = sys.argv[1], sys.argv[2], float(sys.argv[3])
quality = int(sys.argv[4]) if len(sys.argv) > 4 else 82
files = sorted(glob.glob(os.path.join(src, "f_*.png")))
if not files:
    raise SystemExit("no frames in " + src)
frames = [Image.open(f).convert("RGB") for f in files]
os.makedirs(os.path.dirname(out), exist_ok=True)
frames[0].save(out, format="WEBP", save_all=True, append_images=frames[1:], duration=int(round(1000 / fps)),
               loop=0, quality=quality, method=4)
print("wrote", out, len(frames), "frames", frames[0].size, os.path.getsize(out) // 1024, "KB")
