"""boss_osaros v2 寂主・奧薩洛斯 — the dead Sun-Forge's last stoker-king, who fed the furnace all his life and at last became
its fuel.  Assembly of the creature from its sections (head / body / cloth / weapon)."""
import numpy as np
import monlib as M
import ov2_creature as CR
import ov2_looks as OL

NOTES = ("Osaros v2: gaunt fired-clay king. Head sculpted from the subdivided CC0 base head (heavy brow, deep orbits with ember "
         "pinpoints, hollow cheeks, broken nose, lipless mouth of long teeth sealed with forge staples, furnace light behind them); "
         "the left side of the face is hollow and broken open (emis_face: glowing inside, cracks and fissures). Crown grown through "
         "the scalp: forge nails, burnt-out candle stubs and their tallow; a broken iron sun-disc behind the head.")


def build(only=('head', 'body', 'cloth', 'weapon'), cid='boss_osaros'):
    C = CR.Creature2(cid, 45000)
    D = None
    if 'head' in only:
        import ov2_parts_head as PH
        D = PH.add_head(C)
    if 'body' in only or 'cloth' in only or 'weapon' in only:
        import ov2_parts_body as PB
        PB.add_body(C, D, only)
    return C, NOTES
