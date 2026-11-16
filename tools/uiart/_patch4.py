p = r"Y:\tools\uiart\styles.py"
s = open(p, encoding="utf-8").read()
def rep(a, b):
    global s
    assert a in s, a[:80]
    s = s.replace(a, b)
rep("edge=(0.6, 0.56, 0.5), edge_amt=0.7, dark=(0.03, 0.026, 0.024), soot=0.6, hammer=1.2)", "edge=(0.6, 0.56, 0.5), edge_amt=0.7, dark=(0.03, 0.026, 0.024), soot=0.6, hammer=0.45)")
rep("edge=(0.75, 0.66, 0.52), edge_amt=0.8, dark=(0.045, 0.034, 0.026), soot=0.5, hammer=1.2)", "edge=(0.75, 0.66, 0.52), edge_amt=0.8, dark=(0.045, 0.034, 0.026), soot=0.5, hammer=0.45)")
rep("edge=(0.45, 0.42, 0.38), edge_amt=0.6, dark=(0.014, 0.012, 0.012), soot=0.7, hammer=1.2)", "edge=(0.45, 0.42, 0.38), edge_amt=0.6, dark=(0.014, 0.012, 0.012), soot=0.7, hammer=0.45)")
rep("edge=(0.22, 0.21, 0.20), edge_amt=0.3, dark=(0.016, 0.015, 0.015), soot=0.7, hammer=1.0)", "edge=(0.22, 0.21, 0.20), edge_amt=0.3, dark=(0.016, 0.015, 0.015), soot=0.7, hammer=0.4)")
rep("M['face_on'] = k.mat_metal('A_face_on', metal=0.8, base=(0.19, 0.078, 0.026), rough=(0.4, 0.62), edge=(1.0, 0.70, 0.36), edge_amt=0.7, dark=(0.085, 0.032, 0.012), soot=0.35, hammer=1.2)",
    "M['face_on'] = k.mat_metal('A_face_on', metal=0.8, base=(0.105, 0.042, 0.015), rough=(0.45, 0.66), edge=(1.0, 0.70, 0.36), edge_amt=0.7, dark=(0.055, 0.02, 0.008), soot=0.35, hammer=0.45)")
rep("if 'ember_lit' not in M: M['ember_lit'] = k.mat_glow(self.key + '_ember_lit', (1.0, 0.42, 0.10), 3.0)", "if 'ember_lit' not in M: M['ember_lit'] = k.mat_glow(self.key + '_ember_lit', (1.0, 0.36, 0.07), 1.5)")
rep("if 'trim_hi' not in M: M['trim_hi'] = k.mat_glow(self.key + '_trim_hi', (1.0, 0.70, 0.30), 1.5)", "if 'trim_hi' not in M: M['trim_hi'] = k.mat_glow(self.key + '_trim_hi', (1.0, 0.66, 0.26), 1.15)")
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
