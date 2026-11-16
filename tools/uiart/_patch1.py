import io
p = r"Y:\tools\uiart\styles.py"
s = open(p, encoding="utf-8").read()
a = s.index("class StyleA(Style):")
b = s.index("# ═════════════════════════════════ B: ash stone")
new = open(r"Y:\tools\uiart\_styleA.txt", encoding="utf-8").read()
s = s[:a] + new + "\n\n" + s[b:]
open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\tools\uiart\render.py"
s = open(p, encoding="utf-8").read()
if "S.finish(kit)" not in s:
    assert "        S.mats(kit)\n" in s
    s = s.replace("        S.mats(kit)\n", "        S.mats(kit)\n        S.finish(kit)\n")
    open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\tools\uiart\kit.py"
s = open(p, encoding="utf-8").read()
old = """    g.put(p.inputs['Emission Color'], hot)
    g.put(p.inputs['Emission Strength'], g.mul(g.add(v, 0.10), strength))"""
new = """    g.put(p.inputs['Emission Color'], hot)
    # facets that lean away from the eye glow less: the table reads as the hot heart of the stone
    lw = g.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    face = g.ramp(lw.outputs['Facing'], 0.02, 0.55, 1.0, 0.22)
    g.put(p.inputs['Emission Strength'], g.mul(g.mul(g.add(v, 0.10), strength), face))"""
if old in s:
    s = s.replace(old, new)
    open(p, "w", encoding="utf-8", newline="\n").write(s)

p = r"Y:\tools\uiart\mock.py"
s = open(p, encoding="utf-8").read()
s = s.replace("nine('button', x, y, w, h)", "nine('btn', x, y, w, h)")
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
