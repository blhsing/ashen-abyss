r"""Style mock-up: the inventory with a tooltip open, drawn with one style's kit over a real game screenshot.

   python.exe -I Y:\tools\uiart\mock.py --style A --kit Y:\assets_src\ui_gen\explore\A --out Y:\shots\uiart\style_A.png

Composes at 2x the 1080p design size (the x2 textures 1:1), then scales to 1600x900 like the game's test window.
"""
import sys, os, json, glob
sys.path.insert(0, os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "pylibs"))
from PIL import Image, ImageDraw, ImageFont, ImageEnhance

argv = sys.argv[1:]
def opt(name, default=None):
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith('--') else True
    return default

style = str(opt('--style', 'A'))
kit = str(opt('--kit', r"Y:\assets_src\ui_gen\explore" + "\\" + style))
out = str(opt('--out', r"Y:\shots\uiart\style_%s.png" % style))
back = str(opt('--back', r"Y:\shots\ui\final\10_hud_town.png"))
FIELD = {'A': ('slate', 'soot', 'well'), 'B': ('parchment', 'parchment_dark', 'well'), 'C': ('leather', 'leather_dark', 'well')}[style]
D = 2
MAN = json.load(open(os.path.join(kit, "manifest.json"), encoding='utf-8'))
TEX = {}
def tex(name):
    if name not in TEX: TEX[name] = Image.open(os.path.join(kit, "x2", name + ".png")).convert("RGBA")
    return TEX[name]

FONTS = {}
FD = r"Y:\game\assets\fonts"
def font(kind, size):
    key = (kind, size)
    if key not in FONTS:
        f = {'title': "NotoSerifTC-Bold.otf", 'body': "NotoSerifTC-Medium.otf", 'ui': "NotoSansTC-Medium.otf", 'uib': "NotoSansTC-Bold.otf"}[kind]
        FONTS[key] = ImageFont.truetype(os.path.join(FD, f), size * D)
    return FONTS[key]

GOLD = (237, 186, 97); TEXT = (219, 209, 189); DIM = (148, 140, 128); GOOD = (115, 230, 102); BAD = (242, 77, 64)
RAR = {0: (209, 209, 204), 1: (107, 158, 255), 2: (255, 219, 77), 3: (255, 133, 31), 4: (219, 184, 115)}

canvas = None
draw = None

def text(x, y, s, kind='body', size=24, color=TEXT, anchor='la', shadow=True):
    f = font(kind, size)
    if shadow: draw.text((x * D + D, y * D + D), s, font=f, fill=(0, 0, 0, 150), anchor=anchor)
    draw.text((x * D, y * D), s, font=f, fill=color + (255,), anchor=anchor)
    return draw.textlength(s, font=f) / D

def paste(im, x, y):
    """im at pixel position (x, y) of the 2x canvas, alpha composited, clipped."""
    x, y = int(round(x)), int(round(y))
    w, h = im.size
    cx0, cy0 = max(0, x), max(0, y)
    cx1, cy1 = min(canvas.size[0], x + w), min(canvas.size[1], y + h)
    if cx1 <= cx0 or cy1 <= cy0: return
    canvas.alpha_composite(im.crop((cx0 - x, cy0 - y, cx1 - x, cy1 - y)), (cx0, cy0))

def tile_fill(name, x0, y0, x1, y1, alpha=1.0):
    t = tex("field_" + name)
    if alpha < 1.0:
        t = t.copy(); t.putalpha(int(255 * alpha))
    tw, th = t.size
    X0, Y0, X1, Y1 = int(x0 * D), int(y0 * D), int(x1 * D), int(y1 * D)
    lay = Image.new("RGBA", (X1 - X0, Y1 - Y0))
    for yy in range(0, Y1 - Y0, th):
        for xx in range(0, X1 - X0, tw):
            lay.paste(t, (xx, yy))
    paste(lay, X0, Y0)

def span(im, length, horiz):
    """Tile `im` along one axis to exactly `length` px, fitting a whole number of tiles (like TILE_FIT)."""
    w, h = im.size
    per = w if horiz else h
    n = max(1, int(round(length / per)))
    out = Image.new("RGBA", (length, h) if horiz else (w, length))
    for i in range(n):
        a = int(round(i * length / n)); b = int(round((i + 1) * length / n))
        t = im.resize((b - a, h) if horiz else (w, b - a), Image.LANCZOS)
        out.paste(t, (a, 0) if horiz else (0, a))
    return out

def nine(name, x, y, w, h, field=None, inset=5, field_alpha=1.0):
    info = MAN['pieces'][name]
    t = tex(name)
    pad = info['pad']
    ml, mt, mr, mb = info['m']
    W, H = t.size
    X0, Y0 = x * D - pad, y * D - pad
    X1, Y1 = (x + w) * D + pad, (y + h) * D + pad
    if field:
        tile_fill(field, x + inset, y + inset, x + w - inset, y + h - inset, field_alpha)
    iw, ih = (X1 - X0) - ml - mr, (Y1 - Y0) - mt - mb
    paste(t.crop((0, 0, ml, mt)), X0, Y0)
    paste(t.crop((W - mr, 0, W, mt)), X1 - mr, Y0)
    paste(t.crop((0, H - mb, ml, H)), X0, Y1 - mb)
    paste(t.crop((W - mr, H - mb, W, H)), X1 - mr, Y1 - mb)
    if iw > 0:
        paste(span(t.crop((ml, 0, W - mr, mt)), iw, True), X0 + ml, Y0)
        paste(span(t.crop((ml, H - mb, W - mr, H)), iw, True), X0 + ml, Y1 - mb)
    if ih > 0:
        paste(span(t.crop((0, mt, ml, H - mb)), ih, False), X0, Y0 + mt)
        paste(span(t.crop((W - mr, mt, W, H - mb)), ih, False), X1 - mr, Y0 + mt)
    if info.get('center') and iw > 0 and ih > 0:
        c = span(span(t.crop((ml, mt, W - mr, H - mb)), iw, True), ih, False)
        paste(c, X0 + ml, Y0 + mt)

def h3(name, x, y, w, tint=None):
    info = MAN['pieces'][name]
    t = tex(name)
    if tint:
        r, g, b, a = t.split()
        t = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255), b.point(lambda v: v * tint[2] // 255), a))
    pad = info['pad']; ml, _, mr, _ = info['m']
    W, H = t.size
    X0 = x * D - pad; X1 = (x + w) * D + pad; Y0 = y * D - pad
    paste(t.crop((0, 0, ml, H)), X0, Y0)
    paste(t.crop((W - mr, 0, W, H)), X1 - mr, Y0)
    paste(span(t.crop((ml, 0, W - mr, H)), X1 - X0 - ml - mr, True), X0 + ml, Y0)

def sprite(name, x, y, size=None, tint=None, anchor=False):
    info = MAN['pieces'][name]
    t = tex(name)
    if tint:
        r, g, b, a = t.split()
        t = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255), b.point(lambda v: v * tint[2] // 255), a))
    pad = info['pad']
    if anchor:
        ax, ay = info['anchor']
        paste(t, x * D - ax, y * D - ay); return
    if size:
        k = size * D / (t.size[0] - 2 * pad)
        t = t.resize((int(round(t.size[0] * k)), int(round(t.size[1] * k))), Image.LANCZOS)
        pad = pad * k
    paste(t, x * D - pad, y * D - pad)

ICONS = sorted(glob.glob(r"Y:\game\assets\icons\r\item\*.png"))
def icon(i, x, y, size):
    if not ICONS: return
    im = Image.open(ICONS[i % len(ICONS)]).convert("RGBA").resize((int(size * D), int(size * D)), Image.LANCZOS)
    paste(im, x * D, y * D)

def slot(x, y, size, rarity=None, ic=None):
    sprite('slot', x, y, size)
    if rarity is not None and rarity > 0: sprite('slot_rarity', x, y, size, tint=RAR[rarity])
    if ic is not None:
        p = size * 0.12
        icon(ic, x + p, y + p, size - 2 * p)

def button(x, y, w, h, label, size=22, color=TEXT):
    nine('btn', x, y, w, h)
    text(x + w / 2, y + h / 2 - 1, label, 'body', size, color, anchor='mm')

def rule(x0, x1, y, a=110):
    draw.line([(x0 * D, y * D), (x1 * D, y * D)], fill=(150, 112, 56, a), width=D)

def main():
    global canvas, draw
    bg = Image.open(back).convert("RGB").resize((1920 * D, 1080 * D), Image.LANCZOS)
    bg = ImageEnhance.Brightness(bg).enhance(0.62)
    canvas = bg.convert("RGBA")
    draw = ImageDraw.Draw(canvas)

    # ── inventory window ──
    wx, wy, ww, wh = 724, 96, 1168, 892
    nine('window', wx, wy, ww, wh, field=FIELD[0])
    sprite('crest', wx + ww / 2, wy, anchor=True)
    draw_ = ImageDraw.Draw(canvas)
    globals()['draw'] = draw_
    cx, cy = wx + 34, wy + 30
    text(cx, cy, "行囊", 'title', 34, GOLD)
    button(wx + ww - 34 - 78, cy + 2, 78, 46, "關閉")
    text(wx + ww - 34 - 96, cy + 12, "等級 20 燼刃衛", 'body', 22, DIM, anchor='ra')
    text(wx + ww - 34 - 262, cy + 12, "試煉者", 'body', 22, DIM, anchor='ra')
    rule(cx, wx + ww - 34, cy + 62)
    # left: sheet
    y = cy + 82
    text(cx, y, "試煉者", 'title', 28, TEXT); y += 44
    text(cx, y, "等級 20　燼刃衛　重劍式", 'body', 22, DIM); y += 40
    bx = cx
    for i, t in enumerate(("總覽", "攻勢", "守禦", "巧用")):
        button(bx, y, 86, 46, t, 22, GOLD if i == 0 else DIM); bx += 92
    y += 68
    def section(title, rows):
        nonlocal y
        text(cx, y, title, 'title', 24, GOLD); y += 36
        for a, b, c in rows:
            text(cx, y, a, 'body', 22, DIM)
            text(cx + 382, y, b, 'body', 22, c, anchor='ra')
            y += 36
        y += 10
    section("概況", [("等級", "20", TEXT), ("經驗", "9,000 / 16,600", TEXT), ("生命上限", "798", TEXT), ("護甲", "1,007", TEXT),
                   ("每秒傷害（估算）", "130", TEXT), ("武器傷害", "71–106", TEXT), ("每秒攻擊", "1.00", TEXT), ("怒焰上限", "100", TEXT)])
    section("屬性", [("力量（主屬性）", "106", GOLD), ("智力", "49", TEXT), ("敏捷", "45", TEXT), ("意志", "27", TEXT)])
    # right: equipment
    ex = wx + 530
    names_l = ("頭盔", "胸甲", "手套", "護腿"); names_r = ("護符", "戒指", "戒指", "靴子")
    for i in range(4):
        sy = cy + 84 + i * 82
        slot(ex, sy, 68, 2, i + 1); text(ex + 78, sy + 20, names_l[i], 'body', 22, DIM)
        slot(wx + ww - 34 - 136, sy, 68, 2 if i != 2 else 3, i + 3); text(wx + ww - 34 - 146, sy + 20, names_r[i], 'body', 22, DIM, anchor='ra')
    slot(ex + 238, cy + 84 + 3 * 82 + 10, 68, 3, 5); text(ex + 316, cy + 84 + 3 * 82 + 30, "武器", 'body', 22, DIM)
    # purse
    py = cy + 84 + 4 * 82 + 16
    px = ex + 84
    for label, val, col in (("金幣", "250,137", (255, 212, 89)), ("碎鐵", "300", TEXT), ("幽塵", "300", (170, 140, 255)), ("燼核", "300", (255, 128, 38))):
        px += text(px, py, "◆ ", 'ui', 20, col)
        px += text(px, py, label + " ", 'body', 22, DIM)
        px += text(px, py + 1, val, 'body', 22, col) + 22
    # bag: recessed well with a 10 x 4 grid
    gx, gy, cs, gap = ex - 66, py + 46, 64, 4
    rar = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 2, 3, 0, 3, 1, 1, 1, 1, 1, 2, 4, 1]
    for r in range(4):
        for c in range(10):
            i = r * 10 + c
            slot(gx + c * (cs + gap), gy + r * (cs + gap), cs, rar[i] if i < len(rar) else None, i if i < len(rar) else None)
    by = gy + 4 * (cs + gap) + 12
    button(gx, by, 86, 46, "整理"); button(gx + 96, by, 128, 46, "拆解廢品")
    text(wx + ww - 34, by + 12, "行囊 24 / 40", 'body', 22, DIM, anchor='ra')
    text(gx, by + 62, "左鍵：裝備　右鍵：卸下裝備／選取寶石　Shift+左鍵：標記廢品", 'body', 20, DIM)

    # ── tooltip (legendary) ──
    tx, ty, tw, th = 742, 262, 548, 716
    nine('tooltip', tx, ty, tw, th, field=FIELD[1], inset=4, field_alpha=0.985)
    band = MAN['pieces']['tooltip'].get('band')
    tip = MAN['pieces']['tooltip']
    h3('tooltip_band', tx + 22, ty + (band[0] - tip['pad']) / D - 1, tw - 44, tint=RAR[3])
    x = tx + 26; y = ty + 40
    text(x, y, "破綻之鋼紋巨劍", 'title', 28, RAR[3]); y += 44
    w_ = text(x, y, "傳奇巨劍", 'title', 22, RAR[3]); text(x + w_ + 18, y, "物品強度 200", 'body', 22, DIM); y += 66
    w_ = text(x, y - 8, "91.7", 'body', 34, TEXT); w2 = text(x + w_ + 10, y + 4, "每秒傷害", 'body', 22, TEXT); text(x + w_ + w2 + 18, y + 4, "(+7.6)", 'body', 22, GOOD); y += 44
    text(x, y, "傷害 77–116　每秒攻擊 0.95", 'body', 22, DIM); y += 60
    for a, b in (("+15.5% 對菁英傷害", "(+15.5%)"), ("+20.2% 暗影傷害", "(+20.2%)"), ("+21.1% 冰霜傷害", "(+21.1%)"), ("+25.3% 爆擊傷害", "(+25.3%)")):
        text(x, y + 2, "◆", 'ui', 20, TEXT); w_ = text(x + 30, y, a, 'body', 24, TEXT); text(x + 30 + w_ + 8, y, b, 'body', 24, GOOD); y += 37
    for a in ("失去 +22 智力", "失去 +4.8% 幸運一擊機率", "失去 +23.5% 持續傷害"):
        text(x + 30, y, a, 'body', 24, BAD); y += 37
    y += 22
    text(x, y + 2, "✹", 'ui', 20, RAR[3]); text(x + 30, y, "爆擊使敵人陷入易傷，持續 3 秒。", 'body', 24, RAR[3]); y += 58
    text(x, y, "○ 空插槽", 'body', 22, DIM); y += 58
    w_ = text(x, y, "需要等級 18", 'body', 22, DIM); text(x + w_ + 24, y, "售價 1260", 'body', 22, (255, 212, 89)); y += 36
    text(x, y, "左鍵：裝備　Shift+左鍵：標記廢品", 'body', 22, DIM)

    crop = opt('--crop')
    if crop:
        for i, c in enumerate(str(crop).split(';')):
            x, y, w, h = [int(v) * D for v in c.split(',')]
            canvas.convert("RGB").crop((x, y, x + w, y + h)).save(out.replace(".png", "_crop%d.png" % i))
    final = canvas.convert("RGB").resize((1600, 900), Image.LANCZOS)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    final.save(out)
    print("mock ->", out)

main()
