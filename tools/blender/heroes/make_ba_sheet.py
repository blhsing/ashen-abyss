r"""BEFORE / AFTER contact sheets of the realism pass.
blender -b --factory-startup --python make_ba_sheet.py
Reads Y:\shots\chars\_cmp\<id>_<shot>_before.png / _after.png and writes three pages of 1200 px width
(3 columns of 400 px tiles, 46 px labels; each column = one close-up, BEFORE above AFTER):
    Y:\shots\chars\heroes_realism_before_after.jpg      emberblade + wildsoul
    Y:\shots\chars\heroes_realism_before_after_2.jpg    astromancer + forgebody
    Y:\shots\chars\heroes_realism_before_after_3.jpg    villager + game-camera views"""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from rigio import *
import mats

CMP = os.path.join(SHOTS, "_cmp")
TILE = (400, 457)
HEAD_H = 74
TITLE_H = 66
TEXT_PX = 46
PAGES = [
    ("heroes_realism_before_after.jpg", [
        ("EMBERBLADE", [("hero_emberblade", "plate", "plate armour"), ("hero_emberblade", "fur", "fur mantle"),
                        ("hero_emberblade", "tabard", "tabard, mail")]),
        ("WILDSOUL", [("hero_wildsoul", "fur", "wolf pelt"), ("hero_wildsoul", "bark", "bark, fur"),
                      ("hero_wildsoul", "hide", "hide, kilt")]),
    ]),
    ("heroes_realism_before_after_2.jpg", [
        ("ASTROMANCER", [("hero_astromancer", "face", "face"), ("hero_astromancer", "hair", "hair"),
                         ("hero_astromancer", "robes", "robes")]),
        ("FORGEBODY", [("hero_forgebody", "skin", "bare skin"), ("hero_forgebody", "scars", "ember scars"),
                       ("hero_forgebody", "wraps", "wraps")]),
    ]),
    ("heroes_realism_before_after_3.jpg", [
        ("VILLAGER", [("npc_villager_f", "skin", "skin"), ("npc_villager_f", "clothes", "plain clothes"),
                      ("npc_villager_f", "skirt", "apron, skirt")]),
        ("GAME CAMERA", [("hero_emberblade", "game", "emberblade"), ("hero_wildsoul", "game", "wildsoul"),
                         ("hero_forgebody", "game", "forgebody")]),
    ]),
]

# 5x7 fallback font (used only if Blender's font engine is not available in background mode)
_F = {
    'A': "01110 10001 10001 11111 10001 10001 10001", 'B': "11110 10001 10001 11110 10001 10001 11110",
    'C': "01110 10001 10000 10000 10000 10001 01110", 'D': "11110 10001 10001 10001 10001 10001 11110",
    'E': "11111 10000 10000 11110 10000 10000 11111", 'F': "11111 10000 10000 11110 10000 10000 10000",
    'G': "01110 10001 10000 10111 10001 10001 01111", 'H': "10001 10001 10001 11111 10001 10001 10001",
    'I': "01110 00100 00100 00100 00100 00100 01110", 'J': "00111 00010 00010 00010 00010 10010 01100",
    'K': "10001 10010 10100 11000 10100 10010 10001", 'L': "10000 10000 10000 10000 10000 10000 11111",
    'M': "10001 11011 10101 10101 10001 10001 10001", 'N': "10001 11001 10101 10011 10001 10001 10001",
    'O': "01110 10001 10001 10001 10001 10001 01110", 'P': "11110 10001 10001 11110 10000 10000 10000",
    'Q': "01110 10001 10001 10001 10101 10010 01101", 'R': "11110 10001 10001 11110 10100 10010 10001",
    'S': "01111 10000 10000 01110 00001 00001 11110", 'T': "11111 00100 00100 00100 00100 00100 00100",
    'U': "10001 10001 10001 10001 10001 10001 01110", 'V': "10001 10001 10001 10001 10001 01010 00100",
    'W': "10001 10001 10001 10101 10101 11011 10001", 'X': "10001 10001 01010 00100 01010 10001 10001",
    'Y': "10001 10001 01010 00100 00100 00100 00100", 'Z': "11111 00001 00010 00100 01000 10000 11111",
    '-': "00000 00000 00000 11111 00000 00000 00000", ',': "00000 00000 00000 00000 00110 00100 01000",
    ' ': "00000 00000 00000 00000 00000 00000 00000",
}


def _text_bitmap(text, px):
    k = max(1, int(round(px / 9.0)))
    cols = []
    for ch in text.upper():
        g = np.array([[int(c) for c in row] for row in _F.get(ch, _F[' ']).split()], dtype=np.float32)
        cols.append(np.kron(g, np.ones((k, k), dtype=np.float32)))
        cols.append(np.zeros((7 * k, k), dtype=np.float32))
    a = np.concatenate(cols, axis=1)
    return a[::-1]                                   # bottom-up rows


def _text_blf(text, px):
    """alpha mask (h, w) bottom-up, drawn by Blender's font engine; None if not available"""
    try:
        import blf, imbuf
        fid = 0
        blf.size(fid, px)
        tw, th = blf.dimensions(fid, text)
        w, h = int(tw) + 8, int(px * 1.4)
        if tw < 4:
            return None
        ib = imbuf.new((w, h))
        with blf.bind_imbuf(fid, ib):
            blf.color(fid, 1.0, 1.0, 1.0, 1.0)
            blf.position(fid, 3, int(px * 0.34), 0)
            blf.draw_buffer(fid, text)
        tmp = os.path.join(CMP, "_label_tmp.png")
        imbuf.write(ib, filepath=tmp)
        a = mats.read_img(tmp)
        try:
            os.remove(tmp)
        except OSError:
            pass
        al = np.maximum(a[..., 3] if a[..., :3].max() < 0.01 else a[..., :3].max(-1) * a[..., 3], 0.0)
        if al.max() < 0.2:
            al = a[..., :3].max(-1)
        return al if al.max() > 0.2 else None
    except Exception as e:
        log("blf text not available:", str(e)[:100])
        return None


_MODE = {}


def text_mask(text, px, max_w):
    """alpha mask of `text`, at most max_w wide (font shrinks if needed)"""
    while True:
        a = None
        if _MODE.get("blf", True):
            a = _text_blf(text, px)
            if a is None:
                _MODE["blf"] = False
        if a is None:
            a = _text_bitmap(text, px)
        if a.shape[1] <= max_w or px <= 20:
            return a[:, :max_w]
        px = int(px * 0.92)


def put_text(img, text, x, y, px, fg, max_w, box=None):
    """draw text into img (bottom-up rows) with its lower-left corner at (x, y); box = backdrop colour"""
    a = text_mask(text, px, max_w)
    h, w = a.shape
    H, W = img.shape[:2]
    if box is not None:
        y0, y1 = max(0, y - 8), min(H, y + h + 8)
        x0, x1 = max(0, x - 12), min(W, x + w + 12)
        img[y0:y1, x0:x1] = img[y0:y1, x0:x1] * 0.22 + np.array(box, dtype=np.float32) * 0.78
    h = min(h, H - y)
    w = min(w, W - x)
    al = a[:h, :w, None]
    img[y:y + h, x:x + w] = img[y:y + h, x:x + w] * (1 - al) + np.array(fg, dtype=np.float32) * al


def cell(cid, shot, title):
    """one column: title strip, BEFORE tile, AFTER tile (returned bottom-up)"""
    pb = os.path.join(CMP, "%s_%s_before.png" % (cid, shot))
    pa = os.path.join(CMP, "%s_%s_after.png" % (cid, shot))
    if not (os.path.isfile(pb) and os.path.isfile(pa)):
        log("missing pair", cid, shot)
        b = np.zeros((TILE[1], TILE[0], 3), dtype=np.float32)
        a = b.copy()
    else:
        b = mats.read_img(pb, TILE)[..., :3].copy()
        a = mats.read_img(pa, TILE)[..., :3].copy()
    tag_y = TILE[1] - int(TEXT_PX * 1.4) - 12
    put_text(b, "BEFORE", 14, tag_y, TEXT_PX, (1.0, 0.66, 0.50), TILE[0] - 28, box=(0.03, 0.03, 0.035))
    put_text(a, "AFTER", 14, tag_y, TEXT_PX, (0.60, 1.0, 0.68), TILE[0] - 28, box=(0.03, 0.03, 0.035))
    b[:2] = 0.05                                               # thin line between the two tiles
    tl = np.empty((TITLE_H, TILE[0], 3), dtype=np.float32)
    tl[:] = (0.11, 0.11, 0.125)
    put_text(tl, title, 14, int((TITLE_H - TEXT_PX * 1.4) * 0.5), TEXT_PX, (0.92, 0.92, 0.95), TILE[0] - 28)
    return np.concatenate([a, b, tl], axis=0)


def main():
    W = TILE[0] * 3
    for fname, rows in PAGES:
        blocks = []
        for name, cells in rows:
            cs = [cell(*c_) for c_ in cells]
            for c_ in cs[:-1]:
                c_[:, -2:] = 0.05
            row = np.concatenate(cs, axis=1)
            head = np.empty((HEAD_H, W, 3), dtype=np.float32)
            head[:] = (0.05, 0.05, 0.06)
            put_text(head, name, 16, int((HEAD_H - TEXT_PX * 1.4) * 0.5), TEXT_PX, (1.0, 0.82, 0.45), W - 32)
            blocks.append(np.concatenate([row, head], axis=0))
        img = np.concatenate(blocks[::-1], axis=0)
        out = os.path.join(SHOTS, fname)
        mats.save_rgb(img, out, 91)
        log("sheet", out, "size %dx%d" % (img.shape[1], img.shape[0]), "text", "blf" if _MODE.get("blf", True) else "bitmap")


main()
