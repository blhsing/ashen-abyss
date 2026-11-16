def patch(path, pairs):
    s = open(path, encoding="utf-8").read()
    for a, b in pairs:
        assert s.count(a) == 1, (path, s.count(a), a[:90])
        s = s.replace(a, b)
    open(path, "w", encoding="utf-8", newline="\n").write(s)

patch(r"Y:\tools\uiart\kit.py", [
    ("              patina=None, patina_amt=0.0, bump=1.0, scan_px=420.0, metal=1.0):",
     "              patina=None, patina_amt=0.0, bump=1.0, scan_px=420.0, metal=1.0, spec=None):"),
    ("    nb = g.bump(height, nrm, dist_px=1.0, strength=0.8)\n    g.principled(col, g.clamp(rgh), met, nb)",
     "    nb = g.bump(height, nrm, dist_px=1.0, strength=0.8)\n    if spec is None: g.principled(col, g.clamp(rgh), met, nb)\n    else: g.principled(col, g.clamp(rgh), met, nb, **{'Specular IOR Level': spec})"),
    ("def mat_field(name, color=(0.02, 0.019, 0.02), rough=0.8, scan_set='slate_floor', scan_px=640.0, bump=0.6):",
     "def mat_field(name, color=(0.02, 0.019, 0.02), rough=0.8, scan_set='slate_floor', scan_px=640.0, bump=0.6, spec=0.12):"),
    ("    g.principled(col, rough, 0.0, g.bump(sd, None, 1.0, bump), **{'Specular IOR Level': 0.12})",
     "    g.principled(col, rough, 0.0, g.bump(sd, None, 1.0, bump), **{'Specular IOR Level': spec})"),
])

patch(r"Y:\tools\uiart\styles.py", [
    # plates that carry text: mostly dielectric oxide, little mirror → darker, quieter faces
    ("M['face'] = k.mat_metal('A_face', metal=0.8, base=(0.040, 0.037, 0.038),", "M['face'] = k.mat_metal('A_face', metal=0.4, spec=0.25, base=(0.040, 0.037, 0.038),"),
    ("M['face_hi'] = k.mat_metal('A_face_hi', metal=0.8, base=(0.070, 0.056, 0.046),", "M['face_hi'] = k.mat_metal('A_face_hi', metal=0.45, spec=0.3, base=(0.075, 0.060, 0.048),"),
    ("M['face_dn'] = k.mat_metal('A_face_dn', metal=0.8, base=(0.022, 0.021, 0.023),", "M['face_dn'] = k.mat_metal('A_face_dn', metal=0.35, spec=0.2, base=(0.022, 0.021, 0.023),"),
    ("M['face_off'] = k.mat_metal('A_face_off', metal=0.7, base=(0.024, 0.023, 0.024),", "M['face_off'] = k.mat_metal('A_face_off', metal=0.3, spec=0.15, base=(0.024, 0.023, 0.024),"),
    ("M['face_on'] = k.mat_metal('A_face_on', metal=0.8, base=(0.105, 0.042, 0.015),", "M['face_on'] = k.mat_metal('A_face_on', metal=0.5, spec=0.3, base=(0.115, 0.046, 0.016),"),
    ("M['floor'] = k.mat_field('A_floor', color=(0.013, 0.012, 0.013))", "M['floor'] = k.mat_field('A_floor', color=(0.0038, 0.0035, 0.0038), rough=0.92, spec=0.035)"),
])

patch(r"Y:\tools\uiart\pieces.py", [
    ("@variants([('menu_plate', False), ('menu_plate_hover', True)], 'nine', ml=92, mt=40, mr=92, mb=40, px=64, py=16, pad=16, blend=8, center=True, shadow=(7, 2, 4, 0.55))",
     "@variants([('menu_plate', False), ('menu_plate_hover', True)], 'h3', ml=92, mr=92, px=64, h=144, pad=16, blend=8, shadow=(7, 2, 4, 0.55))"),
    ("backfill=(0.016, 0.015, 0.02, 0.5), fill_inset=3)", "backfill=(0.016, 0.015, 0.02, 0.44), fill_inset=3)"),
    ("# ═════════════════════════════════ slots ═════════════════════════════════", '''@variants([('row', 'normal'), ('row_hover', 'hover'), ('row_on', 'on')], 'nine', ml=28, mt=16, mr=28, mb=16, px=32, py=16, pad=4, blend=4, center=True)
def row(k, S, m, state):
    # list row: a low plate; hover brightens its hairline, the chosen row is ember-lit with a stone at its head
    x0, y0, x1, y1 = box(m)
    out = k.chamfer_rect(x0, y0, x1, y1, 4)
    face = {'normal': 'face_dn', 'hover': 'face', 'on': 'face_on'}[state]
    k.solid(out, [(0, 0), (0, 2.2), (1.4, 3.6), (3.0, 3.6), (4.0, 2.4)], face, cap=True, name="row")
    hair(k, out, 4.6, 2.4, {'normal': 'trim_off', 'hover': 'trim', 'on': 'trim_hi'}[state], w=1.8, h=1.0)
    k.catcher()


# ═════════════════════════════════ slots ═════════════════════════════════'''),
])
print("ok")
