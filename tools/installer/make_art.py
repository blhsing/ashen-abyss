"""Builds the installer's wizard images from the app icon's art (Osaros, the final boss, in the icon's "stare").

    uv run --no-project --with pillow python Y:\\tools\\installer\\make_art.py [render.png] [icon_master.png] [out_dir]

render.png      : the icon's 1536 px stare render (default Y:\\tools\\appicon\\renders\\s3.png; re-render a bigger one
                  with Y:\\tools\\appicon\\blender_stare.ps1 if ever needed)
icon_master.png : the icon master (default Y:\\tools\\appicon\\icon_master.png), used for the small header image
out_dir         : default Y:\\tools\\installer\\art

Writes wizard_<w>x<h>.bmp (the tall left-hand image of the Welcome / Finished pages, one per DPI scale, graded like
compose_face_icon.py: lifted mid-tones, contrast, bloom on the ember eyes and mouth, vignette into black) and
small_<n>.bmp (the header icon on the inner pages, on the dark style's header colour).
"""
import sys, os
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

RENDER = sys.argv[1] if len(sys.argv) > 1 else r"Y:\tools\appicon\renders\s3.png"
MASTER = sys.argv[2] if len(sys.argv) > 2 else r"Y:\tools\appicon\icon_master.png"
OUT = sys.argv[3] if len(sys.argv) > 3 else r"Y:\tools\installer\art"
os.makedirs(OUT, exist_ok=True)

# Inno Setup modern wizard image sizes per DPI scale (100 % .. 250 %)
WIZ = [(164, 314), (205, 393), (246, 471), (287, 550), (328, 628), (369, 707), (410, 785)]
SMALL = [55, 69, 83, 96, 110, 124, 138]
HEADER_BG = (32, 32, 32)          # header band colour of Inno's built-in dark style

src = Image.open(RENDER).convert("RGB")
W, H = src.size
# tall crop (aspect of the wizard image) centred on the face, full height: crown spikes to the ember breach
aspect = WIZ[-1][0] / WIZ[-1][1]
cw = int(H * aspect)
cx = int(W * 0.50)                 # face centre in the stare render
face = src.crop((cx - cw // 2, 0, cx - cw // 2 + cw, H))
N = face.size

face = face.point(lambda v: int(255 * (v / 255) ** 0.85))
face = ImageEnhance.Contrast(face).enhance(1.22)
face = ImageEnhance.Color(face).enhance(1.12)
r, g, b = face.split()
hot = ImageChops.subtract(r, b).point(lambda v: 255 if v > 120 else int(v * v / 120 / 2))
lum = face.convert("L").point(lambda v: 0 if v < 150 else (v - 150) * 255 // 105)
mask = ImageChops.lighter(hot, lum)
glow = Image.composite(face, Image.new("RGB", N), mask)
bloom = ImageChops.add(glow.filter(ImageFilter.GaussianBlur(12)), glow.filter(ImageFilter.GaussianBlur(34)))
face = ImageChops.add(face, ImageEnhance.Brightness(bloom).enhance(0.85))

# vignette: an upright ellipse round the head, falling into near-black at the sides and the bottom
vig = Image.new("L", N, 0)
d = ImageDraw.Draw(vig)
for i in range(60):
    t = i / 59.0
    rx = int(N[0] * (0.95 - 0.45 * t)); ry = int(N[1] * (0.70 - 0.32 * t)); cy = int(N[1] * 0.45)
    d.ellipse((N[0] // 2 - rx, cy - ry, N[0] // 2 + rx, cy + ry), fill=int(255 * min(1.0, 0.35 + t)))
vig = vig.filter(ImageFilter.GaussianBlur(N[0] // 12))
face = Image.composite(face, Image.new("RGB", N, (8, 6, 7)), vig)
face = ImageChops.lighter(face, Image.composite(face, Image.new("RGB", N), mask.point(lambda v: v // 2)))
# a thin ember line on the right edge where the image meets the page
edge = Image.new("RGB", N, (0, 0, 0)); ed = ImageDraw.Draw(edge)
for y in range(N[1]):
    t = y / (N[1] - 1)
    ed.line((N[0] - 4, y, N[0], y), fill=(int(70 + 90 * t), int(60 + 2 * t), int(70 - 50 * t)))
em = Image.new("L", N, 0); ImageDraw.Draw(em).rectangle((N[0] - 4, 0, N[0], N[1]), fill=200)
face = Image.composite(edge, face, em)
face.save(os.path.join(OUT, "wizard_master.png"))
for w, h in WIZ:
    face.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, f"wizard_{w}x{h}.bmp"))

ico = Image.open(MASTER).convert("RGBA")
bbox = ico.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
ico = ico.crop(bbox)
for n in SMALL:
    bg = Image.new("RGBA", (n, n), HEADER_BG + (255,))
    s = ico.resize((n, n), Image.LANCZOS)
    if n <= 69:
        s = s.filter(ImageFilter.UnsharpMask(1, 60, 2))
    bg.alpha_composite(s)
    bg.convert("RGB").save(os.path.join(OUT, f"small_{n}.bmp"))
print("ok", OUT, "crop", N)
