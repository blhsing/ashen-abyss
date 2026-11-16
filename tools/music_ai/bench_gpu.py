"""GPU sanity and speed check for the PyTorch build in this environment (ROCm 'cuda' device or DirectML).

Stage 1 (no model needed): basic maths on the GPU against the CPU - matmul, conv1d, attention, in float32 and
float16; reports max relative error and speed-up.
Stage 2 (needs the VAE / text encoder of the ACE-Step Diffusers repo): decodes the same 10 s of latents on the
CPU and on the GPU and compares the audio.

    python bench_gpu.py [--stage 1|2|all] [--backend cuda|dml] [--threads 10]
Exit code 0 = every check passed, 2 = numerical mismatch, 3 = device error.
"""
import argparse
import os
import sys
import time
import traceback

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

MODEL = os.path.join(os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai")),
                     "models", "acestep-v15-xl-turbo-diffusers")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", default="all")
    ap.add_argument("--backend", default="cuda")
    ap.add_argument("--threads", type=int, default=10)
    args = ap.parse_args()
    import torch
    torch.set_num_threads(args.threads)
    print("torch", torch.__version__, "| threads", torch.get_num_threads())
    if args.backend == "dml":
        import torch_directml
        dev = torch_directml.device()
        print("DirectML device:", torch_directml.device_name(torch_directml.default_device()))
        sync = lambda: None
    else:
        print("cuda available:", torch.cuda.is_available())
        if not torch.cuda.is_available():
            return 3
        dev = torch.device("cuda")
        print("device:", torch.cuda.get_device_name(0))
        free, total = torch.cuda.mem_get_info()
        print("GPU memory: %.1f GB free of %.1f GB" % (free / 2 ** 30, total / 2 ** 30))
        print("HIP/ROCm version:", getattr(torch.version, "hip", None))
        sync = torch.cuda.synchronize
    bad = False

    def timed(fn, reps=3):
        fn()
        sync()
        t = time.perf_counter()
        for _ in range(reps):
            out = fn()
        sync()
        return (time.perf_counter() - t) / reps, out

    try:
        if args.stage in ("1", "all"):
            g = torch.Generator().manual_seed(0)
            a = torch.randn(2048, 2048, generator=g)
            b = torch.randn(2048, 2048, generator=g)
            x = torch.randn(1, 256, 4000, generator=g)
            w = torch.randn(256, 256, 7, generator=g) * 0.02
            q = torch.randn(1, 16, 1500, 64, generator=g)
            for dt in (torch.float32, torch.float16):
                tc, rc = timed(lambda: a @ b)
                tg, rg = timed(lambda: (a.to(dev, dt) @ b.to(dev, dt)))
                err = float(((rg.float().cpu() - rc).abs().max() / rc.abs().max()))
                tol = 1e-3 if dt == torch.float32 else 3e-2
                print("matmul 2048 %-8s cpu %.4f s  gpu %.4f s  x%.1f  rel err %.2e %s" % (str(dt).split(".")[1], tc, tg, tc / tg, err, "OK" if err < tol else "MISMATCH"))
                bad |= not err < tol
                tc, rc = timed(lambda: torch.nn.functional.conv1d(x, w, padding=3))
                xg, wg = x.to(dev, dt), w.to(dev, dt)
                tg, rg = timed(lambda: torch.nn.functional.conv1d(xg, wg, padding=3))
                err = float(((rg.float().cpu() - rc).abs().max() / rc.abs().max()))
                print("conv1d       %-8s cpu %.4f s  gpu %.4f s  x%.1f  rel err %.2e %s" % (str(dt).split(".")[1], tc, tg, tc / tg, err, "OK" if err < tol else "MISMATCH"))
                bad |= not err < tol
                tc, rc = timed(lambda: torch.nn.functional.scaled_dot_product_attention(q, q, q), reps=2)
                qg = q.to(dev, dt)
                tg, rg = timed(lambda: torch.nn.functional.scaled_dot_product_attention(qg, qg, qg), reps=2)
                err = float(((rg.float().cpu() - rc).abs().max() / rc.abs().max()))
                print("attention    %-8s cpu %.4f s  gpu %.4f s  x%.1f  rel err %.2e %s" % (str(dt).split(".")[1], tc, tg, tc / tg, err, "OK" if err < tol else "MISMATCH"))
                bad |= not err < tol
        if args.stage in ("2", "all"):
            import compat_dml
            compat_dml.apply()            # no-op except on the old torch of the DirectML test environment
            from diffusers import AutoencoderOobleck
            vae = AutoencoderOobleck.from_pretrained(os.path.join(MODEL, "vae"), torch_dtype=torch.float32, local_files_only=True).eval()
            lat = torch.randn(1, 64, 250, generator=torch.Generator().manual_seed(1)) * 0.5      # 10 s at 25 latents/s
            with torch.inference_mode():
                t = time.perf_counter()
                ref = vae.decode(lat).sample
                tc = time.perf_counter() - t
                vae.to(dev)
                t = time.perf_counter()
                out = vae.decode(lat.to(dev)).sample
                sync()
                t_first = time.perf_counter() - t
                t = time.perf_counter()
                out = vae.decode(lat.to(dev)).sample.float().cpu()
                sync()
                tg = time.perf_counter() - t
            err = float((out - ref).abs().max() / ref.abs().max())
            corr = float(torch.corrcoef(torch.stack([out.flatten(), ref.flatten()]))[0, 1])
            ok = corr > 0.999 and torch.isfinite(out).all()
            print("VAE decode 10 s: cpu %.2f s, gpu first %.2f s, gpu %.2f s (x%.1f); max rel err %.2e, corr %.6f %s" % (
                tc, t_first, tg, tc / tg, err, corr, "OK" if ok else "MISMATCH"))
            bad |= not ok
    except Exception:
        traceback.print_exc()
        return 3
    print("RESULT:", "numerical mismatch" if bad else "all checks passed")
    return 2 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
