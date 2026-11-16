"""Second opinion on "sung lyrics or speech": run a speech recogniser (OpenAI Whisper small) over the music.

Music without words makes Whisper output nothing, a music marker, or a short low-confidence hallucination;
sung or spoken words give multi-word text with a decent average token probability and a low no-speech
probability.  For every 30 s window this records the text, the no-speech probability and the average token
log-probability, and counts the windows that look like real words:
    no_speech_prob < 0.5  and  avg_logprob > -1.0  and  at least 4 words (or 8 CJK characters)
'frac' is the share of such windows.  The audio-text model (clap_score.py) stays the first line; this check is
there because a song model's typical failure is exactly a voice singing words.

Runs on the CPU in the separate "cpu" environment (PyPI torch); needs only torch, transformers, soundfile, scipy:
    <MA_ROOT>/venvs/cpu/Scripts/python.exe speech_check.py --json out.json file1.flac [file2.ogg ...]
"""
import os
import re

import numpy as np

MODEL_DIR = os.path.join(os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai")),
                         "models", "whisper-small")


class WordDetector:
    def __init__(self, model_dir=MODEL_DIR, device=None):
        import torch
        from transformers import WhisperForConditionalGeneration, WhisperProcessor
        self.torch = torch
        self.device = device or "cpu"      # the decoder loop is far slower on the ROCm GPU than on the CPU
        self.proc = WhisperProcessor.from_pretrained(model_dir, local_files_only=True)
        self.model = WhisperForConditionalGeneration.from_pretrained(model_dir, local_files_only=True).eval().to(self.device)
        tok = self.proc.tokenizer
        self.sot = tok.convert_tokens_to_ids("<|startoftranscript|>")
        ids = [tok.convert_tokens_to_ids(t) for t in ("<|nospeech|>", "<|nocaptions|>")]
        self.nospeech = [i for i in ids if i is not None and i != tok.unk_token_id][0]

    def check(self, x, sr, win_s=30.0):
        import scipy.signal as sps
        from math import gcd
        torch = self.torch
        mono = x.mean(axis=1).astype(np.float32)
        if sr != 16000:
            g = gcd(16000, sr)
            mono = sps.resample_poly(mono, 16000 // g, sr // g).astype(np.float32)
        n = int(win_s * 16000)
        wins = [mono[i:i + n] for i in range(0, len(mono), n)]
        wins = [w for w in wins if len(w) >= 16000 * 5]
        out = []
        for w in wins:
            feats = self.proc.feature_extractor(w, sampling_rate=16000, return_tensors="pt").input_features.to(self.device)
            with torch.inference_mode():
                enc = self.model.model.encoder(feats)
                first = self.model(decoder_input_ids=torch.tensor([[self.sot]], device=self.device), encoder_outputs=enc).logits[0, 0]
                p_ns = float(torch.softmax(first.float(), -1)[self.nospeech])
                ids = self.model.generate(feats, max_new_tokens=48, do_sample=False, num_beams=1)
                text = self.proc.tokenizer.decode(ids[0], skip_special_tokens=True).strip()
                lab = ids[:, 1:].clone()
                loss = self.model(input_features=feats, decoder_input_ids=ids[:, :-1], labels=lab).loss if ids.shape[1] > 2 else None
            avg_lp = -float(loss) if loss is not None else -9.0
            plain = re.sub(r"[\[\(（【♪*].*?[\]\)）】♪*]", " ", text)          # drop [Music], (applause), ♪...♪
            words = re.findall(r"[A-Za-zÀ-ÿ']{2,}", plain)
            cjk = re.findall(r"[\u3040-\u30ff\u3400-\u9fff\uac00-\ud7af]", plain)
            real = (p_ns < 0.5) and (avg_lp > -1.0) and (len(words) >= 4 or len(cjk) >= 8)
            out.append(dict(text=text[:120], no_speech=round(p_ns, 3), avg_logprob=round(avg_lp, 2), words=len(words) + len(cjk),
                            real=bool(real)))
        k = sum(1 for o in out if o["real"])
        return dict(windows=len(out), word_windows=k, frac=round(k / max(len(out), 1), 3), detail=out)


def main():
    import argparse
    import json
    import sys
    import time
    import soundfile as sf
    import torch
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--json", default="")
    ap.add_argument("--threads", type=int, default=10)
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()
    torch.set_num_threads(args.threads)
    wd = WordDetector()
    res = {}
    for path in args.files:
        x, sr = sf.read(path, dtype="float32", always_2d=True)
        t = time.time()
        r = wd.check(x, sr)
        r["seconds_taken"] = round(time.time() - t, 1)
        res[path] = r
        if not args.quiet:
            print("%s: %d/%d windows with words (%.1f s)" % (os.path.basename(path), r["word_windows"], r["windows"], r["seconds_taken"]))
            for d in r["detail"]:
                print("    nospeech %.2f  logp %5.2f  words %2d  %s  %r" % (d["no_speech"], d["avg_logprob"], d["words"],
                                                                      "REAL" if d["real"] else "    ", d["text"][:90]))
            sys.stdout.flush()
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(res, f, indent=1)


if __name__ == "__main__":
    main()
