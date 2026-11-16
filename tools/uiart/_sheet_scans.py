import sys, os, glob
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw
roots = [r"Y:\assets_src\montex", r"Y:\assets_src\chartex"]
items = []
for r in roots:
    for d in sorted(os.listdir(r)):
        p = os.path.join(r, d)
        if not os.path.isdir(p) or d.startswith('_'): continue
        c = glob.glob(os.path.join(p, "*_Color.jpg")) + glob.glob(os.path.join(p, "diff_1k.jpg")) + glob.glob(os.path.join(p, "*diff*.jpg"))
        if c: items.append((os.path.basename(r) + "/" + d, c[0]))
for d in ["slate_floor","iron_plate","aged_gold","corroded_bronze","tarnished_bronze","rusty_iron","obsidian","dark_marble","canvas","burlap","dark_panel_wood","basalt"]:
    p = os.path.join(r"Y:\game\assets\env\tex", d, "albedo.jpg")
    if os.path.exists(p): items.append(("env/" + d, p))
T = 200; cols = 8; rows = (len(items) + cols - 1) // cols
sheet = Image.new("RGB", (cols * T, rows * (T + 16)), (20, 20, 20))
dr = ImageDraw.Draw(sheet)
for i, (n, f) in enumerate(items):
    im = Image.open(f).convert("RGB").resize((T, T))
    x, y = (i % cols) * T, (i // cols) * (T + 16)
    sheet.paste(im, (x, y)); dr.text((x + 2, y + T + 2), n, fill=(255, 255, 255))
sheet.save(r"Y:\shots\uiart\_work\scans.jpg", quality=85)
print(len(items))
