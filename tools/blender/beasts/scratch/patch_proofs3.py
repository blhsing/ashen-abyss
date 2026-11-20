"""one-off patch 3: lantern - no fake ground discs (a real light is requested through the meta), skull-like face;
eel - irregular wet mud; bshots - preview light from the meta; bmeta - keep the light entry (2026-11-20)"""
import os, ast
HERE = r"Y:\tools\blender\beasts"


def load(n):
    return open(os.path.join(HERE, n), encoding='utf8').read()


def save(n, s):
    ast.parse(s)
    p = os.path.join(HERE, n)
    open(p + '.new', 'w', encoding='utf8').write(s)
    os.replace(p + '.new', p)
    print(n, 'ok')


def rep(s, a, b):
    assert a in s, a[:90]
    return s.replace(a, b)


# ------------------------------------------------------------------ spec_f
s = load('spec_f.py')
s = rep(s, """    for c, r, zs in p.get('holes', []):
        vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
        N.set(vm.inputs[0], N.mapping(N.coord(), scale=(1, 1, 1.0 / zs)))
        N.set(vm.inputs[1], (float(c[0]), float(c[1]), float(c[2]) / zs))
        m = N.math('MULTIPLY', m, N.mapr(vm.outputs['Value'], r * 0.55, r * 1.25))""",
        """    for c, r, zs in p.get('holes', []):                    # the hollows reach 2.6 radii deep into the head (+Y)
        vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
        N.set(vm.inputs[0], N.mapping(N.coord(), scale=(1, 1.0 / 2.6, 1.0 / zs)))
        N.set(vm.inputs[1], (float(c[0]), float(c[1]) / 2.6, float(c[2]) / zs))
        m = N.math('MULTIPLY', m, N.mapr(vm.outputs['Value'], r * 0.6, r * 1.2))""")
s = rep(s, "    MOUTH = gc + V(0, -0.088, -0.085)", "    MOUTH = gc + V(0, -0.088, -0.09)\n    NOSE = gc + V(0, -0.108, -0.012)")
s = rep(s, "           holes=[(EYES[0], 0.034, 1.25), (EYES[1], 0.034, 1.25), (MOUTH, 0.036, 1.7)])",
        "           holes=[(EYES[0], 0.036, 1.15), (EYES[1], 0.036, 1.15), (MOUTH, 0.046, 1.15), (NOSE, 0.016, 1.6)])")
s = rep(s, "        for c, r3, tilt in ((EYES[0], (0.03, 0.07, 0.04), -18.0), (EYES[1], (0.03, 0.07, 0.04), 18.0), (MOUTH, (0.03, 0.07, 0.062), 0.0)):",
        "        for c, r3, tilt in ((EYES[0], (0.034, 0.07, 0.038), -12.0), (EYES[1], (0.034, 0.07, 0.038), 12.0), (MOUTH, (0.045, 0.07, 0.05), 0.0),\n"
        "                            (NOSE, (0.013, 0.05, 0.024), 0.0)):")
s = rep(s, "        for c in EYES + [MOUTH]:", "        for c in EYES + [MOUTH, NOSE]:")
# no ground discs, no spill bone: the pool of light is a real light in the game (meta 'light')
a = s.index("    # ---- cold light spilling on the ground under it")
b = s.index("    # ---- carrying chain under the floor")
s = s[:a] + s[b:]
s = rep(s, '    sk.bone("spill", (0, 0, 0.07), (0, 0.1, 0.07), "root")\n', '')
s = rep(s, """    def glow(p, s=1.0, dx=0.0, dy=0.0, ember=1.0):
        p.scl('spill', max(0.001, s)); p.move('spill', dx, dy, 0)
        p.scl('ember', max(0.001, ember))""",
        """    def glow(p, s=1.0, dx=0.0, dy=0.0, ember=1.0):
        p.scl('ember', max(0.001, ember))""")
s = rep(s, "    C.meta.update(family='wraith', rig='F', scale=1.0, game_speed=5.0)",
        "    C.meta.update(family='wraith', rig='F', scale=1.0, game_speed=5.0)\n"
        "    # the pool of cold light it throws on the ground: a soft shadowless light the game hangs on the creature\n"
        "    C.meta['light'] = dict(color=[0.55, 0.78, 1.0], energy=0.9, range=3.2, height=round(h - 0.05, 2))")
save('spec_f.py', s)

# ------------------------------------------------------------------ spec_d
s = load('spec_d.py')
a = s.index("    lip = P.ring((0, 0, 0.012), 0.28 * S, 0.11 * S, n=22, m=6).scale((1, 1, 0.42))")
b = s.index("    # ---- motion helpers")
s = s[:a] + '''    def mud_ring(R, r, zs, lobes, seed, n=26):
        """an irregular swell of wet mud round the hole (never a clean hoop)"""
        q = P.ring((0, 0, 0.0), R, r, n=n, m=6)
        rg2 = np.random.default_rng(seed)
        ang = np.arctan2(q.v[:, 1], q.v[:, 0])
        k = 1 + 0.16 * np.sin(ang * lobes + seed) + 0.1 * np.sin(ang * (lobes * 2 + 1) + 2.0 * seed)
        q.v[:, 0] *= k; q.v[:, 1] *= k
        q.v[:, 2] *= zs * (0.6 + 0.8 * (0.5 + 0.5 * np.sin(ang * (lobes + 2) + 1.3 * seed)))
        q.rough(r * 0.3, 5.0, seed=seed)
        q.v[:, 2] += r * zs * 0.55
        return q
    C.rigid(mud_ring(0.30 * S, 0.14 * S, 0.34, 3, 6), 'root', 'mud', smooth=180, free=True)
    C.rigid(RD.mound(0.36 * S, 0.5 * S, n=9, seed=3, h=0.06), 'root', 'mud', free=True)
    for i, b in enumerate(('rip1', 'rip2')):
        C.rigid(mud_ring(0.48 * S, 0.075 * S, 0.14, 4 + i, 9 + i, n=30), b, 'mud', smooth=180, free=True)

''' + s[b:]
s = rep(s, "    C.look('mud', 'mud', 'rock_mound', color=(0.075, 0.06, 0.042), dry=(0.19, 0.16, 0.115))",
        "    C.look('mud', 'mud', 'rock_mound', color=(0.06, 0.047, 0.033), dry=(0.10, 0.082, 0.058), scale=3.0)")
save('spec_d.py', s)

# ------------------------------------------------------------------ bshots: a light from the meta
s = load('bshots.py')
s = rep(s, "    dur = {k: (a.frame_range[1] - a.frame_range[0]) / 30.0 for k, a in acts.items()}\n",
        "    dur = {k: (a.frame_range[1] - a.frame_range[0]) / 30.0 for k, a in acts.items()}\n"
        "    lt = meta.get('light')\n"
        "    if lt:      # the soft light the game hangs on this creature (rigs_bf_meta.json 'light'): shown in the previews too\n"
        "        ld = bpy.data.lights.new('creature_light', 'POINT')\n"
        "        ld.color = tuple(lt['color']); ld.energy = 55.0 * lt['energy'] * lt['range']; ld.shadow_soft_size = 0.25\n"
        "        try:\n"
        "            ld.use_shadow = False\n"
        "        except Exception:\n"
        "            pass\n"
        "        lo = bpy.data.objects.new('creature_light', ld)\n"
        "        lo.location = (0, 0, lt['height'])\n"
        "        bpy.context.scene.collection.objects.link(lo)\n")
s = rep(s, "                o.data.energy *= 0.38\n", "                o.data.energy *= (1.0 if o.name == 'creature_light' else 0.38)\n")
save('bshots.py', s)

# ------------------------------------------------------------------ bmeta: keep the optional entries
s = load('bmeta.py')
s = rep(s, "        'hover_in_model', 'buried', 'clips', 'family', 'rig', 'scale', 'triangles', 'bones')",
        "        'hover_in_model', 'buried', 'clips', 'family', 'rig', 'scale', 'triangles', 'bones', 'light', 'enlarged', 'wheel_radius',\n"
        "        'wheel_center_run')")
s = rep(s, "#   buried                  metres of body below the ground in every clip (burrowers);  clips  clip lengths in seconds",
        "#   buried                  metres of body below the ground in every clip (burrowers);  clips  clip lengths in seconds\n"
        "#   light                   optional {color [r,g,b], energy, range (m), height (m, model space)}: a soft shadowless light\n"
        "#                           the creature carries (a pool of light on the ground under it); off when it dies")
save('bmeta.py', s)
