"""Speaker embeddings for consistency checks (run with the `cosy` venv: onnxruntime + torchaudio).

    <venvs\\cosy python> spk.py in.json out.json        in.json = ["a.wav", "b.wav", ...]

Model: CAM++ (`campplus.onnx`, 192-dimensional), the speaker encoder that ships with the official
CosyVoice 3 weights (Apache-2.0) - the network CosyVoice itself uses to tell voices apart. Input is prepared
exactly as CosyVoice does (16 kHz, 80-bin Kaldi fbank, mean-normalised). out.json = {path: [192 floats]}.
Cosine similarity of two embeddings: about 0.2 or less for different people, 0.6 and above for the same voice.
"""
import json
import os
import sys

import numpy as np
import onnxruntime
import soundfile as sf
import torch
import torchaudio.compliance.kaldi as kaldi
from scipy import signal

ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
MODEL = os.path.join(ROOT, "models", "cosy3", "campplus.onnx")


def main():
    paths = json.load(open(sys.argv[1], encoding="utf-8"))
    opt = onnxruntime.SessionOptions()
    opt.intra_op_num_threads = 2
    sess = onnxruntime.InferenceSession(MODEL, sess_options=opt, providers=["CPUExecutionProvider"])
    name = sess.get_inputs()[0].name
    out = {}
    for p in paths:
        x, sr = sf.read(p, dtype="float32", always_2d=True)
        x = x.mean(axis=1)
        if sr != 16000:
            import math
            g = math.gcd(sr, 16000)
            x = signal.resample_poly(x.astype(np.float64), 16000 // g, sr // g).astype(np.float32)
        peak = float(np.abs(x).max()) or 1.0
        idx = np.nonzero(np.abs(x) > peak * 0.02)[0]                    # cut leading / trailing silence
        if len(idx):
            x = x[max(0, idx[0] - 800):idx[-1] + 800]
        feat = kaldi.fbank(torch.from_numpy(x / peak * 0.5).unsqueeze(0), num_mel_bins=80, dither=0, sample_frequency=16000)
        feat = feat - feat.mean(dim=0, keepdim=True)
        emb = sess.run(None, {name: feat.unsqueeze(0).numpy()})[0].flatten()
        out[p] = [float(v) for v in emb / (np.linalg.norm(emb) + 1e-9)]
    json.dump(out, open(sys.argv[2], "w", encoding="utf-8"))


if __name__ == "__main__":
    main()
