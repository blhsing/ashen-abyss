r"""Prints the piece table (markdown) from pieces.py:  python.exe -I Y:\tools\uiart\doc.py > table.md"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pieces as PC
print("| piece | kind | texture x2 (px) | logical box (design px) | margins L,T,R,B inside the box (design px) | pad (design px) | tile period (design px) | notes |")
print("|---|---|---|---|---|---|---|---|")
for n in PC.ORDER:
    m = PC.PIECES[n]
    k = m['kind']
    pad = m['pad']
    if k == 'nine':
        w, h = m['ml'] + m['px'] + m['mr'], m['mt'] + m['py'] + m['mb']
        mg = "%g, %g, %g, %g" % ((m['ml'] - pad) / 2, (m['mt'] - pad) / 2, (m['mr'] - pad) / 2, (m['mb'] - pad) / 2)
        per = "%g x %g" % (m['px'] / 2, m['py'] / 2)
        box = "any (min %g x %g)" % ((m['ml'] + m['mr'] - 2 * pad) / 2, (m['mt'] + m['mb'] - 2 * pad) / 2)
    elif k == 'h3':
        w, h = m['ml'] + m['px'] + m['mr'], m['h']
        mg = "%g, 0, %g, 0" % ((m['ml'] - pad) / 2, (m['mr'] - pad) / 2)
        per = "%g" % (m['px'] / 2)
        box = "any width x %g" % ((h - 2 * pad) / 2)
    elif k == 'v3':
        w, h = m['w'], m['mt'] + m['py'] + m['mb']
        mg = "0, %g, 0, %g" % ((m['mt'] - pad) / 2, (m['mb'] - pad) / 2)
        per = "%g" % (m['py'] / 2)
        box = "%g x any height" % ((w - 2 * pad) / 2)
    else:
        w, h = m['w'], m['h']
        mg = "-"; per = "-"
        box = "%g x %g" % ((w - 2 * pad) / 2, (h - 2 * pad) / 2)
    notes = []
    if m.get('center'): notes.append("centre drawn (tiled)")
    if m.get('anchor'): notes.append("anchor %g,%g" % (m['anchor'][0] / 2, m['anchor'][1] / 2))
    if m.get('mip'): notes.append("mipmaps")
    if m.get('glow'): notes.append("glow")
    if m.get('backfill'): notes.append("translucent fill")
    if 'fill' in n or n in ('tooltip_band', 'slot_rarity', 'slot_rarity_hi', 'skill_slot_off', 'boxline'): notes.append("neutral: tint with Modulate")
    print("| `%s` | %s | %d x %d | %s | %s | %g | %s | %s |" % (n, k, w, h, box, mg, pad / 2, per, "; ".join(notes)))
