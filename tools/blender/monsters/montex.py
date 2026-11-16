"""Scanned CC0 PBR sets used as the detail source of the monster looks (looks.py).

Raw downloads live in Y:\\assets_src\\montex\\<source id>\\ (see Y:\\credits\\monsters.md); montex_prep.py turns each
set into one cache file  cache\\montex\\<alias>.npz  holding, at 1024 px (tileable):
    det    float16 (h,w,3)  albedo detail: linear albedo / its low-pass  (1.0 = "no change"; multiply the look colour)
    rough  uint8   (h,w)    scan roughness
    h      float32 (h,w)    height in TILE UNITS, integrated from the scan's OpenGL normal map (zero mean).
                            physical height = h * tile size in metres
    ao     uint8   (h,w)    scan ambient occlusion (or one derived from the height)
    meta   json: mean albedo (linear), slope_rms (rms surface slope of the scan), rough mean / std, size_m
looks.NB.scan() loads them as float images and samples them box-projected in object space.
"""
import os, json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"Y:\assets_src\montex"
CACHE = os.path.join(HERE, "cache", "montex")

# alias -> source.  src 'ph' = Poly Haven (maps <dir>\diff_1k.jpg ...), 'acg' = ambientCG (<id>_1K-JPG_Color.jpg ...)
# size = real-world width of one tile in metres (Poly Haven "dimensions"; ambientCG sets are estimated)
SETS = {
    # ---- fabric
    'burlap':      dict(src='ph', id='hessian_230', size=0.27, author='colormass, Rico Cilliers'),
    'linen':       dict(src='ph', id='rough_linen', size=0.27, author='colormass, Rico Cilliers'),
    'wool':        dict(src='ph', id='caban', size=0.27, author='colormass, Rico Cilliers'),
    'fleece':      dict(src='ph', id='curly_teddy_natural', size=0.33, author='colormass, Rico Cilliers'),
    'wool_weave':  dict(src='acg', id='Fabric031', size=0.30, author='Lennart Demes (ambientCG)'),
    # ---- leather / hide
    'leather':     dict(src='ph', id='brown_leather', size=0.40, author='Rob Tuytel'),
    'leather_b':   dict(src='ph', id='fabric_leather_02', size=0.50, author='Rob Tuytel'),
    'hide':        dict(src='ph', id='leather_white', size=0.30, author='Rob Tuytel'),
    'leather_old': dict(src='acg', id='Leather014', size=0.50, author='Lennart Demes (ambientCG)'),
    'scales':      dict(src='acg', id='Leather008', size=0.30, author='Lennart Demes (ambientCG)'),
    # ---- metal
    'rust':        dict(src='ph', id='rusty_metal_02', size=1.0, author='Rob Tuytel'),
    'plate':       dict(src='ph', id='metal_plate', size=0.5, author='Rob Tuytel'),
    'verdigris':   dict(src='ph', id='green_metal_rust', size=1.0, author='Rob Tuytel'),
    'rust_pit':    dict(src='ph', id='rust_coarse_01', size=2.2, author='Dimitrios Savva, Rico Cilliers'),
    'iron':        dict(src='ph', id='rusty_metal_03', size=1.35, author='Amal Kumar'),
    'iron_b':      dict(src='ph', id='rusty_metal_04', size=2.0, author='Amal Kumar'),
    'steel_plate': dict(src='ph', id='metal_plate_02', size=2.0, author='Rob Tuytel'),
    'bronze':      dict(src='acg', id='Metal017', size=0.6, author='Lennart Demes (ambientCG)'),
    'steel_scr':   dict(src='acg', id='Metal038', size=0.6, author='Lennart Demes (ambientCG)'),
    'iron_scr':    dict(src='acg', id='Metal054B', size=0.6, author='Lennart Demes (ambientCG)'),
    # ---- bone stand-ins (porous stone / plaster)
    'coral':       dict(src='ph', id='coral_fort_wall_02', size=0.99, author='Dimitrios Savva'),
    'plaster':     dict(src='ph', id='white_plaster_rough_01', size=1.0, author='Rob Tuytel'),
    # ---- rock / burnt / mud / ice
    'rock_crk':    dict(src='ph', id='rock_boulder_cracked', size=1.39, author='Dario Barresi, Dimitrios Savva'),
    'basalt':      dict(src='ph', id='dark_rock', size=2.42, author='Amal Kumar'),
    'burnt':       dict(src='ph', id='burned_ground_01', size=1.0, author='Rob Tuytel'),
    'mud_crk':     dict(src='ph', id='mud_cracked_dry_03', size=1.5, author='Dario Barresi, Dimitrios Savva'),
    'mud_crk2':    dict(src='ph', id='mud_cracked_dry_riverbed_002', size=2.0, author='Dario Barresi, Dimitrios Savva'),
    'snow':        dict(src='ph', id='snow_02', size=2.0, author='Rob Tuytel'),
    'ice':         dict(src='acg', id='Ice003', size=1.0, author='Lennart Demes (ambientCG)'),
    'lava':        dict(src='acg', id='Lava001', size=1.0, author='Lennart Demes (ambientCG)'),
    'obsidian':    dict(src='acg', id='Rock035', size=1.0, author='Lennart Demes (ambientCG)'),
    # ---- wood
    'bark':        dict(src='ph', id='bark_brown_02', size=1.0, author='Rob Tuytel'),
    'oak':         dict(src='ph', id='oak_veneer_01', size=1.83, author='Jenelle van Heerden'),
}


# used when a set has not been downloaded / prepared (the build then logs which stand-in it took)
FALLBACK = {
    'steel_scr': 'bronze', 'iron_scr': 'iron', 'bronze': 'iron', 'obsidian': 'basalt', 'ice': 'snow',
    'wool_weave': 'wool', 'leather_old': 'leather', 'scales': 'mud_crk', 'lava': 'rock_crk', 'steel_plate': 'bronze',
}


def resolve(alias):
    """alias itself if its cache exists, else the first prepared stand-in from FALLBACK"""
    a = alias
    seen = set()
    while not available(a):
        seen.add(a)
        a = FALLBACK.get(a)
        if a is None or a in seen:
            raise RuntimeError("scanned set %r is not prepared (run montex_prep.py)" % alias)
    return a


def src_maps(alias):
    """paths of the raw maps of a set: dict(diff, nor, rough, disp, ao) (missing ones are None)"""
    s = SETS[alias]
    d = os.path.join(SRC, s['id'])
    if s['src'] == 'ph':
        names = dict(diff='diff_1k.jpg', nor='nor_gl_1k.jpg', rough='rough_1k.jpg', disp='disp_1k.jpg', ao='ao_1k.jpg')
    else:
        p = s['id'] + '_1K-JPG_'
        names = dict(diff=p + 'Color.jpg', nor=p + 'NormalGL.jpg', rough=p + 'Roughness.jpg', disp=p + 'Displacement.jpg',
                     ao=p + 'AmbientOcclusion.jpg')
    out = {}
    for k, n in names.items():
        f = os.path.join(d, n)
        out[k] = f if os.path.isfile(f) else None
    return out


def cache_path(alias):
    return os.path.join(CACHE, alias + ".npz")


_LOADED = {}


def load(alias):
    """dict(det, rough, h, ao, meta) of numpy arrays (cached per process)"""
    if alias not in _LOADED:
        z = np.load(cache_path(alias), allow_pickle=False)
        _LOADED[alias] = dict(det=z['det'].astype(np.float32), rough=z['rough'].astype(np.float32) / 255.0,
                              h=z['h'].astype(np.float32), ao=z['ao'].astype(np.float32) / 255.0,
                              meta=json.loads(str(z['meta'])))
    return _LOADED[alias]


def available(alias):
    return alias in SETS and os.path.isfile(cache_path(alias))


def box_down(a, f):
    """box-filter downsample by an integer factor"""
    if f <= 1:
        return a
    h, w = a.shape[:2]
    h2, w2 = h // f, w // f
    a = a[:h2 * f, :w2 * f]
    if a.ndim == 2:
        return a.reshape(h2, f, w2, f).mean(axis=(1, 3))
    return a.reshape(h2, f, w2, f, a.shape[2]).mean(axis=(1, 3))
