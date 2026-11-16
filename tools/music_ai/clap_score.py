"""Audio-text similarity scoring with LAION CLAP (laion/larger_clap_music_and_speech, Apache-2.0).

Runs on the GPU when one is available: AMD's ROCm build of torch has no optimised CPU BLAS (matmul is about
100x slower than a normal build), so CPU scoring in the production environment would crawl.

Used to pick between candidates nobody has listened to:
  sim           mean cosine similarity between 10-second windows of the audio and the piece's description
  flags[set]    zero-shot label probabilities over small label sets (vocal content, genre drift, mood);
                'bad_mean' / 'bad_max' are the mean and the worst-window probability mass of the unwanted labels
"""
import os

import numpy as np

MODEL_DIR = os.path.join(os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai")),
                         "models", "clap", "larger_clap_music_and_speech")


class ClapScorer:
    def __init__(self, model_dir=MODEL_DIR, threads=10, device=None):
        import torch
        from transformers import ClapModel, ClapProcessor
        self.torch = torch
        torch.set_num_threads(threads)
        self.device = device or ("cuda" if torch.cuda.is_available() else "cpu")
        self.model = ClapModel.from_pretrained(model_dir, local_files_only=True).eval().to(self.device)
        self.proc = ClapProcessor.from_pretrained(model_dir, local_files_only=True)
        self.scale = float(self.model.logit_scale_a.exp())
        self._text_cache = {}

    def _norm(self, e):
        return e / e.norm(dim=-1, keepdim=True).clamp(min=1e-9)

    def text(self, texts):
        missing = [t for t in texts if t not in self._text_cache]
        if missing:
            tok = self.proc.tokenizer(missing, padding=True, truncation=True, return_tensors="pt").to(self.device)
            with self.torch.inference_mode():
                emb = self._norm(self.model.get_text_features(**tok)).float().cpu()
            for t, e in zip(missing, emb):
                self._text_cache[t] = e
        return self.torch.stack([self._text_cache[t] for t in texts])

    def audio(self, x, sr, win_s=10.0):
        """Embeddings of consecutive 10 s windows (mono, 48 kHz). x: (samples, channels) float array."""
        import scipy.signal as sps
        from math import gcd
        mono = x.mean(axis=1).astype(np.float32)
        if sr != 48000:
            g = gcd(48000, int(sr))
            mono = sps.resample_poly(mono, 48000 // g, int(sr) // g).astype(np.float32)
        n = int(win_s * 48000)
        wins = [mono[i:i + n] for i in range(0, max(1, len(mono) - n // 2), n)]
        wins = [w for w in wins if len(w) >= n // 2] or [mono]
        out = []
        for i in range(0, len(wins), 8):
            feats = self.proc.feature_extractor(wins[i:i + 8], sampling_rate=48000, return_tensors="pt").to(self.device)
            with self.torch.inference_mode():
                out.append(self._norm(self.model.get_audio_features(**feats)).float().cpu())
        return self.torch.cat(out)

    def score(self, x, sr, describe, flag_sets):
        a = self.audio(x, sr)                                   # (windows, dim)
        t = self.text([describe])
        sims = (a @ t.T).squeeze(1)
        res = dict(sim=round(float(sims.mean()), 4), sim_min=round(float(sims.min()), 4), windows=int(len(a)), flags={})
        for key, fs in flag_sets.items():
            lab = self.text(fs["labels"])
            probs = self.torch.softmax(self.scale * (a @ lab.T), dim=1)       # (windows, labels)
            bad = probs[:, fs["ok"]:].sum(dim=1)
            mean = probs.mean(dim=0)
            res["flags"][key] = dict(bad_mean=round(float(bad.mean()), 3), bad_max=round(float(bad.max()), 3),
                                     bad_windows=int((bad > 0.5).sum()),
                                     top=fs["labels"][int(mean.argmax())],
                                     probs={l: round(float(p), 3) for l, p in zip(fs["labels"], mean)})
        return res


def main():
    """CLI used by produce.py when the GPU is not in use: runs in the "cpu" environment (PyPI torch).
        python clap_score.py --track <name> --json out.json <audio file>"""
    import argparse
    import json
    import sys
    import soundfile as sf
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import tracks as T
    ap = argparse.ArgumentParser()
    ap.add_argument("file")
    ap.add_argument("--track", required=True)
    ap.add_argument("--json", required=True)
    args = ap.parse_args()
    x, sr = sf.read(args.file, dtype="float32", always_2d=True)
    res = ClapScorer(device="cpu").score(x, sr, T.BY_NAME[args.track]["describe"], T.FLAG_SETS)
    with open(args.json, "w", encoding="utf-8") as f:
        json.dump(res, f)


if __name__ == "__main__":
    main()
