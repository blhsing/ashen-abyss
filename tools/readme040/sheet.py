# usage: sheet.py <out.jpg> <title> <png...>  : contact sheet, 4 columns, 400x225 thumbs with index labels
import sys
from PIL import Image, ImageDraw, ImageFont
out, title, files = sys.argv[1], sys.argv[2], sys.argv[3:]
W, H, C = 480, 270, 4
rows = (len(files) + C - 1) // C
sheet = Image.new("RGB", (C * W, rows * (H + 26) + 34), (18, 16, 14))
d = ImageDraw.Draw(sheet)
try: f = ImageFont.truetype("C:/Windows/Fonts/msjh.ttc", 20)
except Exception: f = ImageFont.load_default()
d.text((8, 6), title, fill=(230, 200, 140), font=f)
import os
for i, p in enumerate(files):
    im = Image.open(p).convert("RGB").resize((W, H), Image.LANCZOS)
    x, y = (i % C) * W, 34 + (i // C) * (H + 26)
    sheet.paste(im, (x, y))
    d.text((x + 6, y + H + 2), os.path.basename(p), fill=(220, 220, 220), font=f)
sheet.save(out, quality=85)
