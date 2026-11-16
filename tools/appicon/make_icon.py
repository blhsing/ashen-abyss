"""Builds the game's application icon from a master picture (RGBA, transparent outside the subject):
game/assets/icons/app_icon.png (256 px, window/taskbar icon) and game/assets/icons/AshenAbyss.ico
(16-256 px, for the executable and the Start-menu shortcut). Small sizes get a slight sharpen.

    python make_icon.py <master.png> <app_icon.png> <AshenAbyss.ico> [<small_master.png>]

small_master (optional): a tighter master used for the 16, 24 and 32 px frames, where the full picture would be
too busy to read (today: the boss's face without the crown, from compose_face_icon.py)."""
import sys
from PIL import Image, ImageFilter
def square(path):
    src = Image.open(path).convert("RGBA")
    bbox = src.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    crest = src.crop(bbox)
    side = max(crest.size)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(crest, ((side - crest.width) // 2, (side - crest.height) // 2))
    pad = int(side * 0.03)
    c = Image.new("RGBA", (side + 2 * pad, side + 2 * pad), (0, 0, 0, 0))
    c.paste(sq, (pad, pad))
    return c


canvas = square(sys.argv[1])
small = square(sys.argv[4]) if len(sys.argv) > 4 else canvas
big = canvas.resize((256, 256), Image.LANCZOS)
big.save(sys.argv[2])
sizes = [16, 24, 32, 48, 64, 128, 256]
frames = []
for s in sizes:
    im = (small if s <= 32 else canvas).resize((s, s), Image.LANCZOS)
    if s <= 48: im = im.filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=1))
    frames.append(im)
frames[-1].save(sys.argv[3], format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
print("ok", big.size, len(frames), "sizes")
