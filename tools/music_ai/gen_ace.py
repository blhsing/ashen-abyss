"""Generate candidates with ACE-Step 1.5 (Diffusers `AceStepPipeline`, XL turbo by default).

One process loads the model once and renders every requested (track, seed) that is not on disk yet:
    <out>/<track>__s<seed>.flac     48 kHz stereo, 24 bit, peak-normalised to -1 dBFS (as the pipeline does)
    <out>/<track>__s<seed>.json     prompt, metadata, seed, timings, device, versions

The initial noise is always drawn on the CPU from the seed, so the same seed gives the same piece on the GPU
and on the CPU (this is what makes the CPU-vs-GPU sanity comparison meaningful).

Robustness on this laptop (integrated GPU = system RAM, other heavy jobs running):
  * the weights are read in their stored bfloat16 form and converted to float32 tensor by tensor while they are
    moved to the device, so the peak footprint is about the size of the float32 model (23 GB) instead of twice
    that; float16 gives NaN on ROCm and bfloat16 is reported to crash on this GPU family, hence float32
  * before loading, the script waits until enough memory is free (exit code 5 if it never is)
  * a small "canary" computation is compared between GPU and CPU before and after every piece; the one failure
    seen during testing was an integer op returning garbage while the machine was out of memory
  * the process ends with os._exit, because the ROCm runtime can hang during interpreter shutdown after an error

Usage (inside the environment, see env.ps1):
    python gen_ace.py --out DIR --tracks pilot|all|name[,name...] [--device cuda|cpu] [--dtype float32]
                      [--seeds 1,2,3] [--seconds N] [--suffix TXT] [--vae-device cpu] [--heartbeat FILE]
    --device dml selects DirectML (only in the torch-directml test environment).
A render that contains NaN/Inf is discarded and its seed is marked with <stem>.nan.json (never retried).
Exit codes: 0 ok, 3 runtime failure on the device (the caller falls back to another device), 4 too many
non-finite renders, 5 not enough free memory, 6 the GPU returned wrong results for the canary (output discarded).
"""
import argparse
import ctypes
import json
import math
import os
import sys
import time
import traceback

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tracks as T  # noqa: E402

DEFAULT_MODEL = os.path.join(os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai")),
                             "models", "acestep-v15-xl-turbo-diffusers")
MODEL_ID = "ACE-Step 1.5 XL turbo (acestep-v15-xl-turbo-diffusers)"


class CanaryError(RuntimeError):
    """The GPU returned wrong results for the canary computation."""


def keep_awake(on):
    """Hold a system-required execution state for this thread so Modern Standby does not suspend the run."""
    try:
        ES_CONTINUOUS, ES_SYSTEM_REQUIRED = 0x80000000, 0x00000001
        ctypes.windll.kernel32.SetThreadExecutionState(ES_CONTINUOUS | (ES_SYSTEM_REQUIRED if on else 0))
    except Exception:
        pass


def log(msg):
    print(time.strftime("%H:%M:%S ") + msg, flush=True)


def free_gb():
    try:
        import psutil
        return psutil.virtual_memory().available / 2 ** 30
    except Exception:
        return 1e9


def stream_to(module, device, torch):
    """Move a module to `device` as float32, one tensor at a time (low peak memory, exact for bfloat16 weights)."""
    for p in module.parameters():
        p.data = (p.data.to(torch.float32) if p.is_floating_point() else p.data).to(device)
    for mod in module.modules():
        for k, b in list(mod._buffers.items()):
            if b is not None:
                mod._buffers[k] = (b.to(torch.float32) if b.is_floating_point() else b).to(device)


def canary(device, torch):
    """Deterministic float and integer operations on the device compared with the CPU. Returns '' if all agree."""
    g = torch.Generator().manual_seed(123)
    a = torch.randn(192, 192, generator=g)
    idx = torch.randint(0, 7, (1000,), generator=g)
    ag, ig = a.to(device), idx.to(device)
    bad = []
    if not torch.allclose((ag @ ag).cpu(), a @ a, rtol=1e-3, atol=1e-3):
        bad.append("matmul")
    if not torch.equal(torch.bincount(ig, minlength=7).cpu(), torch.bincount(idx, minlength=7)):
        bad.append("bincount")
    if int(ig.max().item()) != int(idx.max().item()):
        bad.append("max")
    if not torch.equal(torch.cumsum(ig, 0).cpu(), torch.cumsum(idx, 0)):
        bad.append("cumsum")
    if not torch.equal(torch.argsort(ig * 1000 + torch.arange(1000, device=device), stable=True).cpu(),
                       torch.argsort(idx * 1000 + torch.arange(1000), stable=True)):
        bad.append("argsort")
    return ",".join(bad)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--model-id", default=MODEL_ID, help="name recorded in the metadata")
    ap.add_argument("--out", required=True)
    ap.add_argument("--tracks", default="pilot")
    ap.add_argument("--seeds", default="")
    ap.add_argument("--seconds", type=float, default=0.0, help="override the generated length (benchmarks)")
    ap.add_argument("--suffix", default="", help="appended to the file stem, e.g. _cpu")
    ap.add_argument("--device", default="cuda")
    ap.add_argument("--dtype", default="float32", choices=["float32", "float16", "bfloat16"])
    ap.add_argument("--vae-device", default="", help="decode on another device (default: same as --device)")
    ap.add_argument("--steps", type=int, default=8)
    ap.add_argument("--shift", type=float, default=3.0)
    ap.add_argument("--threads", type=int, default=10)
    ap.add_argument("--heartbeat", default="")
    ap.add_argument("--need-gb", type=float, default=0.0, help="free memory required before loading (0 = 28 GB for float32, 16 GB otherwise)")
    ap.add_argument("--mem-wait", type=float, default=900.0, help="seconds to wait for that memory")
    ap.add_argument("--headroom-gb", type=float, default=7.0, help="free memory required before each render")
    args = ap.parse_args()

    keep_awake(True)
    os.makedirs(args.out, exist_ok=True)
    names = T.PILOT if args.tracks == "pilot" else [t["name"] for t in T.TRACKS] if args.tracks == "all" \
        else [n for n in args.tracks.split(",") if n]
    jobs = []
    for n in names:
        tr = T.BY_NAME[n]
        seeds = [int(s) for s in args.seeds.split(",") if s] or tr["seeds"]
        for s in seeds:
            stem = os.path.join(args.out, "%s__s%d%s" % (n, s, args.suffix))
            if os.path.exists(stem + ".nan.json"):
                continue                      # this seed gave a non-finite result before: do not try it again
            if not (os.path.exists(stem + ".flac") and os.path.exists(stem + ".json")):
                jobs.append((tr, s, stem))
    if not jobs:
        log("nothing to do")
        return 0

    def beat(note):
        if args.heartbeat:
            with open(args.heartbeat, "w", encoding="utf-8") as f:
                f.write("%d %s" % (time.time(), note))

    if args.need_gb <= 0:
        # model + room for the render + other jobs growing (CPU: 20 GB of float32 weights + working memory)
        args.need_gb = 26.0 if args.device == "cpu" else 30.0 if args.dtype == "float32" else 22.0
    t_wait = time.time()
    while free_gb() < args.need_gb:
        if time.time() - t_wait > args.mem_wait:
            log("only %.1f GB of memory free (need %.0f GB) after waiting %.0f s" % (free_gb(), args.need_gb, args.mem_wait))
            return 5
        beat("waiting for memory (%.1f GB free)" % free_gb())
        time.sleep(20)

    import numpy as np
    import soundfile as sf
    import torch
    torch.set_num_threads(args.threads)
    import compat_dml
    compat_dml.apply()            # no-op except on the old torch of the DirectML test environment
    from diffusers import AceStepPipeline
    import diffusers

    dtype = getattr(torch, args.dtype)
    device = args.device
    if device == "cuda" and not torch.cuda.is_available():
        log("GPU requested but torch.cuda.is_available() is False")
        return 3
    if device == "dml":                      # DirectML test path (separate environment with torch-directml)
        import torch_directml
        dev_name = "DirectML: " + torch_directml.device_name(torch_directml.default_device())
        device = torch_directml.device()
    else:
        dev_name = torch.cuda.get_device_name(0) if device == "cuda" else "CPU (%d threads)" % args.threads
    vae_device = args.vae_device or device
    log("torch %s, diffusers %s, device %s, dtype %s, vae on %s, %.1f GB free" % (
        torch.__version__, diffusers.__version__, dev_name, args.dtype, vae_device, free_gb()))
    beat("loading")
    t0 = time.time()
    try:
        if args.dtype == "float32" and args.device in ("cuda", "cpu"):
            pipe = AceStepPipeline.from_pretrained(args.model, torch_dtype=torch.bfloat16, local_files_only=True)
            for comp, dev in ((pipe.text_encoder, device), (pipe.condition_encoder, device),
                              (pipe.transformer, device), (pipe.vae, vae_device)):
                stream_to(comp, dev, torch)
                beat("loading")
        else:
            pipe = AceStepPipeline.from_pretrained(args.model, torch_dtype=dtype, local_files_only=True)
            pipe = pipe.to(device)
            if vae_device != device:
                pipe.vae.to(vae_device)
            pipe.vae.to(torch.float32)          # decode in float32 on every device
        pipe.set_progress_bar_config(disable=True)
        if hasattr(pipe.vae, "enable_tiling"):
            pipe.vae.enable_tiling()
    except Exception:
        traceback.print_exc()
        return 3
    load_s = time.time() - t0
    log("model loaded in %.0f s, %.1f GB free" % (load_s, free_gb()))

    lat_rate = pipe.latents_per_second
    acoustic_dim = pipe.transformer.config.audio_acoustic_hidden_dim
    nan_count = 0

    def non_finite(tr, seed, stem, what):
        """A render with NaN/Inf is discarded and its seed marked, so the batch goes on with the other seeds.
        Returns an exit code if the run must stop (GPU state bad, or too many such renders), else None."""
        log("BAD OUTPUT for %s seed %d: %s - discarded" % (tr["name"], seed, what))
        if args.device == "cuda":
            bad = canary(device, torch)
            if bad:
                log("GPU canary failed after the bad render (%s)" % bad)
                return 6
        with open(stem + ".nan.json", "w", encoding="utf-8") as f:
            json.dump(dict(track=tr["name"], seed=seed, problem=what, device=dev_name, dtype=args.dtype,
                           created=time.strftime("%Y-%m-%d %H:%M:%S")), f)
        return 4 if nan_count >= 4 else None

    for tr, seed, stem in jobs:
        dur = args.seconds or float(tr["gen_s"])
        n_lat = math.ceil(dur * lat_rate)
        noise = torch.randn((1, n_lat, acoustic_dim), generator=torch.Generator("cpu").manual_seed(seed), dtype=torch.float32)
        log("%s seed %d: %.0f s of audio ..." % (tr["name"], seed, dur))
        state = {"t": time.time(), "steps": []}

        def on_step(p, i, t, kw):
            if args.device == "cuda":
                torch.cuda.synchronize()
                bad_now = canary(device, torch)       # a few milliseconds; stops a wrecked render at once
                if bad_now:
                    raise CanaryError(bad_now)
            now = time.time()
            state["steps"].append(round(now - state["t"], 2))
            state["t"] = now
            beat("%s s%d step %d" % (tr["name"], seed, i + 1))
            return {}

        # The GPU shares the system RAM and fails silently when memory runs out, so every render waits for headroom.
        t_wait = time.time()
        while free_gb() < args.headroom_gb:
            if time.time() - t_wait > args.mem_wait:
                log("only %.1f GB of memory free before %s seed %d - giving the memory back and stopping" % (free_gb(), tr["name"], seed))
                return 5
            beat("waiting for memory (%.1f GB free)" % free_gb())
            time.sleep(20)
        try:
            beat("%s s%d start" % (tr["name"], seed))
            if args.device == "cuda":
                bad = canary(device, torch)
                if bad:
                    log("GPU canary failed before the render (%s)" % bad)
                    return 6
                torch.cuda.reset_peak_memory_stats()
            t1 = time.time()
            with torch.inference_mode():
                lat = pipe(prompt=tr["caption"], lyrics="[Instrumental]", vocal_language="unknown",
                           audio_duration=dur, num_inference_steps=args.steps, shift=args.shift, guidance_scale=1.0,
                           bpm=int(tr["bpm"]), keyscale=tr["key"], timesignature=str(tr["ts"]),
                           latents=noise.to(device=device, dtype=dtype), output_type="latent",
                           callback_on_step_end=on_step).audios
                if args.device == "cuda":
                    torch.cuda.synchronize()
                t2 = time.time()
                if not torch.isfinite(lat.float()).all():
                    raise FloatingPointError("NaN/Inf in the latents")
                beat("%s s%d decoding" % (tr["name"], seed))
                audio = pipe.vae.decode(lat.transpose(1, 2).to(device=vae_device, dtype=torch.float32)).sample
                audio = audio.float().cpu()
                t3 = time.time()
            if args.device == "cuda":
                bad = canary(device, torch)
                if bad:
                    log("GPU canary failed after the render (%s) - output discarded" % bad)
                    return 6
        except CanaryError as e:
            log("GPU canary failed during the render (%s) - output discarded" % e)
            return 6
        except FloatingPointError as e:
            nan_count += 1
            code = non_finite(tr, seed, stem, str(e))
            if code:
                return code
            continue
        except Exception:
            traceback.print_exc()
            log("device failure on %s" % args.device)
            return 3
        a = audio[0].numpy().T.astype(np.float64)              # (samples, 2)
        if not np.isfinite(a).all():
            nan_count += 1
            code = non_finite(tr, seed, stem, "NaN/Inf in the decoded audio")
            if code:
                return code
            continue
        peak = float(np.max(np.abs(a))) or 1.0
        a = a * (10.0 ** (-1.0 / 20.0) / peak)
        sf.write(stem + ".flac", a, pipe.sample_rate, format="FLAC", subtype="PCM_24")
        meta = dict(track=tr["name"], seed=seed, model=args.model_id, model_dir=args.model, seconds=round(len(a) / pipe.sample_rate, 3),
                    sample_rate=pipe.sample_rate, caption=tr["caption"], lyrics="[Instrumental]", bpm=tr["bpm"], key=tr["key"],
                    timesignature=tr["ts"], steps=args.steps, shift=args.shift, device=dev_name, dtype=args.dtype,
                    vae_device=str(vae_device), torch=torch.__version__, diffusers=diffusers.__version__,
                    load_s=round(load_s, 1), denoise_s=round(t2 - t1, 2), decode_s=round(t3 - t2, 2),
                    step_s=state["steps"], compute_per_audio_s=round((t3 - t1) / dur, 4), raw_peak=round(peak, 4),
                    gpu_peak_mem_gb=round(torch.cuda.max_memory_allocated() / 2 ** 30, 2) if args.device == "cuda" else None,
                    created=time.strftime("%Y-%m-%d %H:%M:%S"))
        with open(stem + ".json", "w", encoding="utf-8") as f:
            json.dump(meta, f, indent=1)
        log("  done: denoise %.1f s, decode %.1f s -> %.3f s compute per s of audio" % (t2 - t1, t3 - t2, (t3 - t1) / dur))
    beat("finished")
    return 0


if __name__ == "__main__":
    code = 3
    try:
        code = main()
    except SystemExit as e:
        code = e.code if isinstance(e.code, int) else 2
    except BaseException:
        traceback.print_exc()
    keep_awake(False)
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(code)          # skip interpreter teardown: the ROCm runtime can hang there after an error
