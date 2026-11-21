"""Palette of the gear models: source materials of the hero pipeline (mats.py), defined lazily on first use.

Keys matter: mats.flat / mats.leather / mats.wood / mats.cloth pick the scanned substance from words in the key
(rope / cord, paper, wax, feather, crystal / gem / glass, tome, hide, bark, rag ...).
Wood keys ending in "box" have no lofted grain (blocks, boards, carved lumps); the others need the 'luv' attribute
(gearparts.haft / lathe_w / tube_w).
"""
from gearlib import *

PALETTE = {
    # ---------------------------------------------------------------- metals
    "iron":       ("metal", dict(base=(0.16, 0.155, 0.15), rough=(0.45, 0.72), rust_amt=0.40, ash=0.0, pattern="hammered")),
    "rustiron":   ("metal", dict(base=(0.125, 0.105, 0.09), rough=(0.55, 0.85), rust_amt=0.70, ash=0.0, pattern="hammered", edge_amt=0.45)),
    "crudeblade": ("metal", dict(base=(0.17, 0.16, 0.15), rough=(0.38, 0.68), rust_amt=0.42, ash=0.0, pattern="brushed", edge_amt=0.35)),
    "steel":      ("metal", dict(base=(0.33, 0.33, 0.33), rough=(0.26, 0.52), rust_amt=0.12, ash=0.0, pattern="hammered", edge_amt=0.9)),
    "blade":      ("metal", dict(base=(0.40, 0.40, 0.41), rough=(0.20, 0.42), rust_amt=0.07, ash=0.0, pattern="brushed", edge_amt=0.6)),
    "blade_dark": ("metal", dict(base=(0.085, 0.085, 0.095), rough=(0.25, 0.50), rust_amt=0.05, ash=0.0, pattern="brushed",
                                 edge=(0.20, 0.19, 0.20), edge_amt=0.10)),
    "blade_pale": ("metal", dict(base=(0.56, 0.54, 0.50), rough=(0.16, 0.36), rust_amt=0.02, ash=0.0, pattern="brushed",
                                 edge=(0.90, 0.80, 0.60), edge_amt=1.0)),
    "darksteel":  ("metal", dict(base=(0.075, 0.08, 0.09), rough=(0.28, 0.52), rust_amt=0.06, ash=0.0, edge=(0.19, 0.21, 0.24), edge_amt=0.28)),
    "blackiron":  ("metal", dict(base=(0.05, 0.048, 0.05), rough=(0.35, 0.60), rust_amt=0.10, ash=0.0, pattern="hammered",
                                 edge=(0.50, 0.36, 0.24))),
    "bronze":     ("metal", dict(base=(0.36, 0.215, 0.075), rough=(0.28, 0.50), rust=(0.045, 0.085, 0.06), rust_amt=0.22, ash=0.0)),
    "brass":      ("metal", dict(base=(0.32, 0.21, 0.07), rough=(0.30, 0.55), rust=(0.04, 0.07, 0.05), rust_amt=0.20, ash=0.0)),
    "gilt":       ("metal", dict(base=(0.47, 0.305, 0.085), rough=(0.24, 0.48), rust=(0.05, 0.07, 0.05), rust_amt=0.10, ash=0.0, scan="gold")),
    "silver":     ("metal", dict(base=(0.42, 0.41, 0.38), rough=(0.25, 0.45), rust_amt=0.12, rust=(0.06, 0.055, 0.05), ash=0.0)),
    "starmetal":  ("metal", dict(base=(0.085, 0.115, 0.19), rough=(0.34, 0.58), edge=(0.42, 0.52, 0.68), edge_amt=0.9, rust_amt=0.08, ash=0.0)),
    "copper":     ("metal", dict(base=(0.40, 0.17, 0.085), rough=(0.30, 0.55), rust=(0.05, 0.16, 0.12), rust_amt=0.30, ash=0.0)),
    # ---------------------------------------------------------------- leather
    "strap":      ("leather", dict(base=(0.085, 0.05, 0.026), worn=0.7, ash=0.0)),
    "grip":       ("leather", dict(base=(0.045, 0.028, 0.018), worn=0.6, ash=0.0)),
    "grip_black": ("leather", dict(base=(0.020, 0.018, 0.017), worn=0.5, ash=0.0)),
    "grip_red":   ("leather", dict(base=(0.110, 0.022, 0.018), worn=0.6, ash=0.0)),
    "hide":       ("leather", dict(base=(0.066, 0.052, 0.038), worn=0.7, crack=1.0, ash=0.0)),
    "tan":        ("leather", dict(base=(0.150, 0.105, 0.060), worn=0.8, crack=1.0, ash=0.0)),
    "tome":       ("leather", dict(base=(0.060, 0.020, 0.020), ash=0.0, worn=0.8)),
    "tome_dark":  ("leather", dict(base=(0.022, 0.019, 0.024), ash=0.0, worn=0.8)),
    "tome_brown": ("leather", dict(base=(0.070, 0.042, 0.022), ash=0.0, worn=0.9)),
    # ---------------------------------------------------------------- wood
    "wood":       ("wood", dict(base=(0.090, 0.052, 0.028))),
    "woodbox":    ("wood", dict(base=(0.090, 0.052, 0.028), luv_grain=False)),
    "darkwood":   ("wood", dict(base=(0.030, 0.022, 0.020))),
    "darkbox":    ("wood", dict(base=(0.030, 0.022, 0.020), luv_grain=False)),
    "palewood":   ("wood", dict(base=(0.170, 0.115, 0.065))),
    "palebox":    ("wood", dict(base=(0.170, 0.115, 0.065), luv_grain=False)),
    "ebony":      ("wood", dict(base=(0.016, 0.013, 0.013))),
    "ebonybox":   ("wood", dict(base=(0.016, 0.013, 0.013), luv_grain=False)),
    "bark":       ("wood", dict(base=(0.082, 0.066, 0.050), luv_grain=False, rough=(0.75, 0.95))),
    # ---------------------------------------------------------------- bone / horn
    "bone":       ("bone", dict()),
    "oldbone":    ("bone", dict(base=(0.37, 0.31, 0.215), stain=(0.12, 0.08, 0.04))),
    "horn":       ("bone", dict(base=(0.085, 0.07, 0.06), stain=(0.02, 0.015, 0.012))),
    "ivory":      ("bone", dict(base=(0.60, 0.55, 0.43), stain=(0.20, 0.13, 0.06), cracks=0.5)),
    "bone_clean": ("bone", dict(base=(0.46, 0.41, 0.31), stain=(0.16, 0.10, 0.05), cracks=0.12)),
    # ---------------------------------------------------------------- small one-substance props
    "rope":           ("flat", dict(color=(0.20, 0.16, 0.10), rough=0.9)),
    "cord_dark":      ("flat", dict(color=(0.035, 0.028, 0.022), rough=0.85)),
    "cord_red":       ("flat", dict(color=(0.20, 0.022, 0.02), rough=0.85)),
    "paper":          ("flat", dict(color=(0.33, 0.29, 0.21), rough=0.85)),
    "wax":            ("flat", dict(color=(0.55, 0.50, 0.38), rough=0.5)),
    "wax_red":        ("flat", dict(color=(0.30, 0.02, 0.02), rough=0.45)),
    "feather":        ("flat", dict(color=(0.24, 0.22, 0.18), rough=0.8)),
    "feather_dark":   ("flat", dict(color=(0.045, 0.04, 0.035), rough=0.8)),
    "feather_red":    ("flat", dict(color=(0.26, 0.035, 0.02), rough=0.8)),
    "stone":          ("flat", dict(color=(0.20, 0.19, 0.175), rough=0.85)),
    "crystal_clear":  ("flat", dict(color=(0.50, 0.58, 0.66), rough=0.14)),
    "crystal_blue":   ("flat", dict(color=(0.022, 0.070, 0.26), rough=0.12)),
    "crystal_violet": ("flat", dict(color=(0.12, 0.03, 0.26), rough=0.12)),
    "crystal_red":    ("flat", dict(color=(0.30, 0.015, 0.02), rough=0.14)),
    "crystal_green":  ("flat", dict(color=(0.03, 0.26, 0.07), rough=0.14)),
    "crystal_amber":  ("flat", dict(color=(0.42, 0.19, 0.03), rough=0.14)),
    "crystal_smoke":  ("flat", dict(color=(0.05, 0.05, 0.06), rough=0.12)),
    "glass_horn":     ("flat", dict(color=(0.36, 0.25, 0.10), rough=0.22)),
    "gem_ruby":       ("flat", dict(color=(0.42, 0.012, 0.018), rough=0.10)),
    "gem_sapphire":   ("flat", dict(color=(0.025, 0.09, 0.50), rough=0.10)),
    "gem_topaz":      ("flat", dict(color=(0.55, 0.30, 0.03), rough=0.10)),
    "gem_emerald":    ("flat", dict(color=(0.02, 0.33, 0.07), rough=0.10)),
    "gem_amethyst":   ("flat", dict(color=(0.24, 0.05, 0.45), rough=0.10)),
    # ---------------------------------------------------------------- cloth
    "wrap":       ("cloth", dict(base=(0.165, 0.135, 0.095), folds=0.2, ash=0.0, worn=0.6, weave=2.0)),
    "rag":        ("cloth", dict(base=(0.085, 0.07, 0.055), folds=0.3, ash=0.0, worn=0.7)),
    "rag_red":    ("cloth", dict(base=(0.19, 0.028, 0.022), folds=0.3, ash=0.0, worn=0.6)),
    "sash_dark":  ("cloth", dict(base=(0.035, 0.03, 0.04), folds=0.3, ash=0.0, worn=0.5)),
    "velvet_blue": ("cloth", dict(base=(0.03, 0.045, 0.12), folds=0.2, ash=0.0, worn=0.3)),
}
for _k, _c, _s in EMIS:
    PALETTE[_k] = ("emis", dict(color=_c, strength=_s))


def _tone(c, k, white=0.0):
    return tuple(min(1.0, v * k * (1 - white) + white) for v in c)


# cut stones are baked in three tones (dark / mid / light facets): <key>_lo, <key>, <key>_hi
for _k in [k for k in list(PALETTE) if k.startswith(("gem_", "crystal_"))]:
    _cls, _kw = PALETTE[_k]
    PALETTE[_k + "_lo"] = (_cls, dict(_kw, color=_tone(_kw["color"], 0.42)))
    PALETTE[_k + "_hi"] = (_cls, dict(_kw, color=_tone(_kw["color"], 2.1, 0.10), rough=_kw["rough"] * 0.8))
PALETTE["gem_glint"] = ("flat", dict(color=(0.86, 0.88, 0.92), rough=0.08))


def use(key):
    """make sure the source material of `key` exists in this scene"""
    if key in MATCLS:
        return key
    cls, kw = PALETTE[key]
    fn = dict(metal=mats.metal, leather=mats.leather, wood=mats.wood, bone=mats.bone, flat=mats.flat, cloth=mats.cloth,
              emis=mats.emis)[cls]
    fn(key, **kw)
    MATCLS[key] = cls
    return key
