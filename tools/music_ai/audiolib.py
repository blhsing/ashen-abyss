"""Audio measurement, loop construction and mastering for the Ashen Abyss music pipeline.

Nobody listens during a batch run, so every decision here is made from measurements:
  measure()      silence, clipping, DC, dropouts, spectral holes / resonances, tempo, pulse clarity, length
  find_loop()    picks a loop window on tracked bar lines whose tail matches its head (rhythm, harmony, level)
  build_loop()   equal-power crossfade of the tail into the head (correlation-compensated)
  master()       25 Hz high-pass, light bus compression, loudness normalisation, look-ahead limiter, 44.1 kHz
  seam_metrics() residual discontinuity at the loop point (also run on the decoded OGG)
"""
import numpy as np
import scipy.signal as sps
import scipy.ndimage as ndi
import soundfile as sf
from numba import njit

EPS = 1e-12


# ----------------------------------------------------------------------------------------------------- I/O
def read_audio(path):
    x, sr = sf.read(path, dtype="float64", always_2d=True)
    if x.shape[1] == 1:
        x = np.repeat(x, 2, axis=1)
    return x[:, :2], sr


def write_flac(path, x, sr):
    sf.write(path, np.clip(x, -1.0, 1.0), sr, format="FLAC", subtype="PCM_24")


def write_ogg(path, x, sr=44100, quality=0.6):
    """OGG Vorbis at the given quality (libsndfile: quality = 1 - compression_level). Written in blocks because
    libsndfile's Vorbis encoder can overflow its stack when handed minutes of audio in one call."""
    x = np.clip(x, -1.0, 1.0).astype(np.float32)
    with sf.SoundFile(path, "w", samplerate=sr, channels=x.shape[1], format="OGG", subtype="VORBIS",
                      compression_level=1.0 - quality) as f:
        for i in range(0, len(x), sr):
            f.write(x[i:i + sr])


def db(v):
    return 20.0 * np.log10(np.maximum(v, EPS))


def lufs(x, sr):
    import pyloudnorm
    if len(x) < sr:      # BS.1770 needs at least one 400 ms block
        return float("-inf")
    return float(pyloudnorm.Meter(sr).integrated_loudness(x))


def true_peak_db(x, sr):
    up = sps.resample_poly(x, 4, 1, axis=0)
    return float(db(np.max(np.abs(up))))


# ------------------------------------------------------------------------------------------------ analysis
def _frame_rms_db(mono, sr, win_s, hop_s):
    win = max(1, int(win_s * sr))
    hop = max(1, int(hop_s * sr))
    p = ndi.uniform_filter1d(mono * mono, size=win, mode="nearest")
    return 10.0 * np.log10(np.maximum(p[::hop], EPS * EPS)), hop / sr


def _dropouts(x, sr):
    """Abrupt holes: the level falls by 30 dB or more within 30 ms and comes back within 0.8 s."""
    mono = np.max(np.abs(x), axis=1)
    lev, hop = _frame_rms_db(mono, sr, 0.020, 0.010)
    n = len(lev)
    ctx = ndi.median_filter(lev, size=51, mode="nearest", origin=25)     # median of the previous 0.5 s
    events = []
    i = int(2.0 / hop)
    end = n - int(2.0 / hop)
    while i < end:
        if ctx[i] > -45.0 and lev[i] < ctx[i] - 30.0 and lev[i - 3] > ctx[i] - 12.0:
            back = np.nonzero(lev[i:i + int(0.8 / hop)] > ctx[i] - 10.0)[0]
            if len(back):
                events.append(round(i * hop, 2))
                i += int(back[0]) + 5
                continue
        i += 1
    # runs of exact digital zero longer than 5 ms inside the piece
    z = np.all(x == 0.0, axis=1)
    zero_runs = 0
    if z.any():
        edges = np.flatnonzero(np.diff(np.concatenate(([0], z.view(np.int8), [0]))))
        for a, b in zip(edges[::2], edges[1::2]):
            if (b - a) > 0.005 * sr and a > sr and b < len(x) - sr:
                zero_runs += 1
    return events, zero_runs


def _spectrum(x, sr):
    mono = x.mean(axis=1)
    f, pxx = sps.welch(mono, sr, nperseg=16384, noverlap=8192)
    pdb = 10.0 * np.log10(np.maximum(pxx, 1e-30))
    # third-octave band levels
    centres = 1000.0 * 2.0 ** (np.arange(-14, 13) / 3.0)        # 39 Hz .. 16 kHz
    centres = centres[centres < sr / 2.3]
    bands = []
    for c in centres:
        m = (f >= c / 2 ** (1 / 6)) & (f < c * 2 ** (1 / 6))
        bands.append(10.0 * np.log10(np.maximum(pxx[m].sum(), 1e-30)) if m.any() else -300.0)
    bands = np.array(bands)
    holes = []
    for i in range(2, len(centres) - 2):
        if 200.0 <= centres[i] <= 8000.0:
            neigh = np.mean([bands[i - 2], bands[i - 1], bands[i + 1], bands[i + 2]])
            if bands[i] < neigh - 18.0:
                holes.append(int(round(centres[i])))
    # narrow resonances: 1/12-octave smoothed spectrum against a 1-octave smoothed one, 2-12 kHz
    lf = np.log2(np.maximum(f[1:], 1.0))
    grid = np.arange(np.log2(1000.0), np.log2(min(14000.0, sr / 2.2)), 1.0 / 48.0)
    g = np.interp(grid, lf, pdb[1:])
    narrow = ndi.uniform_filter1d(g, size=4, mode="nearest")
    wide = ndi.uniform_filter1d(g, size=48, mode="nearest")
    sel = grid >= np.log2(2000.0)
    excess = narrow[sel] - wide[sel]
    k = int(np.argmax(excess))
    cum = np.cumsum(pxx)
    roll = float(f[np.searchsorted(cum, 0.99 * cum[-1])])
    cen = float((f * pxx).sum() / max(pxx.sum(), 1e-30))
    top = bands.max()
    above = [c for c, b in zip(centres, bands) if b > top - 60.0]
    return dict(holes_hz=holes, resonance_db=round(float(excess[k]), 1), resonance_hz=int(2 ** grid[sel][k]),
                rolloff99_hz=int(roll), centroid_hz=int(cen), bandwidth_hz=int(max(above)) if above else 0)


def onset_envelope(x, sr, hop=256, sr_a=22050):
    import librosa
    mono = librosa.resample(x.mean(axis=1).astype(np.float32), orig_sr=sr, target_sr=sr_a)
    env = librosa.onset.onset_strength(y=mono, sr=sr_a, hop_length=hop)
    return mono, env, sr_a / hop


def tempo_near(env, fps, bpm, tol=0.12):
    """Tempo estimate from the onset autocorrelation. Returns (best tempo within +-tol of bpm, its normalised
    autocorrelation, global best tempo in 40-240 BPM, its normalised autocorrelation)."""
    e = env - env.mean()
    n = len(e)
    nfft = 1 << int(np.ceil(np.log2(2 * n)))
    ac = np.fft.irfft(np.abs(np.fft.rfft(e, nfft)) ** 2)[:n]
    ac = ac / max(ac[0], EPS)
    ac = ac / (1.0 - np.arange(n) / n).clip(0.2)             # unbias
    lags = np.arange(n)

    def best(lo_bpm, hi_bpm):
        lo = max(1, int(np.floor(fps * 60.0 / hi_bpm)))
        hi = min(n - 2, int(np.ceil(fps * 60.0 / lo_bpm)))
        if hi <= lo:
            return float(bpm), 0.0
        k = lo + int(np.argmax(ac[lo:hi + 1]))
        # parabolic refinement
        if 1 <= k < n - 1:
            a, b, c = ac[k - 1], ac[k], ac[k + 1]
            d = 0.5 * (a - c) / (a - 2 * b + c) if abs(a - 2 * b + c) > 1e-12 else 0.0
        else:
            d = 0.0
        return float(60.0 * fps / (k + d)), float(ac[k])

    t_near, s_near = best(bpm * (1 - tol), bpm * (1 + tol))
    t_glob, s_glob = best(40.0, 240.0)
    return t_near, s_near, t_glob, s_glob


def fold_tempo(est, target):
    """Bring est into the octave around target (x2 / x0.5) and return (folded, relative error)."""
    if est <= 0:
        return 0.0, 1.0
    r = est / target
    while r > 1.5:
        r /= 2.0
    while r < 0.75:
        r *= 2.0
    return target * r, abs(r - 1.0)


_KS_MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
_KS_MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])
_NOTES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
_FLAT = {"Db": "C#", "Eb": "D#", "Gb": "F#", "Ab": "G#", "Bb": "A#"}


def estimate_key(m22):
    """Tonal centre from the average chroma (Krumhansl-Schmuckler profiles) plus the strongest pitch class."""
    import librosa
    c = librosa.feature.chroma_cqt(y=m22, sr=22050, hop_length=2048).mean(axis=1)
    best = (-2.0, 0, "minor")
    for k in range(12):
        for prof, mode in ((_KS_MAJOR, "major"), (_KS_MINOR, "minor")):
            r = float(np.corrcoef(np.roll(prof, k), c)[0, 1])
            if r > best[0]:
                best = (r, k, mode)
    return dict(key_est="%s %s" % (_NOTES[best[1]], best[2]), key_conf=round(best[0], 3), key_pc=int(best[1]),
                top_pc=_NOTES[int(np.argmax(c))])


def key_pc(name):
    """Pitch class (0-11) of a key name such as 'Bb minor'."""
    note = name.split()[0]
    return _NOTES.index(_FLAT.get(note, note))


def measure(x, sr, bpm=None, pulse=False, want_s=None):
    """All signal checks for one candidate. Returns a flat dict of numbers plus 'reject' (list of reasons)."""
    import librosa
    n = len(x)
    dur = n / sr
    mono = x.mean(axis=1)
    peak = float(np.max(np.abs(x))) if n else 0.0
    out = dict(seconds=round(dur, 2), peak_db=round(float(db(peak)), 2),
               rms_db=round(float(db(np.sqrt(np.mean(x * x)))), 2),
               dc=round(float(np.max(np.abs(x.mean(axis=0)))), 6),
               clip_frac=float(np.mean(np.max(np.abs(x), axis=1) >= 0.999)),
               lufs=round(lufs(x, sr), 2),
               nan=bool(not np.isfinite(x).all()))
    lev1, _ = _frame_rms_db(mono, sr, 1.0, 0.5)
    out["silent_frac"] = round(float(np.mean(lev1 < -60.0)), 3)
    out["lra_db"] = round(float(np.percentile(lev1, 95) - np.percentile(lev1, 10)), 1)
    ev, zero_runs = _dropouts(x, sr)
    out["dropouts"] = len(ev)
    out["dropout_times"] = ev[:8]
    out["zero_runs"] = zero_runs
    out.update(_spectrum(x, sr))
    side = (x[:, 0] - x[:, 1]) * 0.5
    out["stereo_width"] = round(float(np.sqrt(np.mean(side * side)) / max(np.sqrt(np.mean(mono * mono)), EPS)), 3)
    # rhythm
    m22, env, fps = onset_envelope(x, sr)
    t_near, s_near, t_glob, s_glob = tempo_near(env, fps, bpm or 100.0)
    folded, err = fold_tempo(t_glob, bpm or t_glob)
    if bpm and s_near >= 0.6 * s_glob:          # a real periodicity sits near the requested tempo
        err = min(err, abs(t_near / bpm - 1.0))
    out.update(tempo_near=round(t_near, 2), pulse_near=round(s_near, 3), tempo_global=round(t_glob, 2),
               pulse_global=round(s_glob, 3), tempo_err=round(err, 4),
               tempo=round(t_near if (bpm and s_near >= 0.6 * s_glob) else folded, 2))
    out.update(estimate_key(m22))
    seg = m22[int(len(m22) * 0.25):int(len(m22) * 0.25) + 22050 * 60]
    if len(seg) > 22050 * 5:
        h, p = librosa.effects.hpss(seg)
        out["perc_ratio"] = round(float(np.sum(p * p) / max(np.sum(h * h) + np.sum(p * p), EPS)), 3)
    else:
        out["perc_ratio"] = 0.0
    # hard rejects
    rej = []
    if out["nan"]:
        rej.append("NaN/Inf samples")
    if out["lufs"] < -45.0 or out["silent_frac"] > 0.25:
        rej.append("mostly silent")
    if out["clip_frac"] > 1e-4:
        rej.append("clipping")
    if out["dc"] > 0.01:
        rej.append("DC offset")
    if out["dropouts"] > 0 or out["zero_runs"] > 0:
        rej.append("abrupt dropout")
    if out["holes_hz"]:
        rej.append("spectral hole")
    if out["resonance_db"] > 20.0:
        rej.append("harsh resonance")
    if out["bandwidth_hz"] < 6000:
        rej.append("band-limited (dull)")
    if want_s and abs(dur - want_s) > max(2.0, 0.03 * want_s):
        rej.append("wrong length")
    if pulse and bpm:
        if out["tempo_err"] > 0.06:
            rej.append("wrong tempo")
        if max(out["pulse_near"], out["pulse_global"]) < 0.08:
            rej.append("no clear pulse")
    out["reject"] = rej
    return out


# -------------------------------------------------------------------------------------------- loop building
def _usable_span(x, sr):
    """Start/end (seconds) of the body: drops a fade-in and the final ring-out / fade of the generated piece."""
    mono = x.mean(axis=1)
    lev, hop = _frame_rms_db(mono, sr, 1.0, 0.1)
    n = len(lev)
    body = np.median(lev[int(n * 0.2):int(n * 0.8)])
    ok = np.nonzero(lev > body - 10.0)[0]
    if len(ok) == 0:
        return 0.0, len(x) / sr
    return float(ok[0] * hop), float(min(len(x) / sr, ok[-1] * hop + 0.5))


def trim_silence(x, sr, floor_db=-50.0):
    """Start/end (seconds) after stripping leading and trailing passages more than |floor_db| below the body."""
    lev, hop = _frame_rms_db(x.mean(axis=1), sr, 0.2, 0.05)
    ok = np.nonzero(lev > np.median(lev) + floor_db + 20.0)[0] if len(lev) else []
    if len(ok) == 0:
        return 0.0, len(x) / sr
    return float(max(0.0, ok[0] * hop - 0.1)), float(min(len(x) / sr, ok[-1] * hop + 0.3))


def refine_period(env, fps, period0):
    """Beat period (seconds) refined step by step on longer and longer autocorrelation lags (8, 32, 128 beats)."""
    e = env - env.mean()
    n = len(e)
    nfft = 1 << int(np.ceil(np.log2(2 * n)))
    ac = np.fft.irfft(np.abs(np.fft.rfft(e, nfft)) ** 2)[:n]
    ac = ac / (1.0 - np.arange(n) / n).clip(0.2)
    period = period0
    for m in (8, 32, 128):
        centre = m * period * fps
        half = 0.4 * period * fps
        lo, hi = int(np.floor(centre - half)), int(np.ceil(centre + half))
        if hi >= int(0.6 * n) or lo < 1:
            break
        k = lo + int(np.argmax(ac[lo:hi + 1]))
        y0, y1, y2 = ac[k - 1], ac[k], ac[k + 1]
        den = y0 - 2 * y1 + y2
        d = 0.5 * (y0 - y2) / den if abs(den) > 1e-12 else 0.0
        period = (k + float(np.clip(d, -0.5, 0.5))) / fps / m
    return float(period)


def _refine_length(env, fps, t0, length, xf, max_shift_s):
    """Loop length near `length` (within max_shift_s) at which the onsets after the loop end line up best with the
    onsets at the loop start."""
    i0, n = int(round(t0 * fps)), int(xf * fps)
    e1 = env[i0:i0 + n]
    if len(e1) < n:
        return float(length)
    e1 = e1 - e1.mean()
    k0, span = int(round(length * fps)), max(1, int(round(max_shift_s * fps)))
    vals = []
    for d in range(-span, span + 1):
        e2 = env[i0 + k0 + d:i0 + k0 + d + n]
        if len(e2) < n:
            vals.append(-1.0)
            continue
        e2 = e2 - e2.mean()
        vals.append(float(np.dot(e1, e2) / max(np.linalg.norm(e1) * np.linalg.norm(e2), EPS)))
    j = int(np.argmax(vals))
    frac = 0.0
    if 0 < j < len(vals) - 1:
        y0, y1, y2 = vals[j - 1], vals[j], vals[j + 1]
        den = y0 - 2 * y1 + y2
        frac = float(np.clip(0.5 * (y0 - y2) / den, -0.5, 0.5)) if abs(den) > 1e-12 else 0.0
    return float((k0 + j - span + frac) / fps)


def find_loop(x, sr, bpm, ts, loop_s, pulse):
    """Choose (start, length, crossfade) in seconds. Length is a whole number of tracked bars where the piece
    has a pulse; the tail that is blended into the head is the best match in rhythm, harmony and level."""
    import librosa
    lo_s, hi_s = loop_s
    dur = len(x) / sr
    a, b = _usable_span(x, sr)
    m22, env, fps = onset_envelope(x, sr)
    t_near, s_near, t_glob, s_glob = tempo_near(env, fps, bpm)
    tempo = t_near if s_near >= 0.5 * s_glob else fold_tempo(t_glob, bpm)[0]
    beat = 60.0 / tempo
    bar = ts * beat
    # "Free-time" pieces often come out with a steady pulse anyway (a fingerpicked pattern, a slow ostinato):
    # whenever the onset pattern clearly repeats at the beat period the loop is cut on whole bars as well.
    metric = bool(pulse or (s_near >= 0.5 and s_near >= 0.5 * s_glob))
    if metric:
        xf = 2 * bar if 3.0 <= 2 * bar <= 6.0 else (bar if 3.0 <= bar <= 6.0 else 4.0)
    else:
        xf = 6.0
    # shrink the demands if the usable body is short
    hi_s = min(hi_s, b - a - xf)
    lo_s = min(lo_s, hi_s)
    chroma = librosa.feature.chroma_cqt(y=m22, sr=22050, hop_length=512)
    cfps = 22050 / 512.0
    lev, lhop = _frame_rms_db(x.mean(axis=1), sr, 0.5, 0.05)

    def seg_feat(t):
        i0, i1 = int(t * fps), int((t + xf) * fps)
        c0, c1 = int(t * cfps), int((t + xf) * cfps)
        l0, l1 = int(t / lhop), int((t + xf) / lhop)
        return env[i0:i1], chroma[:, c0:c1], lev[l0:l1]

    def score(t0, length):
        e1, c1, l1 = seg_feat(t0)
        e2, c2, l2 = seg_feat(t0 + length)
        n = min(len(e1), len(e2))
        if n < 8:
            return -1.0, {}
        e1, e2 = e1[:n] - e1[:n].mean(), e2[:n] - e2[:n].mean()
        r_on = float(np.dot(e1, e2) / max(np.linalg.norm(e1) * np.linalg.norm(e2), EPS))
        m = min(c1.shape[1], c2.shape[1])
        num = np.sum(c1[:, :m] * c2[:, :m], axis=0)
        den = np.linalg.norm(c1[:, :m], axis=0) * np.linalg.norm(c2[:, :m], axis=0)
        r_ch = float(np.mean(num / np.maximum(den, EPS)))
        k = min(len(l1), len(l2))
        d_lev = float(np.mean(np.abs(l1[:k] - l2[:k])))
        s_lev = 1.0 - min(1.0, d_lev / 12.0)
        w = (0.5, 0.3, 0.2) if metric else (0.15, 0.5, 0.35)
        return w[0] * max(r_on, 0.0) + w[1] * r_ch + w[2] * s_lev, dict(onset=round(r_on, 3), chroma=round(r_ch, 3),
                                                                      level_diff_db=round(d_lev, 2))

    cands = []
    if metric:
        # The model keeps a steady tempo, so the loop is a whole number of bars of the *measured* beat period.
        # The period is refined on long autocorrelation lags (a 0.2 % tempo error would be half a beat over 60 bars).
        period = refine_period(env, fps, beat)
        tempo = 60.0 / period
        beat, bar = period, ts * period
        _, beats = librosa.beat.beat_track(onset_envelope=env, sr=22050, hop_length=256, start_bpm=tempo, tightness=200)
        bt = librosa.frames_to_time(beats, sr=22050, hop_length=256)
        starts = [float(t) for t in bt if a <= t <= a + max(20.0, 4 * bar + 1.0)]
        for t0 in starts:
            for nb in range(int(np.ceil(lo_s / bar)), int(np.floor(hi_s / bar)) + 1):
                if t0 + nb * bar + xf + 0.5 > b:
                    break
                cands.append((t0, _refine_length(env, fps, t0, nb * bar, xf, 0.25 * beat), "bars"))
    if not cands:                               # free-time piece (or beat tracking found nothing): regular grid
        t0 = a
        while t0 <= a + 16.0:
            length = lo_s
            while length <= hi_s and t0 + length + xf <= b:
                cands.append((t0, length, "grid"))
                length += bar / ts if metric else 0.5
            t0 += 0.5
    if not cands:
        length = max(10.0, min(hi_s, b - a - xf))
        cands.append((a, length, "fallback"))
    best = None
    for t0, length, how in cands:
        s, parts = score(t0, length)
        s += 0.02 * (length - lo_s) / max(hi_s - lo_s, 1.0)     # mild preference for longer loops
        if how == "bars":
            nb = int(round(length / bar))
            s += 0.02 if nb % 4 == 0 else 0.01 if nb % 2 == 0 else 0.0     # phrases usually come in 4-bar units
        if best is None or s > best[0]:
            best = (s, t0, length, how, parts)
    s, t0, length, how, parts = best
    return dict(start=round(t0, 4), length=round(length, 4), xfade=round(xf, 3), tempo=round(tempo, 2),
                bars=round(length / bar, 2), method=how, pulse_strength=round(float(s_near), 3), match=round(float(s), 3),
                usable=(round(a, 2), round(b, 2)),
                candidates=len(cands), **parts)


def build_loop(x, sr, start_s, length_s, xfade_s):
    """Cut [start, start+length) and blend the audio that follows the loop end into the loop head with an
    equal-power crossfade. The last sample of the loop is then followed, after the wrap, by exactly the sample
    that followed it in the original recording, so the seam is continuous by construction."""
    i0 = int(round(start_s * sr))
    n = int(round(length_s * sr / 160.0)) * 160          # multiple of 160 -> exact length at 44.1 kHz too
    nx = int(round(xfade_s * sr))
    nx = min(nx, len(x) - (i0 + n), n // 2)
    y = x[i0:i0 + n].copy()
    head = y[:nx]
    tail = x[i0 + n:i0 + n + nx]
    th = np.linspace(0.0, np.pi / 2.0, nx, endpoint=False)[:, None]
    g_in, g_out = np.sin(th), np.cos(th)
    hm, tm = head.mean(axis=1), tail.mean(axis=1)
    r = float(np.dot(hm, tm) / max(np.linalg.norm(hm) * np.linalg.norm(tm), EPS))
    r = min(max(r, 0.0), 1.0)                            # only compensate positive correlation (level bump)
    norm = 1.0 / np.sqrt(1.0 + 2.0 * r * g_in * g_out)
    y[:nx] = (head * g_in + tail * g_out) * norm
    return y, dict(samples=n, xfade_samples=nx, head_tail_corr=round(r, 3))


def seam_metrics(y, sr):
    """How well the end of the loop continues into its start."""
    d1 = np.abs(np.diff(y, axis=0)).max(axis=1)
    p999 = float(np.percentile(d1, 99.9))
    jump = float(np.max(np.abs(y[0] - y[-1])))
    k = int(0.02 * sr)
    local = np.concatenate((y[-k:], y[:k]))
    local_rms = float(np.sqrt(np.mean(np.diff(local, axis=0) ** 2)))
    d2 = np.abs(np.diff(y, n=2, axis=0)).max(axis=1)
    kink = float(np.max(np.abs((y[0] - y[-1]) - (y[-1] - y[-2]))))
    # high-frequency click at the wrap: short-time energy above 4 kHz in a +-0.25 s window around the seam
    w = int(0.25 * sr)
    wrap = np.concatenate((y[-w:], y[:w])).mean(axis=1)
    f, t, z = sps.stft(wrap, sr, nperseg=512, noverlap=384, boundary=None)
    hf = 10.0 * np.log10(np.maximum((np.abs(z[f > 4000.0]) ** 2).sum(axis=0), 1e-20))
    mid = int(np.argmin(np.abs(t - w / sr)))
    click = float(hf[max(0, mid - 2):mid + 3].max() - np.median(hf))
    # level step across the seam (1 s either side)
    s = int(min(sr, len(y) // 4))
    step = float(db(np.sqrt(np.mean(y[:s] ** 2))) - db(np.sqrt(np.mean(y[-s:] ** 2))))
    return dict(jump=round(jump, 6), jump_vs_p999=round(jump / max(p999, EPS), 3),
                jump_vs_local_rms=round(jump / max(local_rms, EPS), 3),
                kink_vs_p999=round(kink / max(float(np.percentile(d2, 99.9)), EPS), 3),
                hf_click_db=round(click, 1), level_step_db=round(step, 2))


def boundary_clicks(x, sr, period_s):
    """Worst high-frequency transient (dB above the local median) exactly at multiples of period_s."""
    mono = x.mean(axis=1)
    w = int(0.25 * sr)
    worst = -99.0
    t = period_s
    while t < len(mono) / sr - 1.0:
        c = int(round(t * sr))
        seg = mono[c - w:c + w]
        f, tt, z = sps.stft(seg, sr, nperseg=512, noverlap=384, boundary=None)
        hf = 10.0 * np.log10(np.maximum((np.abs(z[f > 4000.0]) ** 2).sum(axis=0), 1e-20))
        mid = int(np.argmin(np.abs(tt - w / sr)))
        worst = max(worst, float(hf[max(0, mid - 1):mid + 2].max() - np.median(hf)))
        t += period_s
    return round(worst, 1)


def boundary_steps(x, period_samples, sr):
    """Decoder tiles are joined without a crossfade, so a mismatch shows up as a one-sample step exactly at a
    multiple of the tile stride. Returns the largest such step relative to the 99.9th percentile of ordinary
    sample-to-sample movement in the surrounding half second (a drum hit that happens to fall on a boundary has
    large movement all around it and does not stand out; a tiling step does)."""
    w = int(0.25 * sr)
    worst = 0.0
    k = period_samples
    while k < len(x) - w:
        d = np.abs(np.diff(x[k - w:k + w], axis=0)).max(axis=1)
        step = float(d[w - 1])                       # |x[k] - x[k-1]|
        rest = np.delete(d, w - 1)
        worst = max(worst, step / max(float(np.percentile(rest, 99.9)), 1e-6))
        k += period_samples
    return round(worst, 2)


# ------------------------------------------------------------------------------------------------ mastering
@njit(cache=True)
def _comp_gain_db(level_db, thr, ratio, knee, att, rel):
    out = np.empty_like(level_db)
    state = 0.0
    slope = 1.0 - 1.0 / ratio
    for i in range(level_db.shape[0]):
        over = level_db[i] - thr
        if over <= -0.5 * knee:
            gr = 0.0
        elif over >= 0.5 * knee:
            gr = slope * over
        else:
            gr = slope * (over + 0.5 * knee) ** 2 / (2.0 * knee)
        coef = att if gr > state else rel
        state = coef * state + (1.0 - coef) * gr
        out[i] = -state
    return out


@njit(cache=True)
def _release(g, coef):
    out = np.empty_like(g)
    prev = 1.0
    for i in range(g.shape[0]):
        up = prev + (1.0 - prev) * coef
        prev = g[i] if g[i] < up else up
        out[i] = prev
    return out


def compress(x, sr, ratio=1.6, knee=6.0, attack_ms=30.0, release_ms=300.0, pct=70.0):
    """Light stereo-linked bus compression. The threshold sits at the pct-th percentile of the active level, so
    only the loudest passages are touched (a few dB of gain reduction at most)."""
    p = ndi.uniform_filter1d((x * x).mean(axis=1), size=int(0.010 * sr), mode="nearest")
    lev = 10.0 * np.log10(np.maximum(p, 1e-14))
    active = lev[lev > lev.max() - 50.0]
    thr = float(np.percentile(active, pct)) if len(active) else -20.0
    att = np.exp(-1.0 / (attack_ms * 0.001 * sr))
    rel = np.exp(-1.0 / (release_ms * 0.001 * sr))
    g_db = _comp_gain_db(lev, thr, ratio, knee, att, rel)
    return x * (10.0 ** (g_db / 20.0))[:, None], dict(comp_threshold_db=round(thr, 1), comp_max_gr_db=round(float(-g_db.min()), 2),
                                                    comp_mean_gr_db=round(float(-g_db.mean()), 2))


def limit(x, sr, ceiling_db=-1.5, lookahead_ms=5.0, release_ms=120.0):
    """Look-ahead peak limiter (offline, zero latency): the gain never exceeds what each sample needs."""
    ceiling = 10.0 ** (ceiling_db / 20.0)
    peak = np.max(np.abs(x), axis=1)
    need = np.minimum(1.0, ceiling / np.maximum(peak, EPS))
    w = 2 * int(lookahead_ms * 0.001 * sr) + 1
    g = ndi.minimum_filter1d(need, size=w, mode="nearest")
    g = ndi.uniform_filter1d(g, size=w, mode="nearest")
    g = np.minimum(g, need)
    g = _release(g, 1.0 - np.exp(-1.0 / (release_ms * 0.001 * sr)))
    g = np.minimum(g, need)
    return x * g[:, None], dict(limiter_max_gr_db=round(float(-db(g.min())), 2),
                                limited_frac=round(float(np.mean(g < 0.999)), 4))


def master(y, sr, target_lufs, loop, out_sr=44100, fade_in_s=0.0, fade_out_s=0.0):
    """High-pass 25 Hz, light compression, loudness normalisation, limiter, resampling to out_sr.
    Loops are processed as three back-to-back copies and the middle copy is kept, so filters, compressor and
    limiter are in their periodic steady state at the seam and the loop stays seamless after mastering."""
    n = len(y)
    info = {}
    if not loop:
        y = y.copy()
        if fade_in_s > 0:
            k = int(fade_in_s * sr)
            y[:k] *= np.sin(np.linspace(0, np.pi / 2, k))[:, None] ** 2
        if fade_out_s > 0:
            k = int(fade_out_s * sr)
            y[-k:] *= np.cos(np.linspace(0, np.pi / 2, k))[:, None] ** 2
    z = np.concatenate((y, y, y)) if loop else np.concatenate((np.zeros((sr, 2)), y, np.zeros((sr, 2))))
    mid = slice(n, 2 * n) if loop else slice(sr, sr + n)
    sos = sps.butter(2, 25.0, btype="highpass", fs=sr, output="sos")
    z = sps.sosfilt(sos, z, axis=0)
    z, ci = compress(z, sr)
    info.update(ci)
    gain_db = target_lufs - lufs(z[mid], sr)
    z = z * 10.0 ** (gain_db / 20.0)
    ceiling = -1.5
    for _ in range(4):
        zl, li = limit(z, sr, ceiling_db=ceiling)
        delta = target_lufs - lufs(zl[mid], sr)
        tp = true_peak_db(zl[mid], sr)
        if abs(delta) <= 0.1 and tp <= -1.0:
            break
        if tp > -1.0:
            ceiling -= (tp + 1.0) + 0.1
        z = z * 10.0 ** (delta / 20.0)
    info.update(li)
    info["gain_db"] = round(float(gain_db), 2)
    out = sps.resample_poly(zl, out_sr, sr, axis=0) if out_sr != sr else zl
    if loop:
        m = n * out_sr // sr
        out = out[m:2 * m]
    else:
        m = int(round(n * out_sr / sr))
        s = int(round(sr * out_sr / sr))
        out = out[s:s + m]
    info.update(lufs=round(lufs(out, out_sr), 2), true_peak_db=round(true_peak_db(out, out_sr), 2),
                peak_db=round(float(db(np.max(np.abs(out)))), 2), samples=int(len(out)), sample_rate=out_sr,
                seconds=round(len(out) / out_sr, 4))
    return out, info
