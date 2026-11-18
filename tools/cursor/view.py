r"""Cuts the contact sheet into 1:1 pieces for review (the full sheet is wider than a viewer shows at 1:1).
   python.exe -I Y:\tools\cursor\view.py  ->  Y:\shots\cursor\_v_<ground>_<a|b>.png"""
import sys, os
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image
im = Image.open(r"Y:\shots\cursor\sheet.png")
lab = 190
W = (im.size[0] - lab) // 4
H = im.size[1]
mid = 58 + (H - 58) // 2
for gi, g in enumerate(("dark", "lit", "ui", "bright")):
    im.crop((lab + gi * W, 0, lab + (gi + 1) * W, mid)).save(r"Y:\shots\cursor\_v_%s_a.png" % g)
    im.crop((lab + gi * W, mid, lab + (gi + 1) * W, H)).save(r"Y:\shots\cursor\_v_%s_b.png" % g)
print("view: pieces written", W, H)
