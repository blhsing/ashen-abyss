"""Turns a close-up render of Osaros's face (ov2_render.py, black background, 1024 px) into the icon master:
the face and crown on a dark round plate with an iron rim, eyes and breach given extra bloom so they still read at 16 px.

    python compose_face_icon.py <render.png> <out_master.png> [cx cy crop [bloom]]

cx, cy, crop: centre and side of the square cut from the render, in render pixels (default 512 470 820);
bloom: size and strength of the extra glow on eyes / breach / mouth (default 1; the 16-32 px master uses 2).
Writes a 1024 px RGBA master (transparent outside the plate) for make_icon.py.
"""
import sys
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

src = Image.open(sys.argv[1]).convert("RGB")
out = sys.argv[2]
cx = int(sys.argv[3]) if len(sys.argv) > 3 else 512
cy = int(sys.argv[4]) if len(sys.argv) > 4 else 470
crop = int(sys.argv[5]) if len(sys.argv) > 5 else 820
BLOOM = float(sys.argv[6]) if len(sys.argv) > 6 else 1.0
N = 1024

face = src.crop((cx - crop // 2, cy - crop // 2, cx + crop // 2, cy + crop // 2)).resize((N, N), Image.LANCZOS)

# grade: a little more contrast and a deeper black, so the face separates from the plate
face = face.point(lambda v: int(255 * (v / 255) ** 0.85))   # lift the mid-tones of the skin a little
face = ImageEnhance.Contrast(face).enhance(1.25)
face = ImageEnhance.Color(face).enhance(1.12)

# bloom on the glowing parts (bright and orange): blurred twice and added, so the eyes survive the downscale
r, g, b = face.split()
hot = ImageChops.subtract(r, b).point(lambda v: 255 if v > 120 else int(v * v / 120 / 2))
lum = face.convert("L").point(lambda v: 0 if v < 150 else (v - 150) * 255 // 105)
mask = ImageChops.lighter(hot, lum)
glow_src = Image.composite(face, Image.new("RGB", face.size), mask)
bloom = ImageChops.add(glow_src.filter(ImageFilter.GaussianBlur(10 * BLOOM)), glow_src.filter(ImageFilter.GaussianBlur(28 * BLOOM)))
face = ImageChops.add(face, ImageEnhance.Brightness(bloom).enhance(0.9 * BLOOM ** 0.5))

# vignette: light falls away toward the plate's edge and the shoulders at the bottom
vig = Image.new("L", (N, N), 0)
d = ImageDraw.Draw(vig)
for i in range(60):
    t = i / 59.0
    rad = int(N * (0.66 - 0.30 * t))
    d.ellipse((N // 2 - rad, int(N * 0.40) - rad, N // 2 + rad, int(N * 0.40) + rad), fill=int(255 * min(1.0, 0.45 + t)))
vig = vig.filter(ImageFilter.GaussianBlur(40))
plate_col = Image.new("RGB", (N, N), (10, 8, 9))
face = Image.composite(face, plate_col, vig)
# glowing parts stay bright even where the vignette darkens
face = ImageChops.lighter(face, Image.composite(face, Image.new("RGB", face.size), mask))

# the plate: a disc with a forged iron rim (dark, a warm lower edge, a cool upper edge)
S = 4 * N  # supersampled masks for clean edges
def disc(rad_frac):
    m = Image.new("L", (S, S), 0)
    rr = int(S * rad_frac)
    ImageDraw.Draw(m).ellipse((S // 2 - rr, S // 2 - rr, S // 2 + rr, S // 2 + rr), fill=255)
    return m.resize((N, N), Image.LANCZOS)

outer = disc(0.495)
inner = disc(0.455)
ring = ImageChops.subtract(outer, inner)
# rim colour: a vertical gradient from cool iron at the top to ember at the bottom
grad = Image.new("RGB", (1, N))
for y in range(N):
    t = y / (N - 1)
    top = (70, 78, 92); bot = (150, 62, 22)
    grad.putpixel((0, y), tuple(int(top[k] * (1 - t) + bot[k] * t) for k in range(3)))
grad = grad.resize((N, N))
# a bevel: lighter outer edge, darker inner edge of the ring
bevel = ImageChops.subtract(outer, disc(0.485))
rim = Image.composite(ImageEnhance.Brightness(grad).enhance(1.5), ImageEnhance.Brightness(grad).enhance(0.55), bevel)

img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
img.paste(face, (0, 0), inner)
img.paste(rim, (0, 0), ring)
# a thin black line round the whole thing so it holds on a light taskbar
edge = ImageChops.subtract(disc(0.4995), outer)
img.paste((0, 0, 0), (0, 0), edge)
alpha = disc(0.4995)
img.putalpha(alpha)
img.save(out)
print("ok", out)
