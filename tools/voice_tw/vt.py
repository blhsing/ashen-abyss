"""Shared helpers: paths, audio I/O, text comparison (CER), offline ASR (SenseVoice via sherpa-onnx)."""
from __future__ import annotations

import os
import re

import numpy as np
import soundfile as sf

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
PROJECT = "Y:\\"
VO_LINES = r"Y:\game\data\vo_lines.json"
KOKORO_DIR = r"Y:\assets_src\voice\kokoro-multi-lang-v1_1"
SENSEVOICE_DIR = r"Y:\assets_src\voice\sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17"
THREADS = 8


def read_wav(path: str) -> tuple[np.ndarray, int]:
    x, sr = sf.read(path, dtype="float32", always_2d=True)
    return x.mean(axis=1), sr


def write_wav16(path: str, x: np.ndarray, sr: int) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, np.clip(x, -1.0, 1.0), sr, subtype="PCM_16")


def write_float(path: str, x: np.ndarray, sr: int) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, x, sr, subtype="FLOAT")


# ------------------------------------------------------------------ text comparison

_cc_t2s = None
_cc_s2t = None


def t2s(text: str) -> str:
    global _cc_t2s
    if _cc_t2s is None:
        from opencc import OpenCC
        _cc_t2s = OpenCC("t2s")
    return _cc_t2s.convert(text)


def s2t(text: str) -> str:
    global _cc_s2t
    if _cc_s2t is None:
        from opencc import OpenCC
        _cc_s2t = OpenCC("s2t")
    return _cc_s2t.convert(text)


def is_han(c: str) -> bool:
    return "\u3400" <= c <= "\u9fff" or "\uf900" <= c <= "\ufaff" or c == "〇"


def for_compare(text: str) -> str:
    """Simplified script, Han characters and lower-case Latin letters only."""
    s = t2s(re.sub(r"<\|[^|]*\|>", "", text)).replace("著", "着")     # OpenCC keeps 著; the script only has the 着 readings
    return "".join(c.lower() for c in s if is_han(c) or c.isascii() and c.isalpha())


def edit_distance(a, b) -> int:
    prev = list(range(len(b) + 1))
    for i in range(1, len(a) + 1):
        cur = [i] + [0] * len(b)
        for j in range(1, len(b) + 1):
            cur[j] = min(prev[j - 1] + (a[i - 1] != b[j - 1]), prev[j] + 1, cur[j - 1] + 1)
        prev = cur
    return prev[len(b)]


def cer_strict(expected: str, heard: str) -> float:
    a, b = for_compare(expected), for_compare(heard)
    if not a:
        return 0.0 if not b else 1.0
    return edit_distance(a, b) / len(a)


_FOLD = str.maketrans({"v": "u"})


def sound_keys(text: str) -> list[str]:
    """Toneless syllables, with the distinctions a recogniser cannot be blamed for folded away:
    homophones (葛魯姆 vs 格鲁姆) and the n/ng, retroflex/dental pairs that Taiwanese Mandarin merges."""
    from pypinyin import lazy_pinyin
    keys = []
    for syl in lazy_pinyin(for_compare(text), errors="ignore"):
        s = syl.translate(_FOLD)
        s = re.sub(r"^zh", "z", s); s = re.sub(r"^ch", "c", s); s = re.sub(r"^sh", "s", s)
        s = re.sub(r"(in|en)g$", r"\1", s)
        keys.append(s)
    return keys


def cer_sound(expected: str, heard: str) -> float:
    a, b = sound_keys(expected), sound_keys(heard)
    if not a:
        return 0.0 if not b else 1.0
    return edit_distance(a, b) / len(a)


# ------------------------------------------------------------------ ASR

class Asr:
    """SenseVoice (zh/en/ja/ko/yue), int8 ONNX, through sherpa-onnx. Returns text and the language tag."""

    def __init__(self, threads: int = 4):
        import sherpa_onnx
        self.rec = sherpa_onnx.OfflineRecognizer.from_sense_voice(
            model=os.path.join(SENSEVOICE_DIR, "model.int8.onnx"),
            tokens=os.path.join(SENSEVOICE_DIR, "tokens.txt"),
            num_threads=threads, use_itn=False, language="auto")

    def transcribe(self, x: np.ndarray, sr: int) -> dict:
        from scipy import signal
        if sr != 16000:
            import math
            g = math.gcd(sr, 16000)
            x = signal.resample_poly(x.astype(np.float64), 16000 // g, sr // g).astype(np.float32)
        st = self.rec.create_stream()
        st.accept_waveform(16000, x)
        self.rec.decode_stream(st)
        r = st.result
        return dict(text=r.text, lang=getattr(r, "lang", ""), emotion=getattr(r, "emotion", ""), event=getattr(r, "event", ""))


def measure(x: np.ndarray, sr: int) -> dict:
    """Level facts used by the validators."""
    import fx
    return dict(duration=round(len(x) / sr, 3), peak_db=round(fx.peak_db(x), 2), true_peak_db=round(fx.true_peak_db(x), 2),
                lufs=round(fx.lufs(x, sr), 2), clipped=int(np.sum(np.abs(x) >= 0.999)),
                nan=bool(not np.all(np.isfinite(x))))


# ------------------------------------------------------------------ production check (hint-aware)

def _fold(syl: str) -> str:
    s = syl.translate(_FOLD)
    s = re.sub(r"^zh", "z", s); s = re.sub(r"^ch", "c", s); s = re.sub(r"^sh", "s", s)
    return re.sub(r"(in|en)g$", r"\1", s)


def expected_keys(text: str, levels=None) -> list[set]:
    """Acceptable toneless syllables for every Han character of a line: the dictionary reading in
    context and, where we gave a hint, the hinted reading (a recogniser hearing ㄏㄢˋ for 和 writes 漢,
    not 和 - that is correct speech, not an error)."""
    import tw_text
    from pypinyin import lazy_pinyin
    hs = tw_text.hints(text, levels if levels is not None else tw_text.LEVELS_ALL)
    chars = [(c, p) for c, p in hs if is_han(c)]
    base = lazy_pinyin(t2s("".join(c for c, _ in chars)).replace("著", "着"), errors="ignore")
    if len(base) != len(chars):
        base = [lazy_pinyin(t2s(c).replace("著", "着"))[0] for c, _ in chars]
    out = []
    for (c, p), b in zip(chars, base):
        keys = {_fold(b)}
        if p:
            k = _fold(tw_text.split_py(p)[0])
            keys.add(k)
            if k[:1] in "jqx":                      # measured: 別{xi2} and even 別習 are written 別急 by the recogniser,
                keys.update(i + k[1:] for i in "jqx")   # while the unhinted 別熄 (ㄒㄧ) is written correctly
        else:
            # An unhinted polyphone may be heard in any of its dictionary readings: the recogniser writes a
            # character, and which reading that character stands for cannot be recovered from the text
            # (一座接一座地暗了 came back as 一座接一座的暗了 - correct speech, "de", but 地 looks like "di").
            keys |= _readings(c)
        out.append(keys)
    return out


_READINGS: dict = {}


def _readings(c: str) -> set:
    if c not in _READINGS:
        from pypinyin import Style, pinyin
        r = pinyin(t2s(c).replace("著", "着"), style=Style.NORMAL, heteronym=True, errors="ignore")
        _READINGS[c] = {_fold(x) for x in (r[0] if r else []) if x}
    return _READINGS[c]


def cer_hint(text: str, heard: str, levels=None) -> float:
    """Character error rate on sounds: homophones equal, zh/z ch/c sh/s and -n/-ng folded (Taiwanese
    Mandarin merges them), hinted readings accepted. This is the pass/fail measure of the production run."""
    from pypinyin import lazy_pinyin
    exp = expected_keys(text, levels)
    got = [_fold(s) for s in lazy_pinyin(for_compare(heard), errors="ignore")]
    if not exp:
        return 0.0 if not got else 1.0
    prev = list(range(len(got) + 1))
    for i in range(1, len(exp) + 1):
        cur = [i] + [0] * len(got)
        for j in range(1, len(got) + 1):
            cur[j] = min(prev[j - 1] + (got[j - 1] not in exp[i - 1]), prev[j] + 1, cur[j - 1] + 1)
        prev = cur
    return prev[len(got)] / len(exp)


# ------------------------------------------------------------------ pitch and cleanliness of a voice

def voice_stats(x: np.ndarray, sr: int) -> dict:
    """Median pitch and harmonics-to-noise ratio of the voiced frames (higher HNR = cleaner, less rasp).
    Autocorrelation method with the window's own autocorrelation divided out (Boersma 1993); without that
    correction short lags win and low voices are reported an octave too high."""
    from scipy import signal
    y = signal.resample_poly(x.astype(np.float64), 1, sr // 16000) if sr % 16000 == 0 else x.astype(np.float64)
    ysr = 16000 if sr % 16000 == 0 else sr
    n, hop, lo, hi = int(0.06 * ysr), int(0.01 * ysr), ysr // 400, ysr // 55
    win = np.hanning(n)
    wac = signal.correlate(win, win, mode="full")[n - 1:]
    wac = wac / wac[0]
    cost = 0.04 * np.arange(hi - lo) / (hi - lo)                 # between equal peaks prefer the shorter period
    peak = float(np.abs(y).max()) or 1.0
    f0s, hnr = [], []
    for s in range(0, max(1, len(y) - n), hop):
        w = y[s:s + n]
        if float(np.abs(w).max()) < 0.03 * peak:
            continue
        w = (w - w.mean()) * win
        ac = signal.correlate(w, w, mode="full")[n - 1:]
        if ac[0] <= 0:
            continue
        r = (ac[lo:hi] / ac[0]) / wac[lo:hi]
        k = int(np.argmax(r - cost))
        rk = float(min(r[k], 0.999))
        if rk > 0.5:
            f0s.append(ysr / (lo + k))
            hnr.append(10 * np.log10(rk / (1 - rk)))
    if len(f0s) < 5:
        return dict(f0=None, hnr=None)
    return dict(f0=round(float(np.median(f0s))), hnr=round(float(np.median(hnr)), 1))
