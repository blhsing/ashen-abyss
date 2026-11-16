"""Second gate for a designed voice: does it sound male or female?  (run with the `voxcpm` venv)

    gender.py calibrate      fit and cross-validate the classifier, write gender_model.json
    gender.py score a.wav b.wav ...

Pitch alone is not enough (an "old man" designed at 172 Hz was heard as an elderly woman), and no
gender classifier is installed. So one is built from what is on disk, with labels that are certain:
the Kokoro v1.1-zh model has 100 Mandarin speakers, 55 female (zf_*, ids 3-57) and 45 male (zm_*, ids
58-102). Each speaks one sentence; the CAM++ speaker encoder (the one used for the consistency check)
turns every clip into a 192-dimensional embedding; a regularised logistic regression on those embeddings
separates the sexes. Cross-validated accuracy and the pitch statistics of both groups are printed and
stored. No recording of a person is involved: the calibration voices are synthetic too.
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
MODEL = os.path.join(HERE, "gender_model.json")
TEXT = "夜深了，爐火還在燒。我坐在窗邊，聽遠處的風慢慢吹過山谷。"
FEMALE, MALE = range(3, 58), range(58, 103)
_model = None


def _fit(X: np.ndarray, y: np.ndarray, l2: float = 0.01, steps: int = 3000, lr: float = 2.0):
    w, b = np.zeros(X.shape[1]), 0.0
    for _ in range(steps):
        p = 1 / (1 + np.exp(-(X @ w + b)))
        g = p - y
        w -= lr * (X.T @ g / len(y) + l2 * w)
        b -= lr * float(g.mean())
    return w, b


def p_male(emb: np.ndarray) -> float:
    """Probability (0..1) that a unit CAM++ embedding belongs to a male voice."""
    global _model
    if _model is None:
        m = json.load(open(MODEL, encoding="utf-8"))
        _model = (np.array(m["w"]), float(m["b"]))
    w, b = _model
    return float(1 / (1 + np.exp(-(float(np.dot(emb, w)) + b))))


def calibrate():
    import engine as E
    import kokoro_tts
    import vt
    out = os.path.join(vt.ROOT, "stage", "gender")
    os.makedirs(out, exist_ok=True)
    tts = None
    paths, labels, f0 = [], [], []
    for sid in list(FEMALE) + list(MALE):
        p = os.path.join(out, f"kokoro_{sid}.wav")
        if not os.path.exists(p):
            tts = tts or kokoro_tts.make_tts()
            a = tts.generate(vt.t2s(TEXT), sid=sid, speed=1.0)
            vt.write_float(p, np.asarray(a.samples, dtype=np.float32), a.sample_rate)
        x, sr = vt.read_wav(p)
        paths.append(p); labels.append(1.0 if sid in MALE else 0.0); f0.append(vt.voice_stats(x, sr)["f0"] or 0)
    emb = E.embeddings(paths)
    X, y, f0 = np.array([emb[p] for p in paths]), np.array(labels), np.array(f0)
    rng = np.random.default_rng(0)
    idx = rng.permutation(len(y))
    wrong, margins = 0, []
    for k in range(5):                                   # 5-fold cross-validation
        test = idx[k::5]
        train = np.setdiff1d(idx, test)
        w, b = _fit(X[train], y[train])
        s = X[test] @ w + b
        wrong += int(np.sum((s > 0) != (y[test] > 0.5)))
        margins += list(np.where(y[test] > 0.5, s, -s))
    w, b = _fit(X, y)
    fm, ff = f0[y > 0.5], f0[y < 0.5]
    info = dict(calibration="Kokoro v1.1-zh speakers 3-57 (female) and 58-102 (male), one sentence each, CAM++ embeddings",
                speakers=len(y), cross_validation_errors=wrong, cross_validation_accuracy=round(1 - wrong / len(y), 3),
                smallest_margin=round(float(min(margins)), 2),
                male_pitch_hz=dict(min=int(fm.min()), p10=int(np.percentile(fm, 10)), median=int(np.median(fm)), p90=int(np.percentile(fm, 90)), max=int(fm.max())),
                female_pitch_hz=dict(min=int(ff.min()), p10=int(np.percentile(ff, 10)), median=int(np.median(ff)), p90=int(np.percentile(ff, 90)), max=int(ff.max())))
    json.dump(dict(info=info, w=[float(v) for v in w], b=float(b)), open(MODEL, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(json.dumps(info, ensure_ascii=False, indent=1))


def main():
    if sys.argv[1] == "calibrate":
        calibrate()
    else:
        import engine as E
        import vt
        emb = E.embeddings(sys.argv[2:])
        for p in sys.argv[2:]:
            x, sr = vt.read_wav(p)
            st = vt.voice_stats(x, sr)
            print(f"{os.path.basename(p):40} p(male) {p_male(emb[p]):.2f}  median pitch {st['f0']} Hz  harmonics-to-noise {st['hnr']} dB")


if __name__ == "__main__":
    main()
