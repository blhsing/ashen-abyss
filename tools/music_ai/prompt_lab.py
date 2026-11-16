"""Prompt experiments, judged by measurement: renders short clips for several caption variants and seeds with
the model loaded once, scores each clip with the same audio-text model the batch uses, and prints a table.

    python prompt_lab.py <variants.json> [--seconds 30] [--seeds 1,2,3] [--keep DIR]

variants.json: {"describe": "...text the clips are scored against...", "bpm": 116, "key": "G minor", "ts": 4,
                "pulse": true, "variants": {"A": {"caption": "...", "lyrics": "[Instrumental]"}, ...}}
Each variant may override bpm / key / ts / lyrics.  Captions must stay generic (instruments, mood, tempo, form).
"""
import argparse
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tracks as T  # noqa: E402
import gen_ace  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("spec")
    ap.add_argument("--seconds", type=float, default=30.0)
    ap.add_argument("--seeds", default="1,2,3")
    ap.add_argument("--keep", default="", help="folder for the rendered clips (FLAC)")
    ap.add_argument("--model", default=gen_ace.DEFAULT_MODEL)
    args = ap.parse_args()
    with open(args.spec, encoding="utf-8") as f:
        spec = json.load(f)
    seeds = [int(s) for s in args.seeds.split(",")]
    gen_ace.keep_awake(True)
    import numpy as np
    import torch
    import soundfile as sf
    from diffusers import AceStepPipeline
    import audiolib as al
    from clap_score import ClapScorer
    if gen_ace.free_gb() < 16:
        raise SystemExit("not enough free memory (%.1f GB)" % gen_ace.free_gb())
    pipe = AceStepPipeline.from_pretrained(args.model, torch_dtype=torch.bfloat16, local_files_only=True).to("cuda")
    pipe.vae.to(torch.float32)
    pipe.vae.enable_tiling()
    pipe.set_progress_bar_config(disable=True)
    scorer = ClapScorer(device="cuda")
    n_lat = math.ceil(args.seconds * pipe.latents_per_second)
    dim = pipe.transformer.config.audio_acoustic_hidden_dim
    rows = []
    for name, v in spec["variants"].items():
        acc = []
        for seed in seeds:
            if gen_ace.canary("cuda", torch):
                raise SystemExit("GPU canary failed")
            noise = torch.randn((1, n_lat, dim), generator=torch.Generator("cpu").manual_seed(seed), dtype=torch.float32)
            t = time.time()
            with torch.inference_mode():
                lat = pipe(prompt=v["caption"], lyrics=v.get("lyrics", "[Instrumental]"), vocal_language="unknown",
                           audio_duration=args.seconds, num_inference_steps=8, shift=3.0, guidance_scale=1.0,
                           bpm=int(v.get("bpm", spec["bpm"])), keyscale=v.get("key", spec["key"]),
                           timesignature=str(v.get("ts", spec.get("ts", 4))),
                           latents=noise.to("cuda", torch.bfloat16), output_type="latent").audios
                lat_ok = bool(torch.isfinite(lat.float()).all())
                audio = pipe.vae.decode(lat.transpose(1, 2).float()).sample.float().cpu()
            x = audio[0].numpy().T.astype(np.float64)
            if not lat_ok or not np.isfinite(x).all():
                bad_frac = float(np.mean(~np.isfinite(x)))
                print("%-4s seed %d  NON-FINITE OUTPUT (latents finite: %s, %.1f%% of the samples bad, canary after: %r)" % (
                    name, seed, lat_ok, 100 * bad_frac, gen_ace.canary("cuda", torch) or "ok"), flush=True)
                continue
            x = x / max(float(np.max(np.abs(x))), 1e-9) * 0.89
            c = scorer.score(x, pipe.sample_rate, v.get("describe", spec["describe"]), T.FLAG_SETS)
            _, env, fps = al.onset_envelope(x, pipe.sample_rate)
            tn, sn, tg, sg = al.tempo_near(env, fps, float(v.get("bpm", spec["bpm"])))
            fl = c["flags"]
            score = 100 * c["sim"] - 25 * fl["vocal"]["bad_mean"] - 12 * fl["genre"]["bad_mean"] - 12 * fl["mood"]["bad_mean"]
            acc.append((score, c["sim"], fl["vocal"]["bad_mean"], fl["genre"]["bad_mean"], fl["mood"]["bad_mean"], sn, sg))
            print("%-4s seed %d  score %5.1f  sim %.3f  vocal %.2f  genre %.2f (%s)  mood %.2f (%s)  pulse@bpm %.2f  %.0f s" % (
                name, seed, score, c["sim"], fl["vocal"]["bad_mean"], fl["genre"]["bad_mean"], fl["genre"]["top"][:24],
                fl["mood"]["bad_mean"], fl["mood"]["top"][:20], sn, time.time() - t), flush=True)
            if args.keep:
                os.makedirs(args.keep, exist_ok=True)
                sf.write(os.path.join(args.keep, "%s_s%d.flac" % (name, seed)), x, pipe.sample_rate, subtype="PCM_16")
        if not acc:
            continue
        m = np.mean(np.array(acc), axis=0)
        rows.append((m[0], name, m))
    print("\nvariant  score    sim  vocal  genre   mood  pulse   (means over %d seeds, %.0f s clips)" % (len(seeds), args.seconds))
    for _, name, m in sorted(rows, reverse=True):
        print("%-7s %6.1f  %.3f   %.2f   %.2f   %.2f   %.2f" % (name, m[0], m[1], m[2], m[3], m[4], m[5]))
    sys.stdout.flush()
    os._exit(0)


if __name__ == "__main__":
    main()
