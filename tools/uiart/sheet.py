r"""Contact sheet of a folder of pieces (raw renders or sliced textures).
   python.exe -I sheet.py <folder> <out.png> [cell] [cols] [name-filter-substring,...]"""
import sys, os, glob
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw, ImageFont
src, out = sys.argv[1], sys.argv[2]
cell = int(sys.argv[3]) if len(sys.argv) > 3 else 260
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 6
flt = sys.argv[5].split(',') if len(sys.argv) > 5 else None
files = sorted(glob.glob(os.path.join(src, "*.png")))
if flt: files = [f for f in files if any(s in os.path.basename(f) for s in flt)]
rows = (len(files) + cols - 1) // cols
lab = 22
sheet = Image.new("RGB", (cols * cell, rows * (cell + lab)), (34, 31, 32))
dr = ImageDraw.Draw(sheet)
try: font = ImageFont.truetype(r"Y:\game\assets\fonts\NotoSansTC-Medium.otf", 15)
except Exception: font = None
for i, f in enumerate(files):
    im = Image.open(f).convert("RGBA")
    k = min((cell - 8) / im.size[0], (cell - 8) / im.size[1], 2.0)
    im = im.resize((max(1, int(im.size[0] * k)), max(1, int(im.size[1] * k))), Image.LANCZOS if k < 1 else Image.BICUBIC)
    x, y = (i % cols) * cell, (i // cols) * (cell + lab)
    bg = Image.new("RGBA", (cell, cell), (34, 31, 32, 255))
    # lighter strip on the left to judge shadows
    bg.paste((74, 66, 58, 255), (0, 0, cell // 5, cell))
    bg.alpha_composite(im, ((cell - im.size[0]) // 2, (cell - im.size[1]) // 2))
    sheet.paste(bg.convert("RGB"), (x, y))
    dr.text((x + 4, y + cell + 2), os.path.basename(f)[:-4], fill=(230, 220, 200), font=font)
sheet.save(out)
print(len(files), sheet.size)
