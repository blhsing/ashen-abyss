"""Objective accent indicators, measured on the dry synthesis with the recogniser's token times.

1. Retroflex index.  Mainland standard Mandarin keeps zh/ch/sh (retroflex, frication noise centred
   low) clearly apart from z/c/s (dental, noise centred high). Taiwanese Mandarin moves the
   retroflexes toward the dentals. We locate the frication of each zh/ch/sh and z/c/s syllable and
   report  R = median centroid(zh ch sh) / median centroid(z c s).  Closer to 1 = more merged.
   The centroid is taken in 2-7.5 kHz only: some models (and the 16 kHz reference clip) carry nothing
   above 8 kHz, and a wider band would make band-limited audio look "merged" for the wrong reason.
   Anchors measured with this code: Kokoro (mainland voices) and the Taiwanese speaker of the example
   clip bundled with BreezyVoice - see calibrate().
2. Tone of 寂 (Taiwan ㄐㄧˊ rising, mainland jì falling): F0 movement inside the syllable, in
   semitones; positive = rising = the Taiwan reading was actually spoken.

Both are indicators, not proofs: token times are coarse (60 ms) and six lines are a small sample.
"""
from __future__ import annotations

import difflib

import numpy as np
from scipy import signal

import tw_text
import vt

SR = 22050          # everything is analysed at one rate
BAND_HI = 7500      # Hz; common band that every model and reference clip actually contains
RETRO = ("zh", "ch", "sh")
DENTAL = ("z", "c", "s")


def _tokens(asr: vt.Asr, x: np.ndarray, sr: int):
    import math
    g = math.gcd(sr, 16000)
    x16 = signal.resample_poly(x.astype(np.float64), 16000 // g, sr // g).astype(np.float32)
    st = asr.rec.create_stream()
    st.accept_waveform(16000, x16)
    asr.rec.decode_stream(st)
    r = st.result
    return list(r.tokens), list(r.timestamps)


def align(text: str, tokens: list[str], times: list[float]):
    """[(char, pinyin, time or None, next_time or None)] for the Han characters of the expected text."""
    exp = [(c, p) for c, p, _f, _b in tw_text.readings(tw_text.clean(text)) if p]
    ek = [vt.sound_keys(c)[0] if vt.sound_keys(c) else c for c, _ in exp]
    hk, ht = [], []
    for tok, t in zip(tokens, times):
        k = vt.sound_keys(tok)
        if k:
            hk.append(k[0]); ht.append(t)
    out = [[c, p, None, None] for c, p in exp]
    sm = difflib.SequenceMatcher(a=ek, b=hk, autojunk=False)
    for blk in sm.get_matching_blocks():
        for i in range(blk.size):
            j = blk.b + i
            out[blk.a + i][2] = ht[j]
            out[blk.a + i][3] = ht[j + 1] if j + 1 < len(ht) else None
    return out


def _initial(py: str) -> str:
    syl, _ = tw_text.split_py(py)
    return syl[:2] if syl[:2] in RETRO else syl[:1]


def frication_centroid(x: np.ndarray, t: float) -> float | None:
    """Spectral centroid (Hz, 2-7.5 kHz) of the noisiest frames around a token time."""
    a, b = int(max(0, t - 0.14) * SR), int(min(len(x) / SR, t + 0.12) * SR)
    seg = x[a:b]
    if len(seg) < 1024:
        return None
    f, _, z = signal.stft(seg, SR, nperseg=512, noverlap=512 - 110)
    p = np.abs(z) ** 2
    tot = p[(f >= 100) & (f <= BAND_HI)].sum(axis=0) + 1e-12
    hf = p[(f >= 2500) & (f <= BAND_HI)].sum(axis=0)
    ratio = hf / tot
    ok = np.nonzero((ratio > 0.6) & (tot > 1e-7))[0]
    if len(ok) < 2:
        return None
    best = ok[np.argsort(hf[ok])[-4:]]
    band = (f >= 2000) & (f <= BAND_HI)
    spec = p[band][:, best].sum(axis=1)
    return float((f[band] * spec).sum() / (spec.sum() + 1e-12))


def f0_track(seg: np.ndarray) -> np.ndarray:
    """F0 per 10 ms frame (Hz, NaN = unvoiced), normalised autocorrelation, 60-400 Hz."""
    n, hop = int(0.04 * SR), int(0.01 * SR)
    lo, hi = SR // 400, SR // 60
    out = []
    for s in range(0, max(1, len(seg) - n), hop):
        w = seg[s:s + n] * np.hanning(n)
        w = w - w.mean()
        e = float((w ** 2).sum())
        if e < 1e-6:
            out.append(np.nan); continue
        ac = signal.correlate(w, w, mode="full")[n - 1:]
        ac = ac / (ac[0] + 1e-12)
        k = lo + int(np.argmax(ac[lo:hi]))
        out.append(SR / k if ac[k] > 0.45 else np.nan)
    return np.array(out)


def tone_movement(x: np.ndarray, t0: float, t1: float | None) -> float | None:
    """Semitone change from the first to the last third of the voiced part of a syllable."""
    end = min(t0 + 0.34, t1 + 0.02 if t1 else t0 + 0.30)
    seg = x[int(max(0, t0 - 0.04) * SR):int(end * SR)]
    f0 = f0_track(seg)
    v = f0[np.isfinite(f0)]
    if len(v) < 6:
        return None
    med = np.median(v)
    v = v[(v > med / 1.6) & (v < med * 1.6)]          # drop octave errors
    if len(v) < 6:
        return None
    k = max(2, len(v) // 3)
    return float(12 * np.log2(np.median(v[-k:]) / np.median(v[:k])))


def probe(asr: vt.Asr, text: str, x: np.ndarray, sr: int) -> dict:
    tokens, times = _tokens(asr, x, sr)
    import math
    g = math.gcd(sr, SR)
    y = signal.resample_poly(x.astype(np.float64), SR // g, sr // g).astype(np.float32)
    retro, dental, ji = [], [], []
    for ch, py, t, t_next in align(text, tokens, times):
        if t is None:
            continue
        ini = _initial(py)
        if ini in RETRO or ini in DENTAL:
            c = frication_centroid(y, t)
            if c:
                (retro if ini in RETRO else dental).append(round(c))
        if ch == "寂":
            m = tone_movement(y, t, t_next)
            if m is not None:
                ji.append(round(m, 1))
    return dict(retro=retro, dental=dental, ji=ji)


def summarise(rows: list[dict]) -> dict:
    retro = [c for r in rows for c in r["retro"]]
    dental = [c for r in rows for c in r["dental"]]
    ji = [m for r in rows for m in r["ji"]]
    out = dict(n_retro=len(retro), n_dental=len(dental), ji=ji)
    if len(retro) >= 5 and len(dental) >= 3:
        out.update(retro_hz=int(np.median(retro)), dental_hz=int(np.median(dental)),
                   retroflex_index=round(float(np.median(retro) / np.median(dental)), 2))
    return out
