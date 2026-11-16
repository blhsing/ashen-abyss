"""Signal processing for the character voices (numpy / scipy only, mono float arrays).

Mirrors the intent of Y:\\tools\\voice\\src\\Dsp.cs + Fx.cs (the C# Kokoro fallback tool): duration-
preserving pitch/formant shift, layers, growl, ring-mod, comb, chorus, EQ, saturation, compression,
reverb send, then trim / -17 LUFS / true-peak limit / 60 ms padding. Human characters get light
settings only; the heavy colour is reserved for bosses (see profiles.py).
"""
from __future__ import annotations

import math

import numpy as np
from scipy import signal
from scipy.ndimage import maximum_filter1d, minimum_filter1d, uniform_filter1d

OUT_RATE = 44100
TARGET_LUFS = -17.0
CEILING_DB = -1.5       # limiter ceiling (sample peak); leaves room for inter-sample / codec overshoot
MAX_TRUE_PEAK_DB = -1.0
EDGE_SILENCE = 0.060


def lin(db: float) -> float:
    return 10.0 ** (db / 20.0)


def db(x: float) -> float:
    return 20.0 * math.log10(max(x, 1e-12))


def rms(x: np.ndarray) -> float:
    return float(np.sqrt(np.mean(np.square(x, dtype=np.float64)))) if len(x) else 0.0


def peak_db(x: np.ndarray) -> float:
    return db(float(np.max(np.abs(x)))) if len(x) else -120.0


def true_peak_db(x: np.ndarray) -> float:
    if not len(x):
        return -120.0
    return db(float(np.max(np.abs(signal.resample_poly(x.astype(np.float64), 4, 1)))))


def resample(x: np.ndarray, sr_in: int, sr_out: int) -> np.ndarray:
    if sr_in == sr_out:
        return x.astype(np.float32)
    g = math.gcd(int(sr_in), int(sr_out))
    return signal.resample_poly(x.astype(np.float64), sr_out // g, sr_in // g).astype(np.float32)


# ------------------------------------------------------------------ time / pitch

def time_stretch(x: np.ndarray, factor: float, sr: int) -> np.ndarray:
    """WSOLA. factor > 1 makes the signal longer; pitch is unchanged."""
    if abs(factor - 1.0) < 1e-4 or len(x) < sr // 20:
        return x.astype(np.float32).copy()
    x = x.astype(np.float32)
    win = int(sr * 0.040) // 2 * 2          # 40 ms frames
    hop_out = win // 2
    hop_in = hop_out / factor
    search = int(sr * 0.012)
    w = np.hanning(win + 1)[:win].astype(np.float32)
    n_frames = int((len(x) - win - search) / hop_in)
    if n_frames < 2:
        return x.copy()
    out = np.zeros(n_frames * hop_out + win, dtype=np.float32)
    norm = np.zeros_like(out)
    pos = 0                                  # where the previous frame was actually taken from
    out[:win] += x[:win] * w
    norm[:win] += w
    for k in range(1, n_frames):
        target = int(round(k * hop_in))
        # the natural continuation of the previous frame, used as the similarity template
        nat = pos + hop_out
        lo = max(0, target - search)
        hi = min(len(x) - win, target + search)
        if hi <= lo or nat + win > len(x):
            best = min(max(target, 0), len(x) - win)
        else:
            tmpl = x[nat:nat + win]
            seg = x[lo:hi + win]
            corr = signal.correlate(seg, tmpl, mode="valid", method="fft")
            energy = np.sqrt(np.maximum(uniform_filter1d(seg.astype(np.float64) ** 2, win, origin=-(win // 2))[:len(corr)] * win, 1e-9))
            best = lo + int(np.argmax(corr / energy))
        o = k * hop_out
        out[o:o + win] += x[best:best + win] * w
        norm[o:o + win] += w
        pos = best
    out /= np.maximum(norm, 1e-3)
    return out


def pitch_shift(x: np.ndarray, semitones: float, sr: int) -> np.ndarray:
    """Pitch shift that keeps the duration; formants move with the pitch (bigger / smaller body)."""
    if abs(semitones) < 0.01:
        return x.astype(np.float32).copy()
    ratio = 2.0 ** (semitones / 12.0)
    stretched = time_stretch(x, ratio, sr)                     # ratio < 1: shorter, then slowed down
    n_out = len(x)
    from fractions import Fraction
    fr = Fraction(1.0 / ratio).limit_denominator(240)          # pitch exact to ~0.01 semitone, short filter
    y = signal.resample_poly(stretched.astype(np.float64), fr.numerator, fr.denominator)
    y = y[:n_out] if len(y) >= n_out else np.pad(y, (0, n_out - len(y)))
    return y.astype(np.float32)


def speed(x: np.ndarray, rate: float, sr: int) -> np.ndarray:
    """Tempo change without pitch change. rate < 1 = slower."""
    return time_stretch(x, 1.0 / rate, sr)


def _cepstral_envelope(mag: np.ndarray, sr: int, nfft: int, cutoff_ms: float = 1.8) -> np.ndarray:
    """Log spectral envelope per frame (columns): low-quefrency part of the cepstrum, i.e. the vocal-tract
    shape without the harmonics. cutoff_ms must stay below the shortest pitch period (3 ms = 330 Hz)."""
    logm = np.log(np.maximum(mag, 1e-7))
    cep = np.fft.irfft(logm, n=nfft, axis=0)
    k = max(8, int(cutoff_ms * 0.001 * sr))
    lifter = np.zeros(nfft)
    lifter[:k] = 1.0
    lifter[-k + 1:] = 1.0
    return np.fft.rfft(cep * lifter[:, None], n=nfft, axis=0).real


def pitch_shift_fp(x: np.ndarray, semitones: float, sr: int) -> np.ndarray:
    """Pitch shift that keeps duration AND formants: the voice gets lower, the mouth stays the same size,
    so vowels keep their colour and the consonants their place. pitch_shift() moves everything; here the
    spectral envelope of every frame is put back to the original's (correction limited to +-18 dB)."""
    if abs(semitones) < 0.01:
        return x.astype(np.float32).copy()
    y = pitch_shift(x, semitones, sr)
    nfft = 2048 if sr >= 32000 else 1024
    hop = nfft // 4
    _, _, zx = signal.stft(x.astype(np.float64), sr, nperseg=nfft, noverlap=nfft - hop)
    _, _, zy = signal.stft(y.astype(np.float64), sr, nperseg=nfft, noverlap=nfft - hop)
    n = min(zx.shape[1], zy.shape[1])
    zx, zy = zx[:, :n], zy[:, :n]
    gain = _cepstral_envelope(np.abs(zx), sr, nfft) - _cepstral_envelope(np.abs(zy), sr, nfft)
    gain = uniform_filter1d(np.clip(gain, -2.07, 2.07), size=3, axis=1)        # +-18 dB, smoothed over 3 frames
    _, out = signal.istft(zy * np.exp(gain), sr, nperseg=nfft, noverlap=nfft - hop)
    out = out[:len(x)] if len(out) >= len(x) else np.pad(out, (0, len(x) - len(out)))
    return (out * (rms(x) / max(rms(out), 1e-6))).astype(np.float32)


# ------------------------------------------------------------------ EQ

def _biquad(kind: str, hz: float, gain_db: float, q: float, sr: int):
    w0 = 2 * math.pi * min(hz, sr * 0.45) / sr
    cw, sw = math.cos(w0), math.sin(w0)
    a = 10 ** (gain_db / 40.0)
    alpha = sw / (2 * q)
    if kind == "hp":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]; den = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "lp":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]; den = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "peak":
        b = [1 + alpha * a, -2 * cw, 1 - alpha * a]; den = [1 + alpha / a, -2 * cw, 1 - alpha / a]
    elif kind == "lowshelf":
        s = 2 * math.sqrt(a) * alpha
        b = [a * ((a + 1) - (a - 1) * cw + s), 2 * a * ((a - 1) - (a + 1) * cw), a * ((a + 1) - (a - 1) * cw - s)]
        den = [(a + 1) + (a - 1) * cw + s, -2 * ((a - 1) + (a + 1) * cw), (a + 1) + (a - 1) * cw - s]
    elif kind == "highshelf":
        s = 2 * math.sqrt(a) * alpha
        b = [a * ((a + 1) + (a - 1) * cw + s), -2 * a * ((a - 1) + (a + 1) * cw), a * ((a + 1) + (a - 1) * cw - s)]
        den = [(a + 1) - (a - 1) * cw + s, 2 * ((a - 1) - (a + 1) * cw), (a + 1) - (a - 1) * cw - s]
    else:
        raise ValueError(kind)
    return np.array(b) / den[0], np.array(den) / den[0]


def eq(x: np.ndarray, bands, sr: int) -> np.ndarray:
    """bands: list of (kind, hz[, gain_db[, q]]); kinds hp lp peak lowshelf highshelf."""
    y = x.astype(np.float64)
    for band in bands:
        kind, hz = band[0], band[1]
        g = band[2] if len(band) > 2 else 0.0
        q = band[3] if len(band) > 3 else (1.0 if kind == "peak" else 0.707)
        b, a = _biquad(kind, hz, g, q, sr)
        y = signal.lfilter(b, a, y)
    return y.astype(np.float32)


# ------------------------------------------------------------------ colour

def saturate(x: np.ndarray, drive: float) -> np.ndarray:
    if drive <= 0:
        return x
    k = 1.0 + 9.0 * drive
    ref = max(rms(x), 1e-6)
    y = np.tanh(x / (ref * 4.0) * k)
    return (y * (ref / max(rms(y), 1e-6))).astype(np.float32)


def growl(x: np.ndarray, sr: int, hz: float, depth: float, seed: int) -> np.ndarray:
    """Gravel: irregular low-rate amplitude pulses in the roughness band (25-45 Hz)."""
    rng = np.random.default_rng(seed)
    n = len(x)
    t = 0.0
    env = np.ones(n, dtype=np.float32)
    while t < n:
        period = sr / hz * (1.0 + rng.uniform(-0.25, 0.25))
        i0, i1 = int(t), min(n, int(t + period))
        if i1 > i0:
            ph = np.linspace(0, 1, i1 - i0, endpoint=False)
            env[i0:i1] = 1.0 - depth * (0.5 - 0.5 * np.cos(2 * np.pi * ph)) * rng.uniform(0.6, 1.0)
        t += period
    return (x * env).astype(np.float32)


def ring_mod(x: np.ndarray, sr: int, hz: float, mix: float) -> np.ndarray:
    t = np.arange(len(x)) / sr
    return (x * (1 - mix) + x * np.sin(2 * np.pi * hz * t) * mix).astype(np.float32)


def comb(x: np.ndarray, sr: int, ms: float, feedback: float, mix: float) -> np.ndarray:
    """Feedback comb with a damped loop: a resonant metal tube at 1000/ms Hz."""
    d = max(1, int(round(ms * sr / 1000.0)))
    damp = 0.25
    a = np.zeros(d + 2)
    a[0] = 1.0
    a[d] = -feedback * (1 - damp)
    a[d + 1] = -feedback * damp
    wet = signal.lfilter([1.0], a, x.astype(np.float64))
    wet *= rms(x) / max(rms(wet), 1e-6)
    return (x * (1 - mix) + wet * mix).astype(np.float32)


def chorus(x: np.ndarray, sr: int, voices: int, delay_ms: float, depth_ms: float, rate_hz: float, mix: float, seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    n = len(x)
    t = np.arange(n)
    wet = np.zeros(n, dtype=np.float64)
    xp = np.arange(n)
    for v in range(voices):
        ph = rng.uniform(0, 2 * np.pi)
        rate = rate_hz * (1 + 0.17 * v)
        d = (delay_ms + depth_ms * np.sin(2 * np.pi * rate * t / sr + ph)) * sr / 1000.0
        wet += np.interp(t - d, xp, x, left=0.0)
    wet /= max(voices, 1)
    return (x * (1 - mix * 0.5) + wet * mix).astype(np.float32)


def whisper(x: np.ndarray, sr: int, seed: int) -> np.ndarray:
    """Replace the voiced excitation by noise: keep each frame's spectral envelope, randomise phases."""
    rng = np.random.default_rng(seed)
    nfft = 1024
    f, t, z = signal.stft(x.astype(np.float64), sr, nperseg=nfft, noverlap=nfft * 3 // 4)
    mag = np.abs(z)
    smooth = uniform_filter1d(mag, size=max(3, int(260 / (sr / nfft))), axis=0)     # ~260 Hz: harmonics gone
    tilt = np.clip(f / 1500.0, 0.25, 1.6)[:, None]                                  # breath is brighter than voice
    ph = rng.uniform(0, 2 * np.pi, size=z.shape)
    _, y = signal.istft(smooth * tilt * np.exp(1j * ph), sr, nperseg=nfft, noverlap=nfft * 3 // 4)
    y = y[:len(x)] if len(y) >= len(x) else np.pad(y, (0, len(x) - len(y)))
    return (y * (rms(x) / max(rms(y), 1e-6))).astype(np.float32)


def compress(x: np.ndarray, sr: int, threshold_db=-20.0, ratio=2.5, attack_ms=6.0, release_ms=120.0, makeup_db=0.0) -> np.ndarray:
    """Feed-forward compressor on a smoothed peak envelope."""
    env = np.abs(x.astype(np.float64))
    att = math.exp(-1.0 / (attack_ms * 0.001 * sr))
    rel = math.exp(-1.0 / (release_ms * 0.001 * sr))
    # attack/release follower (vectorised in blocks is awkward; a plain loop over ~0.5 M samples is fine)
    out = np.empty_like(env)
    e = 0.0
    for i, v in enumerate(env):
        c = att if v > e else rel
        e = c * e + (1 - c) * v
        out[i] = e
    level = 20 * np.log10(np.maximum(out, 1e-9))
    over = np.maximum(level - threshold_db, 0.0)
    gain_db = -over * (1 - 1 / ratio) + makeup_db
    return (x * 10 ** (gain_db / 20)).astype(np.float32)


def reverb(x: np.ndarray, sr: int, rt60=1.5, pre_delay_ms=20.0, size=1.0, damp_hz=5000.0, low_cut_hz=150.0, high_cut_hz=8000.0, seed=1) -> np.ndarray:
    """Wet signal only (len(x) + tail). Synthetic room: sparse early reflections + exponentially
    decaying noise whose highs die faster (damping); unit-energy impulse response so send levels mean
    the same thing for short and long decays."""
    rng = np.random.default_rng(seed)
    n = int(sr * min(rt60 * 1.3 + 0.2, 12.0))
    t = np.arange(n) / sr
    noise = rng.standard_normal(n)
    # two bands with different decay times: lows keep rt60, highs decay faster
    sos_lo = signal.butter(2, min(damp_hz, sr * 0.45), "lp", fs=sr, output="sos")
    sos_hi = signal.butter(2, min(damp_hz, sr * 0.45), "hp", fs=sr, output="sos")
    lo = signal.sosfilt(sos_lo, noise) * np.exp(-6.908 * t / rt60)
    hi = signal.sosfilt(sos_hi, noise) * np.exp(-6.908 * t / (rt60 * 0.45))
    ir = lo + hi
    build = np.minimum(1.0, t / (0.012 * size + 1e-4)) ** 1.5         # diffuse field builds up after the early part
    ir *= build
    # early reflections
    for k in range(int(6 + 6 * size)):
        d = int((0.004 + rng.uniform(0, 0.045) * size) * sr)
        if d < n:
            ir[d] += rng.choice([-1.0, 1.0]) * (0.9 ** k) * 8.0
    ir = signal.sosfilt(signal.butter(2, low_cut_hz, "hp", fs=sr, output="sos"), ir)
    ir = signal.sosfilt(signal.butter(2, min(high_cut_hz, sr * 0.45), "lp", fs=sr, output="sos"), ir)
    ir /= math.sqrt(float(np.sum(ir ** 2))) + 1e-12
    pre = int(pre_delay_ms * sr / 1000.0)
    ir = np.concatenate([np.zeros(pre), ir])
    return signal.fftconvolve(x.astype(np.float64), ir).astype(np.float32)


# ------------------------------------------------------------------ loudness / limiting / finishing

def lufs(x: np.ndarray, sr: int) -> float:
    """Integrated loudness, ITU-R BS.1770-4 (pyloudnorm); ungated K-weighted level for very short clips."""
    import pyloudnorm as pyln
    x64 = x.astype(np.float64)
    if len(x64) >= int(0.45 * sr):
        try:
            v = pyln.Meter(sr).integrated_loudness(x64)
            if np.isfinite(v):
                return float(v)
        except Exception:
            pass
    # fallback: K-weighting + mean square
    y = signal.lfilter(*_biquad("highshelf", 1500, 4.0, 0.707, sr), x64)
    y = signal.lfilter(*_biquad("hp", 38, 0, 0.5, sr), y)
    return -0.691 + 10 * math.log10(max(float(np.mean(y ** 2)), 1e-12))


def limit(x: np.ndarray, sr: int, ceiling_db: float) -> np.ndarray:
    """Look-ahead brick-wall limiter (5 ms look-ahead, ~60 ms release)."""
    ceil = lin(ceiling_db)
    a = np.abs(x.astype(np.float64))
    need = np.minimum(1.0, ceil / np.maximum(a, 1e-9))
    la = max(1, int(0.005 * sr))
    g = minimum_filter1d(need, size=2 * la + 1)                      # hold the reduction around each peak
    g = minimum_filter1d(uniform_filter1d(g, size=la), size=3)
    # release smoothing
    rel = int(0.060 * sr)
    g = np.minimum(g, uniform_filter1d(g, size=rel))
    g = np.minimum(g, need)
    y = x * g
    return np.clip(y, -ceil, ceil).astype(np.float32)


def active_range(x: np.ndarray, start_db=-46.0, end_db=-52.0, sr=OUT_RATE):
    """(start, end) sample range that carries signal, relative to the peak of a 10 ms envelope."""
    env = maximum_filter1d(np.abs(x), size=max(1, sr // 100))
    pk = float(env.max()) if len(env) else 0.0
    if pk <= 0:
        return 0, 0
    a = np.nonzero(env > pk * lin(start_db))[0]
    b = np.nonzero(env > pk * lin(end_db))[0]
    if not len(a) or not len(b):
        return 0, 0
    return int(a[0]), int(b[-1]) + 1


def finish(x: np.ndarray, target_lufs: float = TARGET_LUFS, sr: int = OUT_RATE) -> np.ndarray:
    """Trim, loudness-normalise, limit, pad with 60 ms of silence."""
    s, e = active_range(x, sr=sr)
    if e <= s:
        return np.zeros(int(2 * EDGE_SILENCE * sr), dtype=np.float32)
    tail_cut = e < len(x) - sr // 50
    s = max(0, s - sr * 4 // 1000)
    body = x[s:e].astype(np.float32).copy()
    fin = min(len(body) // 2, sr * 4 // 1000)
    body[:fin] *= np.linspace(0, 1, fin, dtype=np.float32)
    fout = min(len(body) // 2, sr * (140 if tail_cut else 12) // 1000)
    body[len(body) - fout:] *= np.linspace(1, 0, fout, dtype=np.float32) ** 2
    for i in range(5):
        diff = target_lufs - lufs(body, sr)
        if i > 0 and abs(diff) < 0.1:
            break
        body = limit(body * lin(float(np.clip(diff, -40, 40))), sr, CEILING_DB)
    # guarantee the true-peak requirement even for odd material
    tp = true_peak_db(body)
    if tp > MAX_TRUE_PEAK_DB:
        body = body * lin(MAX_TRUE_PEAK_DB - 0.1 - tp)
    pad = int(round(EDGE_SILENCE * sr))
    return np.concatenate([np.zeros(pad, np.float32), body, np.zeros(pad, np.float32)])


# ------------------------------------------------------------------ the profile chain

def apply(profile: dict, dry: np.ndarray, dry_sr: int, seed: int = 0) -> np.ndarray:
    """Dry model output (any rate) -> processed 44.1 kHz character voice (not yet loudness-finished).

    profile keys (all optional): pitch (formant-preserving; "pitch_formants": "move" for the old kind),
    layers[{pitch,gain_db,delay_ms,whisper,ring,comb,drive,hp,lp,reverb_only}] - parallel copies of the
    dry voice, the place for any harsh colour -, growl(hz,depth), ring(hz,mix), comb(ms,fb,mix),
    chorus{...}, eq[...], drive, comp{...}, reverb{...}.
    """
    p = profile
    x44 = resample(dry, dry_sr, OUT_RATE)
    shift = pitch_shift if p.get("pitch_formants") == "move" else pitch_shift_fp
    voice = shift(x44, p.get("pitch", 0.0), OUT_RATE)
    ref_rms = max(rms(voice), 1e-6)
    layers = p.get("layers", [])
    max_delay = int(max([l.get("delay_ms", 0) for l in layers], default=0) * OUT_RATE / 1000.0) + 1 if layers else 0
    voice = np.pad(voice, (0, max_delay))
    send = np.zeros_like(voice)
    any_send = False
    for k, l in enumerate(layers):
        y = pitch_shift(x44, l.get("pitch", 0.0), OUT_RATE)
        if l.get("whisper"):
            y = whisper(y, OUT_RATE, seed + 31 * (k + 1))
        if l.get("ring"):
            y = ring_mod(y, OUT_RATE, *l["ring"])
        if l.get("comb"):
            y = comb(y, OUT_RATE, *l["comb"])
        if l.get("drive"):
            y = saturate(y, l["drive"])
        bands = []
        if l.get("hp"):
            bands.append(("hp", l["hp"]))
        if l.get("lp"):
            bands.append(("lp", l["lp"]))
        if bands:
            y = eq(y, bands, OUT_RATE)
        g = ref_rms / max(rms(y), 1e-6) * lin(l.get("gain_db", -12.0))
        d = int(l.get("delay_ms", 0) * OUT_RATE / 1000.0)
        dst = send if l.get("reverb_only") else voice
        any_send |= bool(l.get("reverb_only"))
        m = min(len(y), len(dst) - d)
        dst[d:d + m] += y[:m] * g
    if p.get("growl"):
        voice = growl(voice, OUT_RATE, p["growl"][0], p["growl"][1], seed + 7)
    if p.get("ring"):
        voice = ring_mod(voice, OUT_RATE, p["ring"][0], p["ring"][1])
    if p.get("comb"):
        voice = comb(voice, OUT_RATE, *p["comb"])
    if p.get("chorus"):
        voice = chorus(voice, OUT_RATE, seed=seed + 13, **p["chorus"])
    if p.get("eq"):
        voice = eq(voice, p["eq"], OUT_RATE)
    voice = saturate(voice, p.get("drive", 0.0))
    norm = 0.1 / max(rms(voice), 1e-6)                    # about -20 dBFS RMS so every profile reacts the same
    voice = voice * norm
    send = send * norm
    voice = compress(voice, OUT_RATE, **p.get("comp", {}))
    rv = p.get("reverb")
    if not rv:
        return voice.astype(np.float32)
    rv = dict(rv)
    send_db = rv.pop("send_db", -20.0)
    wet = reverb(voice + send if any_send else voice, OUT_RATE, seed=seed + 3, **rv) * lin(send_db)
    wet[:len(voice)] += voice
    return wet.astype(np.float32)
