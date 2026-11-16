"""BreezyVoice (MediaTek Research, Apache-2.0) batch driver - CPU, model loaded once.

    <venvs\\breezy python> synth_breezy.py jobs.json
    jobs = [{"out": wav, "text": zh-TW line, "mode": "zero_shot" | "native" | "dual",
             "ref_wav": path, "ref_text": transcript,            # timbre (and, in zero_shot, also accent)
             "accent_wav": path, "accent_text": transcript,      # dual only: the clip the LM continues from
             "seed": 1, "pause": 0.35, "split": false, "annotate": "safe"}]

BreezyVoice = CosyVoice-300M fine-tuned on Taiwanese Mandarin: a language model writes speech tokens
(content, prosody, ACCENT), a flow-matching decoder renders them in the TIMBRE of a reference clip.
  zero_shot  the LM continues from the reference clip: timbre AND accent follow the clip (official mode).
  native     the LM gets no audio prompt, only the text: it speaks with the phonetics it was trained on
             (Taiwanese Mandarin); the reference clip only gives the timbre (CosyVoice's cross-lingual path).
  dual       the LM continues from accent_wav (e.g. the repo's bundled Taiwanese example), the decoder
             renders in the timbre of ref_wav.
The official repo's classes are used unmodified (src\\BreezyVoice\\single_inference.py). Readings come
from tw_text (pypinyin + Taiwan tables) instead of g2pW, written in BreezyVoice's 字[:ㄅㄆㄇ3] syntax.
"""
import json
import os
import random
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # never depend on the console code page
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cpu_cap  # noqa: E402,F401  (thread and affinity limits; must precede torch)
import stubs  # noqa: E402

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
REPO = os.path.join(ROOT, "src", "BreezyVoice")
MODEL_DIR = os.path.join(ROOT, "models", "breezy")
sys.path[:0] = [REPO, os.path.join(REPO, "third_party", "Matcha-TTS")]
os.chdir(REPO)

# WeTextProcessing needs pynini (no Windows wheel); we bypass the repo's normaliser anyway.
# gdown is imported by a download helper that inference never calls.
stubs.install("tn.chinese.normalizer", "tn.english.normalizer", "gdown")
for _m in ("tn.chinese.normalizer", "tn.english.normalizer"):
    if _m in sys.modules and not hasattr(sys.modules[_m], "Normalizer"):
        sys.modules[_m].Normalizer = stubs.PassThroughNormalizer

import numpy as np  # noqa: E402
import soundfile as sf  # noqa: E402
import torch  # noqa: E402

import single_inference as bv  # noqa: E402  (official script; sets torch threads to 1 on import)
from cosyvoice.utils.file_utils import load_wav  # noqa: E402
from utils.word_utils import always_augment_chars, char2phn, word_to_dataset_frequency  # noqa: E402

import tw_text  # noqa: E402

torch.set_num_threads(cpu_cap.THREADS)
SR = 22050


def annotate(text: str, mode: str = "safe") -> str:
    """Traditional text -> BreezyVoice input with bopomofo hints.

    Table readings (Taiwan readings, polyphone fixes) are hinted only where they differ from the plain reading.
    safe: those + rare characters (training frequency < 500); the model decides the other polyphones.
    auto: safe + readings that are not the character's most common one (the repo's get_bopomofo_rare
          rule, but trusting pypinyin instead of g2pW).
    forced: only table readings.  none: no hints."""
    out = []
    for ch, py, forced, base in tw_text.readings(tw_text.clean(text)):
        if py is None or mode == "none":
            out.append(ch)
            continue
        try:
            bpm = tw_text.to_bopomofo(py)
        except ValueError:
            out.append(ch)
            continue
        freq = word_to_dataset_frequency.get(ch, 0)
        phn = char2phn.get(ch, [])
        hint = forced and py != base                    # hints are used sparingly (README): only where they change something
        if mode in ("auto", "safe") and not hint and freq < 500:
            hint = True                                   # rare character: always spell it out
        if mode == "auto" and not hint and len(phn) >= 2 and bpm != phn[0] and (freq < 10000 or ch in always_augment_chars):
            hint = True                                   # unusual reading of a polyphone
        out.append(f"{ch}[:{bpm}]" if hint else ch)
    return "".join(out)


def trim(x: np.ndarray, sr: int, keep: float = 0.03) -> np.ndarray:
    """Cut leading/trailing silence so the pauses between sentences are ours."""
    env = np.abs(x)
    if not len(env) or env.max() <= 0:
        return x
    idx = np.nonzero(env > env.max() * 0.01)[0]
    a = max(0, idx[0] - int(keep * sr))
    b = min(len(x), idx[-1] + int(keep * sr))
    return x[a:b]


class Prompts:
    """Front-end features of each reference clip, computed once."""

    def __init__(self, cosy):
        self.cosy = cosy
        self.cache = {}

    def get(self, wav: str, text: str) -> dict:
        key = (wav, text)
        if key not in self.cache:
            speech16 = load_wav(wav, 16000)
            # a dummy target text; only the prompt-side fields are kept
            self.cache[key] = self.cosy.frontend.frontend_zero_shot("。", annotate(text, "forced"), speech16)
        return self.cache[key]


def build_input(cosy, prompts: Prompts, sent: str, j: dict) -> dict:
    fe = cosy.frontend
    text, text_len = fe._extract_text_token(sent)
    tim = prompts.get(j["ref_wav"], j["ref_text"])
    mode = j.get("mode", "zero_shot")
    m = dict(text=text, text_len=text_len,
             flow_prompt_speech_token=tim["flow_prompt_speech_token"], flow_prompt_speech_token_len=tim["flow_prompt_speech_token_len"],
             prompt_speech_feat=tim["prompt_speech_feat"], prompt_speech_feat_len=tim["prompt_speech_feat_len"],
             flow_embedding=tim["flow_embedding"])
    if mode == "zero_shot":
        src = tim
    elif mode == "dual":
        src = prompts.get(j["accent_wav"], j["accent_text"])
    else:                                                   # native: no audio prompt for the language model
        m.update(llm_embedding=tim["llm_embedding"])
        return m
    m.update(prompt_text=src["prompt_text"], prompt_text_len=src["prompt_text_len"],
             llm_prompt_speech_token=src["llm_prompt_speech_token"], llm_prompt_speech_token_len=src["llm_prompt_speech_token_len"],
             llm_embedding=src["llm_embedding"])
    return m


def main():
    jobs = json.load(open(sys.argv[1], encoding="utf-8"))
    t0 = time.time()
    cosy = bv.CustomCosyVoice(MODEL_DIR)
    prompts = Prompts(cosy)
    print(json.dumps(dict(event="loaded", seconds=round(time.time() - t0, 1), threads=torch.get_num_threads(), affinity=hex(cpu_cap.MASK))), flush=True)
    for j in jobs:
        text = annotate(j["text"], j.get("annotate", "safe"))
        seed = int(j.get("seed", 1))
        torch.manual_seed(seed); np.random.seed(seed); random.seed(seed)
        t1 = time.time()
        parts = []
        for sent in (tw_text.sentences(text) if j.get("split") else [text]):
            with torch.inference_mode():
                wav = cosy.model.inference(**build_input(cosy, prompts, sent, j))["tts_speech"]
            parts.append(trim(wav.squeeze(0).numpy().astype(np.float32), SR))
        pause = np.zeros(int(float(j.get("pause", 0.35)) * SR), dtype=np.float32)
        x = parts[0] if parts else np.zeros(1, np.float32)
        for p in parts[1:]:
            x = np.concatenate([x, pause, p])
        el = time.time() - t1
        os.makedirs(os.path.dirname(j["out"]), exist_ok=True)
        sf.write(j["out"], x, SR, subtype="FLOAT")
        dur = len(x) / SR
        print(json.dumps(dict(out=j["out"], mode=j.get("mode", "zero_shot"), tts_text=text, seconds=round(dur, 2), compute=round(el, 2),
                              rtf=round(el / max(dur, 1e-6), 3), seed=seed), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
