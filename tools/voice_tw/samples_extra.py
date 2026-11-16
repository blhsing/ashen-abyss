"""Extra BreezyVoice variant for the listening test: language model unprompted (clean Taiwan readings),
timbre and consonant colour from the bundled Taiwanese example (shifted per role)."""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import samples_run as R
import samples_spec as S

jobs = [dict(out=os.path.join(S.STAGE, "breezyvoice_tw2", s["key"] + ".wav"), text=text, mode="native", seed=1, **R.tw_ref(s["voice"]))
        for s, text in R.dry_samples()]
R.save_timing("breezyvoice_tw2", R.run_driver("breezy", "synth_breezy.py", jobs, "breezy_tw2"))
