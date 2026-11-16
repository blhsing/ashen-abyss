"""Scanned CC0 PBR sets (and a few synthesised tiles) used as the detail source of the hero / NPC materials.

Raw downloads: Y:\\assets_src\\chartex\\<source id>\\ (credits: Y:\\credits\\heroes.md). Sets marked 'env' reuse the game's
environment texture sets (Y:\\game\\assets\\env\\tex\\<name>, also CC0). chartex_prep.py turns every set into
_texcache\\<alias>.npz holding (tileable, RES px):
    det    float16 (h,w,3)  albedo detail: linear albedo / its mean colour (1.0 = no change -> multiplies a base colour)
    h      float16 (h,w)    height / h_std (unit variance, zero mean), integrated from the scan's OpenGL normal map
    rough  uint8   (h,w)    scan roughness
    aux    uint8   (h,w,2)  free channels (synth sets: e.g. fur tip-ness, cavity)
    meta   json   (mean albedo, native size_m, h_std in tile units, slope rms, dominant period in px)
mats.G.scan() samples them box-projected in object space; one cache texel should cover about one atlas texel, so `tile`
(metres per repeat as used on the characters, usually larger than life) is part of the registry.
"""
import os, json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"Y:\assets_src\chartex"
ENV = r"Y:\game\assets\env\tex"
CACHE = os.path.join(HERE, "_texcache")

# alias -> dict(kind, id, size (native metres per tile), tile (metres per repeat on a character), res (cache px))
SETS = {
    # ---- fabric
    'wool':      dict(kind='ph', id='caban', size=0.27, tile=0.55, author='colormass, Rico Cilliers'),
    'linen':     dict(kind='ph', id='rough_linen', size=0.27, tile=0.62, author='colormass, Rico Cilliers'),
    'twill':     dict(kind='ph', id='poly_wool_herringbone', size=0.27, tile=0.60, author='colormass, Rico Cilliers'),
    'brocade':   dict(kind='ph', id='quatrefoil_jacquard_fabric', size=0.28, tile=0.50, author='colormass, Rico Cilliers'),
    'burlap':    dict(kind='env', id='burlap', src_id='hessian_230', size=0.27, tile=0.60, author='colormass, Rico Cilliers'),
    'velvet':    dict(kind='env', id='red_velvet', src_id='velour_velvet', size=0.28, tile=0.50, author='colormass, Rico Cilliers'),
    'canvas':    dict(kind='env', id='canvas', src_id='Fabric063', size=0.40, tile=0.60, author='Lennart Demes (ambientCG)'),
    'fleece':    dict(kind='ph', id='curly_teddy_natural', size=0.33, tile=0.45, author='colormass, Rico Cilliers'),
    # ---- leather / hide
    'leather':   dict(kind='ph', id='brown_leather', size=0.40, tile=0.55, author='Rob Tuytel'),
    'leather_b': dict(kind='ph', id='fabric_leather_02', size=0.50, tile=0.60, author='Rob Tuytel'),
    'hide':      dict(kind='ph', id='leather_white', size=0.30, tile=0.50, author='Rob Tuytel'),
    'leather_old': dict(kind='acg', id='Leather014', size=0.50, tile=0.55, author='Lennart Demes (ambientCG)'),
    # ---- metal
    'steel':     dict(kind='acg', id='Metal050C', size=1.0, tile=0.60, author='Lennart Demes (ambientCG)'),
    'iron':      dict(kind='acg', id='Metal038', size=1.0, tile=0.60, author='Lennart Demes (ambientCG)'),
    'rust':      dict(kind='ph', id='rusty_metal_02', size=1.0, tile=0.70, author='Rob Tuytel'),
    'rust_b':    dict(kind='env', id='rusty_iron', src_id='rusty_metal_04', size=2.0, tile=0.80, author='Amal Kumar'),
    'verdigris': dict(kind='ph', id='green_metal_rust', size=1.0, tile=0.70, author='Rob Tuytel'),
    'bronze':    dict(kind='env', id='tarnished_bronze', src_id='Metal008', size=1.0, tile=0.60, author='Lennart Demes (ambientCG)'),
    'bronze_c':  dict(kind='env', id='corroded_bronze', src_id='Metal017', size=1.0, tile=0.60, author='Lennart Demes (ambientCG)'),
    'gold':      dict(kind='env', id='aged_gold', src_id='Metal048A', size=1.0, tile=0.60, author='Lennart Demes (ambientCG)'),
    # ---- wood / bark / bone / rope
    'bark':      dict(kind='ph', id='bark_brown_02', size=1.0, tile=0.60, author='Rob Tuytel'),
    'bark_b':    dict(kind='env', id='bark', src_id='bark_willow_02', size=2.15, tile=0.80, author='Charlotte Baglioni'),
    'wood':      dict(kind='env', id='rough_beam', src_id='rough_wood', size=0.50, tile=0.50, author='Rob Tuytel'),
    'bone':      dict(kind='ph', id='coral_fort_wall_02', size=0.99, tile=0.60, author='Dimitrios Savva'),
    'rope':      dict(kind='env', id='rope', src_id='Rope001', size=0.30, tile=0.30, author='Lennart Demes (ambientCG)'),
    # ---- synthesised in chartex_prep.py (no third-party data)
    'fur':       dict(kind='syn', id='fur', size=0.30, tile=0.34, res=384),
    'fur_long':  dict(kind='syn', id='fur_long', size=0.40, tile=0.46, res=384),
    'mail':      dict(kind='syn', id='mail', size=0.16, tile=0.20, res=192),
}
RES = 512


def src_maps(alias):
    """paths of the raw maps of a set: dict(diff, nor, rough, disp) (missing -> None); env sets pack roughness in arm.G"""
    s = SETS[alias]
    if s['kind'] == 'ph':
        d = os.path.join(SRC, s['id'])
        names = dict(diff='diff_1k.jpg', nor='nor_gl_1k.jpg', rough='rough_1k.jpg', disp='disp_1k.jpg')
    elif s['kind'] == 'env':
        d = os.path.join(ENV, s['id'])
        names = dict(diff='albedo.jpg', nor='normal.jpg', arm='arm.jpg', disp='height.jpg')
    elif s['kind'] == 'acg':
        d = os.path.join(SRC, s['id'])
        p = s['id'] + '_1K-JPG_'
        names = dict(diff=p + 'Color.jpg', nor=p + 'NormalGL.jpg', rough=p + 'Roughness.jpg', disp=p + 'Displacement.jpg')
    else:
        return {}
    out = {}
    for k, n in names.items():
        f = os.path.join(d, n)
        out[k] = f if os.path.isfile(f) else None
    return out


def cache_path(alias):
    return os.path.join(CACHE, alias + ".npz")


def available(alias):
    return alias in SETS and os.path.isfile(cache_path(alias))


_LOADED = {}


def load(alias):
    if alias not in _LOADED:
        z = np.load(cache_path(alias), allow_pickle=False)
        _LOADED[alias] = dict(det=z['det'].astype(np.float32), h=z['h'].astype(np.float32),
                              rough=z['rough'].astype(np.float32) / 255.0, aux=z['aux'].astype(np.float32) / 255.0,
                              meta=json.loads(str(z['meta'])))
    return _LOADED[alias]


def meta(alias):
    return load(alias)['meta']
