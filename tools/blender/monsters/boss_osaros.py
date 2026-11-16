"""boss_osaros 寂主・奧薩洛斯 — Osaros, Lord of Silence (v2): the dead Sun-Forge's last stoker-king, who fed the furnace all his
life and at last became its fuel.

A towering gaunt figure of fired clay and ash. The face is sculpted (osaros_v2\\ov2_head.py, ov2_dense.py): heavy level
brow, deep orbits with ember pinpoints, hollow cheeks, a burnt nose, a lipless mouth of long teeth sealed with forge
staples, furnace light behind them; his left cheekbone, temple and outer orbit are broken out of the hollow shell and
the dying forge shows inside; cracks run from the break. A crown grown through the scalp (forge nails, slumped candle
stubs) and a broken iron sun-disc behind the head. Body (ov2_parts_body.py): starved torso in an iron rib-cage grate
with the ember heart behind the bars, a yoke round the neck, charred beams chained to the right shoulder, a broken
tongueless forge bell on the left, clawed burnt gauntlets, a stoker's apron over a trailing soot-black robe with
ember-eaten hems, an ash-heavy torn mantle, dead lanterns on chains; wpn_1: the furnace rake, its tines still hot.

Three 2048 atlases (head / body / cloth), emissive faces in emis_face / emis_ember / emis_hem (ov2_bake.py splits them
off after the bake: exactly the faces that glow). The previous model: boss_osaros_v1.py, out\\boss_osaros_v1.glb.
Build:  build.py boss_osaros   (development builds that do not touch the game folder: osaros_v2\\dev_build.py)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "osaros_v2"))
import ov2_build


def build():
    return ov2_build.build()
