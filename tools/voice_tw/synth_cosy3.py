"""Fun-CosyVoice3-0.5B-2512 (FunAudioLLM, Apache-2.0) batch driver - CPU, model loaded once.

    <venvs\\cosy python> synth_cosy3.py jobs.json
    jobs = [{"out": wav, "text": zh-TW line, "ref_wav": path, "ref_text": transcript, "seed": 1,
             "speed": 1.0, "script": "s"}]

Zero-shot mode: the language model continues from the reference clip, so the ACCENT follows the
reference (CosyVoice 3 has no Taiwanese-Mandarin instruction; its dialect list only has 閩南話).
Readings that differ from the mainland default are written with CosyVoice 3's pinyin tokens,
e.g. 寂 -> [j][í]. The official repo is used unmodified; two things are stubbed at import time:
  * wetext (text normaliser that downloads FST models; our lines have no digits, we pass text_frontend=False)
  * the 1 GB CosyVoice-BlankEN/model.safetensors: only its config is needed, llm.pt overwrites every weight.
"""
import json
import os
import random
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # never depend on the console code page
import time

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
REPO = os.path.join(ROOT, "src", "CosyVoice")
MODEL_DIR = os.path.join(ROOT, "models", "cosy3")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path[:0] = [REPO, os.path.join(REPO, "third_party", "Matcha-TTS")]
os.chdir(REPO)
sys.modules["wetext"] = None            # -> ImportError inside the front end -> "no frontend", nothing downloaded
sys.path.insert(0, HERE)
import cpu_cap  # noqa: E402  (thread limits; must precede torch)
import stubs  # noqa: E402

stubs.install("pyworld", "gdown")   # dataset / download helpers, never called at inference

import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402
import transformers  # noqa: E402

if not os.path.exists(os.path.join(MODEL_DIR, "CosyVoice-BlankEN", "model.safetensors")):
    _orig = transformers.Qwen2ForCausalLM.from_pretrained.__func__

    def _config_only(cls, path, *a, **k):
        return cls(transformers.AutoConfig.from_pretrained(path))

    transformers.Qwen2ForCausalLM.from_pretrained = classmethod(_config_only)

from cosyvoice.cli.cosyvoice import AutoModel  # noqa: E402

sys.path.insert(0, HERE)
import tw_text  # noqa: E402
import vt  # noqa: E402

torch.set_num_threads(cpu_cap.THREADS)
ALWAYS_HINT = set("燼")              # rare characters at the heart of the game's vocabulary
PREFIX = "You are a helpful assistant.<|endofprompt|>"


def token_inventory() -> set[str]:
    src = open(os.path.join(REPO, "cosyvoice", "tokenizer", "tokenizer.py"), encoding="utf-8").read()
    import re
    return set(re.findall(r'"(\[[^"\]]+\])"', src))


TOKENS = token_inventory()


def annotate(text: str, script: str = "s") -> str:
    """Traditional line -> CosyVoice 3 input: pinyin tokens where our reading differs from the plain
    (mainland) reading or the character is in ALWAYS_HINT; everything else as characters, converted to
    Simplified by default (the LM was trained mostly on Simplified text)."""
    out = []
    for ch, py, forced, base in tw_text.readings(tw_text.clean(text)):
        if py and ((forced and py != base) or ch in ALWAYS_HINT):
            ini, fin = tw_text.to_marked(py)
            toks = ([f"[{ini}]"] if ini else []) + [f"[{fin}]"]
            if all(t in TOKENS for t in toks):
                out.append("".join(toks))
                continue
        if script == "s" and ch == "著" and py in ("zhe5", "zhao2", "zhuo2"):
            out.append("着")                     # OpenCC keeps 著; Simplified text writes the particle as 着
            continue
        out.append(vt.t2s(ch) if script == "s" else ch)
    return "".join(out)


def main():
    jobs = json.load(open(sys.argv[1], encoding="utf-8"))
    t0 = time.time()
    cosy = AutoModel(model_dir=MODEL_DIR)
    sr = cosy.sample_rate
    print(json.dumps(dict(event="loaded", seconds=round(time.time() - t0, 1), threads=torch.get_num_threads(), sample_rate=sr)), flush=True)
    for j in jobs:
        script = j.get("script", "s")
        text = annotate(j["text"], script)
        ref_text = PREFIX + (vt.t2s(tw_text.clean(j["ref_text"])) if script == "s" else tw_text.clean(j["ref_text"]))
        seed = int(j.get("seed", 1))
        torch.manual_seed(seed); np.random.seed(seed); random.seed(seed)
        t1 = time.time()
        parts = []
        with torch.inference_mode():
            for o in cosy.inference_zero_shot(text, ref_text, j["ref_wav"], stream=False, speed=float(j.get("speed", 1.0)), text_frontend=False):
                parts.append(o["tts_speech"].squeeze(0).numpy().astype(np.float32))
        x = np.concatenate(parts) if parts else np.zeros(1, np.float32)
        el = time.time() - t1
        os.makedirs(os.path.dirname(j["out"]), exist_ok=True)
        sf.write(j["out"], x, sr, subtype="FLOAT")
        dur = len(x) / sr
        print(json.dumps(dict(out=j["out"], tts_text=text, seconds=round(dur, 2), compute=round(el, 2),
                              rtf=round(el / max(dur, 1e-6), 3), seed=seed), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
