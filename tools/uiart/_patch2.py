p = r"Y:\tools\uiart\slice.py"
s = open(p, encoding="utf-8").read()
old = s[s.index("def process(m, src_dir):"):s.index("def main():")]
new = '''def bloom(a, radius=7.0, strength=0.6):
    """Faint warm halo round the ember stones and ember-lit lines (orange-hot pixels only; gold does not count)."""
    al = a[..., 3]
    rgb = np.where(al[..., None] > 1e-4, a[..., :3] / np.maximum(al[..., None], 1e-4), 0.0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hot = np.clip((r - 0.62) / 0.3, 0, 1) * np.clip((0.66 - g / (r + 1e-4)) / 0.2, 0, 1) * np.clip((r - b - 0.4) / 0.3, 0, 1) * al
    if hot.max() < 0.08: return a
    add = blur(hot, radius) * strength + blur(hot, radius * 0.35) * strength * 0.5
    col = np.array((1.0, 0.42, 0.10), np.float32)
    out = a.copy()
    out[..., :3] = a[..., :3] + col[None, None, :] * add[..., None] * (1.0 - 0.6 * a[..., :3])
    out[..., 3] = np.clip(np.maximum(al, add * 0.85), 0, 1)
    return out


def process(m, src_dir):
    a = load(os.path.join(src_dir, m['name'] + ".png"))
    assert a.shape[0] == m['h'] and a.shape[1] == m['w'], (m['name'], a.shape, m['w'], m['h'])
    info = dict(kind=m['kind'], pad=m['pad'])
    if m.get('backfill'):
        # translucent veil inside the logical box (HUD plaques): under the render
        r, g, b, al = m['backfill']
        i = m['pad'] + m.get('fill_inset', 0)
        fill = np.zeros_like(a)
        fill[i:a.shape[0] - i, i:a.shape[1] - i] = (r * al, g * al, b * al, al)
        a = under(a, fill)
    if m['kind'] == 'nine':
        a = seam_axis(a, m['ml'], m['px'], m['blend'], 1)
        a = seam_axis(a, m['mt'], m['py'], m['blend'], 0)
        info.update(m=[m['ml'], m['mt'], m['mr'], m['mb']], center=bool(m.get('center')))
    elif m['kind'] == 'h3':
        a = seam_axis(a, m['ml'], m['px'], m['blend'], 1)
        info.update(m=[m['ml'], 0, m['mr'], 0])
    elif m['kind'] == 'v3':
        a = seam_axis(a, m['mt'], m['py'], m['blend'], 0)
        info.update(kind='nine', m=[0, m['mt'], 0, m['mb']], center=True)
    if m.get('glow'): a = glow(a, m['glow'])
    if m.get('bloom', True): a = bloom(a)
    if m.get('shadow'): a = shadow(a, m['shadow'])
    for key in ('anchor', 'band', 'mip'):
        if key in m: info[key] = m[key]
    info['w'] = a.shape[1]; info['h'] = a.shape[0]
    return a, info


'''
s = s.replace(old, new)
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("slice patched")
