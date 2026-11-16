"""VoxCPM2 production engine: one process loads the model once and does synthesis, the ASR round-trip,
retries, and staging. Run with the `voxcpm` venv. Used by gen.py and osaros_page.py.

Staging (under %VT_ROOT%\\stage\\prod):
    anchors\\<voice>.wav + .json     the one take made from the voice DESCRIPTION (48 kHz float)
    dry\\<id>.wav + .json            every line, dry, exactly as the model produced it (48 kHz float)
    final\\<id>.ogg                  processed and encoded, ready for the game
A line is a continuation of its character's anchor (prompt = anchor audio + anchor text, reference =
anchor audio): synthetic self-reference, the model's own designed voice.
"""
from __future__ import annotations

import json
import os
import random
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
os.environ.setdefault("VT_THREADS", "6")          # the GPU is busy with the music job: CPU, at most 6 threads
import cpu_cap  # noqa: E402  (must precede torch)
import stubs  # noqa: E402

stubs.install("modelscope.pipelines", "modelscope.utils.constant")       # denoiser imports; never used
for _m, _a in (("modelscope.pipelines", "pipeline"), ("modelscope.utils.constant", "Tasks")):
    if _m in sys.modules and not hasattr(sys.modules[_m], _a):
        setattr(sys.modules[_m], _a, None)

import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402

import fx  # noqa: E402
import tw_text  # noqa: E402
import voices  # noqa: E402
import vt  # noqa: E402

torch.set_num_threads(cpu_cap.THREADS)
MODEL_DIR = os.path.join(vt.ROOT, "models", "voxcpm2")
PROD = os.path.join(vt.ROOT, "stage", "prod")
ANCHORS, DRY, FINAL = (os.path.join(PROD, d) for d in ("anchors", "dry", "final"))
LIMIT = 0.15                    # largest acceptable sound error rate of a take (0 is tried for first)
FULL = tw_text.LEVELS_ALL
SAFE = ("tw", "name")           # fallback when a take keeps failing: drop tone and polyphone hints


def log(msg: str, path: str | None = None):
    line = time.strftime("%H:%M:%S ") + msg
    print(line, flush=True)
    if path:
        with open(path, "a", encoding="utf-8") as f:
            f.write(line + "\n")


def running_file(what: str, log_path: str):
    """Y:\\tools\\voice_tw\\RUNNING.txt while a long batch runs (removed by done())."""
    with open(os.path.join(HERE, "RUNNING.txt"), "w", encoding="utf-8") as f:
        f.write(f"voice_tw batch is running: {what}\npid: {os.getpid()}\nstarted: {time.strftime('%Y-%m-%d %H:%M:%S')}\n"
                f"log: {log_path}\nstop it with:  Stop-Process -Id {os.getpid()} -Force\n"
                "The batch is resumable: start the same command again and it continues with what is missing.\n")


def done():
    try:
        os.remove(os.path.join(HERE, "RUNNING.txt"))
    except FileNotFoundError:
        pass


def trim(x: np.ndarray, sr: int, keep: float = 0.05) -> np.ndarray:
    env = np.abs(x)
    if not len(env) or env.max() <= 0:
        return x
    idx = np.nonzero(env > env.max() * 0.01)[0]
    return x[max(0, idx[0] - int(keep * sr)):min(len(x), idx[-1] + int(keep * sr))]


class Engine:
    def __init__(self, logfile: str | None = None):
        from voxcpm import VoxCPM
        import voxcpm.model.voxcpm2 as m2
        pick = m2.pick_runtime_dtype           # the checkpoint asks for bfloat16, which is very slow on a CPU
        m2.pick_runtime_dtype = lambda device, dtype: "float32" if str(device).startswith("cpu") else pick(device, dtype)
        self.logfile = logfile
        t0 = time.time()
        self.model = VoxCPM.from_pretrained(MODEL_DIR, load_denoiser=False, optimize=False, device="cpu")
        self.sr = self.model.tts_model.sample_rate
        self.asr = vt.Asr(threads=2)
        self.compute = 0.0
        self.audio = 0.0
        log(f"model loaded in {time.time() - t0:.0f} s, {torch.get_num_threads()} threads, {self.sr} Hz", logfile)

    # ---------------------------------------------------------------- one synthesis
    def synth(self, tts_text: str, seed: int, anchor: dict | None = None, desc: str | None = None) -> np.ndarray:
        kw = dict(cfg_value=2.0, inference_timesteps=10)
        if anchor:
            kw.update(prompt_wav_path=anchor["wav"], prompt_text=tw_text.to_voxcpm(anchor["text"]), reference_wav_path=anchor["wav"])
            text = tts_text
        else:
            text = f"({desc}){tts_text}"
        torch.manual_seed(seed); np.random.seed(seed); random.seed(seed)
        t0 = time.time()
        with torch.inference_mode():
            wav = self.model.generate(text=text, **kw)
        x = trim(np.asarray(wav, dtype=np.float32).reshape(-1), self.sr)
        self.compute += time.time() - t0
        self.audio += len(x) / self.sr
        return x

    def check(self, text: str, x: np.ndarray, levels) -> dict:
        """ASR round-trip and sanity facts for one dry take."""
        heard = self.asr.transcribe(x, self.sr)["text"]
        n = max(1, len(vt.for_compare(text)))
        dur = len(x) / self.sr
        r = dict(heard=vt.s2t(heard), cer=round(vt.cer_hint(text, heard, levels), 3), cer_strict=round(vt.cer_strict(text, heard), 3),
                 seconds=round(dur, 2), chars_per_s=round(n / max(dur, 0.1), 2), finite=bool(np.all(np.isfinite(x))),
                 peak=round(float(np.abs(x).max()), 3) if len(x) else 0.0)
        r["sane"] = r["finite"] and r["peak"] > 0.02 and 1.5 <= r["chars_per_s"] <= 8.5
        return r

    def take(self, text: str, anchor: dict | None, desc: str | None, tag: str, seeds=(1, 2, 3), fallback_seeds=(4, 5),
             perfect: float = 0.0, good: float = 0.06) -> tuple[np.ndarray, dict]:
        """Best of several attempts: stop at the first take the recogniser reads back perfectly; otherwise
        try another seed and keep the best; after two tries a take within `good` (one doubtful syllable in
        a long line) is accepted; if the best is still above LIMIT, try again with fewer hints."""
        best = None
        tries = []
        # Simplified input with all hints first; if the best take is still above LIMIT: Traditional input, then fewer hints
        plan = [(s, FULL, "s") for s in seeds] + [(s, FULL if k == 0 else SAFE, "t" if k == 0 else "s") for k, s in enumerate(fallback_seeds)]
        for k, (seed, levels, script) in enumerate(plan):
            if k >= len(seeds) and best[1]["cer"] <= LIMIT and best[1]["sane"]:
                break                                              # acceptable already; fallbacks are for failures only
            tts = tw_text.to_voxcpm(text, levels, script)
            t0 = time.time()
            x = self.synth(tts, seed, anchor=anchor, desc=desc)
            r = self.check(text, x, levels)
            r.update(seed=seed, tts_text=tts, levels="+".join(levels) + ("" if script == "s" else " traditional"), compute=round(time.time() - t0, 1))
            tries.append(dict(seed=seed, levels=r["levels"], cer=r["cer"], heard=r["heard"], seconds=r["seconds"], sane=r["sane"]))
            log(f"  {tag} seed {seed} {r['levels']}: cer {r['cer']:.3f} ({r['cer_strict']:.3f} strict) {r['seconds']:.1f}s "
                f"{r['compute'] / max(r['seconds'], 0.1):.1f}x  {r['heard']}", self.logfile)
            score = (0 if r["sane"] else 1, r["cer"])
            if best is None or score < best[2]:
                best = (x, r, score)
            if r["sane"] and r["cer"] <= perfect:
                break
            if len(tries) >= 2 and best[1]["sane"] and best[1]["cer"] <= good:
                break
        x, r, _ = best
        r["tries"] = tries
        r["ok"] = bool(r["sane"] and r["cer"] <= LIMIT)
        return x, r

    # ---------------------------------------------------------------- anchors and lines (resumable)
    def anchor(self, voice: str, desc: str | None = None, text: str | None = None, key: str | None = None, force: bool = False,
               f0: tuple | None = None, max_designs: int = 4, seed_base: int = 0) -> dict:
        """The anchor take of a voice: made once from its description; reused ever after.
        A design is repeated (new seed = new voice) until the text reads back exactly, the median pitch is
        inside the range wanted for this voice and the voice is clean enough (harmonics-to-noise ratio); if
        none fits within the allowed number of designs, the closest is kept and flagged. Every design is
        kept in anchors\\_designs so an interrupted run does not repeat them."""
        key = key or voice
        v = voices.get(voice)
        desc, text, f0 = desc or v["desc"], text or v["anchor"], f0 or v.get("f0")
        seed_base = seed_base or v.get("seed_base", 0)
        wav, meta = os.path.join(ANCHORS, key + ".wav"), os.path.join(ANCHORS, key + ".json")
        if not force and os.path.exists(wav) and os.path.exists(meta):
            m = json.load(open(meta, encoding="utf-8"))
            if m.get("desc") == desc and m.get("text") == text:
                return m
        designs = os.path.join(ANCHORS, "_designs")
        os.makedirs(designs, exist_ok=True)
        hnr_min = v.get("hnr_min", 4.0)             # harmonics-to-noise floor: below it a voice is rough / hoarse
        cands = []
        for seed in range(seed_base + 1, seed_base + v.get("designs", max_designs) + 1):
            dwav, dmeta = os.path.join(designs, f"{key}_{seed}.wav"), os.path.join(designs, f"{key}_{seed}.json")
            cached = json.load(open(dmeta, encoding="utf-8")) if os.path.exists(dmeta) and os.path.exists(dwav) else None
            if cached and cached.get("desc") == desc and cached.get("text") == text:       # measured in an earlier run
                r, st = cached["r"], cached["st"]
                x = vt.read_wav(dwav)[0]
            else:
                x, r = self.take(text, None, desc, f"anchor {key}", seeds=(seed,), fallback_seeds=())
                st = vt.voice_stats(x, self.sr)
                vt.write_float(dwav, x, self.sr)
                json.dump(dict(desc=desc, text=text, r=r, st=st), open(dmeta, "w", encoding="utf-8"), ensure_ascii=False)
            off = 0.0 if not f0 or not st["f0"] else max(f0[0] - st["f0"], st["f0"] - f0[1], 0) / st["f0"]
            rough = max(0.0, hnr_min - (st["hnr"] or 0))
            exact = r["sane"] and r["cer"] <= 0.04
            pm = sex_score(dwav)                               # second gate: male / female classifier on the speaker embedding
            sex = v.get("sex")
            sex_ok = sex_fits(pm, sex)
            st = dict(st, p_male=pm)
            verdict = "reads back exactly" if exact else "does NOT read back exactly"
            log(f"  anchor {key} design {seed}: median pitch {st['f0']} Hz (wanted {f0}), p(male) {pm} (character: {sex}), "
                f"harmonics-to-noise {st['hnr']} dB (at least {hnr_min}), {r['chars_per_s']} ch/s, {verdict}", self.logfile)
            # best = reads back exactly, right sex, pitch in range, clean enough; then the most clearly male / female
            sure = -(pm or 0) if sex == "m" else (pm or 0)
            cands.append(((0 if exact else 1, 0 if sex_ok else 1, round(off, 3), round(rough, 1), sure, -(st["hnr"] or 0)), x, r, st, seed))
            if exact and sex_ok and off == 0 and rough == 0 and not v.get("explore"):
                break
        _, x, r, st, kept = min(cands, key=lambda c: c[0])
        vt.write_float(wav, x, self.sr)
        pm, sex = st.get("p_male"), v.get("sex")
        sex_ok = sex_fits(pm, sex)
        m = dict(voice=voice, key=key, desc=desc, text=text, wav=wav, median_f0=st["f0"], hnr=st["hnr"], p_male=pm, sex=sex, sex_ok=bool(sex_ok),
                 f0_wanted=list(f0) if f0 else None, hnr_min=hnr_min,
                 in_range=bool((not f0 or (st["f0"] and f0[0] <= st["f0"] <= f0[1])) and (st["hnr"] or 0) >= hnr_min and sex_ok),
                 designs_tried=len(cands), design_kept=kept, uid=f"{key}-{kept}-{int(time.time())}", **r)
        json.dump(m, open(meta, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        return m

    def line(self, line_id: str, voice: str, text: str, anchor: dict, force: bool = False, seeds=(1, 2, 3)) -> dict:
        """One script line, dry, as a continuation of the character's anchor."""
        wav, meta = os.path.join(DRY, line_id + ".wav"), os.path.join(DRY, line_id + ".json")
        if not force and os.path.exists(wav) and os.path.exists(meta):
            m = json.load(open(meta, encoding="utf-8"))
            if m.get("text") == text and m.get("anchor_uid") == anchor.get("uid"):      # made from THIS anchor
                return m
        os.makedirs(DRY, exist_ok=True)
        x, r = self.take(text, anchor, None, line_id, seeds=seeds)
        vt.write_float(wav, x, self.sr)
        m = dict(id=line_id, voice=voice, text=text, wav=wav, anchor_key=anchor.get("key"), anchor_seed=anchor.get("seed"),
                 anchor_uid=anchor.get("uid"), **r)
        json.dump(m, open(meta, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        return m


# ------------------------------------------------------------------ male / female check (gender.py)

# Thresholds on p(male). The classifier is 99 % right on its own kind of voice (Kokoro), but on VoxCPM2 voices its
# scores bunch towards the middle: voices heard as women score 0.28-0.42 (including the first elder, an old man
# the user heard as an old woman), clear men 0.68-0.87. So: a man must reach 0.60, a woman must stay under 0.45.
SEX_M_MIN, SEX_F_MAX = 0.60, 0.45


def sex_fits(pm, sex) -> bool:
    if pm is None or sex not in ("m", "f"):
        return True
    return pm >= SEX_M_MIN if sex == "m" else pm <= SEX_F_MAX


def sex_score(*wavs: str) -> float | None:
    """Mean p(male) of the given takes (None if the classifier has not been calibrated)."""
    import gender
    if not os.path.exists(gender.MODEL):
        return None
    emb = embeddings(list(wavs))
    return round(float(np.mean([gender.p_male(emb[w]) for w in wavs])), 2)


# ------------------------------------------------------------------ speaker similarity (cosy venv, CAM++)

def embeddings(paths: list[str]) -> dict:
    """{path: unit vector} through spk.py in the `cosy` venv (kept out of this process: two ONNX runtimes)."""
    tmp = os.path.join(PROD, "spk_in.json")
    out = os.path.join(PROD, "spk_out.json")
    os.makedirs(PROD, exist_ok=True)
    json.dump(paths, open(tmp, "w", encoding="utf-8"))
    py = os.path.join(vt.ROOT, "venvs", "cosy", "Scripts", "python.exe")
    r = subprocess.run([py, os.path.join(HERE, "spk.py"), tmp, out], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        raise RuntimeError("spk.py failed: " + r.stderr[-1500:])
    return {k: np.array(v) for k, v in json.load(open(out, encoding="utf-8")).items()}


def cos(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.dot(a, b))
