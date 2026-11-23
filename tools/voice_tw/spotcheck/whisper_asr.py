"""AUDIO agent spot check (2026-11-22): an independent recogniser (Whisper small, music_ai's CPU venv) reads the
delivered .ogg files back. Usage: <music_ai venv cpu python> whisper_asr.py out.json id1 id2 ...
Comparison against the script is done by compare.py (voxcpm venv, uses vt.py's error rates)."""
import json, os, sys
import numpy as np, soundfile as sf, scipy.signal as sps, torch
from transformers import WhisperForConditionalGeneration, WhisperProcessor
torch.set_num_threads(4)
M = os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai", "models", "whisper-small")
proc = WhisperProcessor.from_pretrained(M, local_files_only=True)
model = WhisperForConditionalGeneration.from_pretrained(M, local_files_only=True).eval()
out = {}
for i in sys.argv[2:]:
    x, sr = sf.read(os.path.join(r"Y:\game\assets\audio\vo", i + ".ogg"), dtype="float32")
    if x.ndim > 1: x = x.mean(axis=1)
    x = sps.resample_poly(x, 160, sr // 100).astype(np.float32)
    f = proc.feature_extractor(x, sampling_rate=16000, return_tensors="pt").input_features
    with torch.inference_mode():
        ids = model.generate(f, language="zh", task="transcribe", max_new_tokens=120, do_sample=False, num_beams=1)
    out[i] = proc.tokenizer.decode(ids[0], skip_special_tokens=True).strip()
    print(i, out[i], flush=True)
json.dump(out, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
