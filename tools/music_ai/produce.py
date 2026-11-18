"""Resumable batch: generate candidates -> measure and score -> select -> loop, master, encode.

    python produce.py --tracks all|pilot|name[,name...] [--device cuda] [--no-fallback] [--only-eval]

Everything lands in the staging folder (default %MA_ROOT%\\stage\\ace_xl):
    cand\\<track>__s<seed>.flac/.json     raw candidates (48 kHz) and how they were made
    cand\\<track>__s<seed>.eval.json      measurements and audio-text scores (cached)
    final\\<track>.ogg                    mastered delivery candidate (44.1 kHz OGG Vorbis q0.6)
    final\\<track>.json                   which seed won and why, loop/seam/master numbers, pass/fail
    report.json / report.txt             the whole table
Nothing is copied into the game here; deliver.py does that for tracks whose final JSON says "passed".

Safety: one batch at a time (lock file), a keep-awake request is held for the whole run, the generator runs
as one child process at a time, and if the GPU path fails (non-zero exit, or no heartbeat for --hang seconds)
the GPU is not used again in this run and the remaining candidates are rendered on the CPU.
"""
import argparse
import ctypes
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tracks as T  # noqa: E402

MA_ROOT = os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai"))
CPU_PYTHON = os.path.join(MA_ROOT, "venvs", "cpu", "Scripts", "python.exe")
CANARY_RETRIES = [20]            # per run: how often a silent-corruption failure (memory pressure) may be retried on the GPU
DEFAULT_GPU_DTYPE = "bfloat16"   # see GPU_NOTES.md: fastest path that passed the checks on the Radeon 8060S
EXTRA_SEED_ROUNDS = 2          # if no candidate passes, try this many more rounds of 2 new seeds
LOGF = None


def log(msg):
    line = time.strftime("%Y-%m-%d %H:%M:%S ") + msg
    print(line, flush=True)
    if LOGF:
        with open(LOGF, "a", encoding="utf-8") as f:
            f.write(line + "\n")


RUNNING = os.path.join(HERE, "RUNNING.txt")
_running = {}


def running_note(stage=None, child=None, clear=False):
    """RUNNING.txt (next to this script) tells the lead what is running and how to stop it; it is removed when
    the batch ends."""
    if clear:
        try:
            os.remove(RUNNING)
        except OSError:
            pass
        return
    if stage:
        _running.update(stage=stage, started=time.strftime("%Y-%m-%d %H:%M:%S"))
    _running["child"] = child
    lines = ["music_ai generation batch is running",
             "batch process (produce.py) pid: %d" % os.getpid(),
             "generator process (gen_ace.py) pid: %s" % (child or "-"),
             "started: %s" % _running.get("started"),
             "log: %s   (generator output: %s)" % (os.path.join(_running.get("stage") or "", "produce.log"),
                                                  os.path.join(_running.get("stage") or "", "gen.log")),
             "stop it with:  taskkill /PID %d /T /F" % os.getpid() + ("   and   taskkill /PID %d /T /F" % child if child else ""),
             "or, by name:   Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'python.exe' -and $_.CommandLine -match 'music_ai.(produce|gen_ace).py' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }",
             "The batch is resumable: start it again with run_batch.ps1 and it continues with what is missing."]
    with open(RUNNING, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def keep_awake(on):
    try:
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000000 | (0x00000001 if on else 0))
    except Exception:
        pass


def single_instance(path):
    import msvcrt
    fh = open(path, "a+")
    try:
        fh.seek(0)
        msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1)
    except OSError:
        raise SystemExit("another generation batch is already running (lock %s)" % path)
    return fh


# ------------------------------------------------------------------------------------------- generation
def run_generator(names, seeds_by_track, cand_dir, device, dtype, hang_s, gen_log, model_args=()):
    """Run gen_ace.py as a child for the missing candidates (one child at a time).
    Returns "ok", "memory" (not enough free RAM even after waiting - not a device problem) or "failed"."""
    todo = {}
    for n in names:
        miss = [s for s in seeds_by_track[n] if not os.path.exists(os.path.join(cand_dir, "%s__s%d.flac" % (n, s)))
                and not os.path.exists(os.path.join(cand_dir, "%s__s%d.nan.json" % (n, s)))]
        if miss:
            todo[n] = miss
    if not todo:
        return "ok"
    hb = os.path.join(os.path.dirname(cand_dir), "heartbeat.txt")
    # Tracks that only need their standard seeds go into ONE child (the model is loaded once; gen_ace.py skips
    # what is already on disk); tracks with extra seeds get a child per distinct seed list.
    groups = {}
    for n, seeds in todo.items():
        std = seeds_by_track[n] == T.BY_NAME[n]["seeds"]
        groups.setdefault("" if std else ",".join(map(str, seeds)), []).append(n)
    for seeds, tracks_ in groups.items():
        # AMD's ROCm torch build has no fast CPU BLAS, so CPU rendering uses the separate "cpu" environment
        exe = CPU_PYTHON if (device == "cpu" and os.path.exists(CPU_PYTHON)) else sys.executable
        cmd = [exe, os.path.join(HERE, "gen_ace.py"), "--out", cand_dir, "--tracks", ",".join(tracks_),
               "--device", device, "--dtype", dtype, "--heartbeat", hb] + list(model_args)
        if seeds:
            cmd += ["--seeds", seeds]
        for attempt in range(1, 7):
            log("generator (%s, %s): %s seeds %s" % (device, dtype, ",".join(tracks_), seeds or "standard"))
            with open(hb, "w") as f:
                f.write("%d spawn" % time.time())
            with open(gen_log, "a", encoding="utf-8") as lf:
                p = subprocess.Popen(cmd, stdout=lf, stderr=subprocess.STDOUT)
                running_note(child=p.pid)
                while p.poll() is None:
                    time.sleep(5)
                    try:
                        age = time.time() - os.path.getmtime(hb)
                    except OSError:
                        age = 0
                    if age > hang_s:
                        log("no heartbeat for %.0f s -> killing the generator (treated as a hang)" % age)
                        subprocess.run(["taskkill", "/PID", str(p.pid), "/T", "/F"], capture_output=True)   # whole tree
                        p.kill()
                        p.wait()
                        running_note(child=None)
                        return "failed"
            running_note(child=None)
            if p.returncode == 0:
                break
            if p.returncode == 5:           # memory never became free: other jobs are using it; try again later
                log("generator: not enough free memory (attempt %d) - waiting 5 minutes" % attempt)
                time.sleep(300)
                continue
            if p.returncode == 6 and CANARY_RETRIES[0] > 0:
                # wrong results without an error: seen when the shared-memory GPU is over-committed. A fresh
                # process works again, so the GPU gets a limited number of second chances per run.
                CANARY_RETRIES[0] -= 1
                log("generator: GPU canary failed, output discarded - retrying on the GPU in 30 s (%d retries left)" % CANARY_RETRIES[0])
                time.sleep(30)
                continue
            log("generator exited with code %s" % p.returncode)
            return "failed"
        else:
            return "memory"
    return "ok"


# ------------------------------------------------------------------------------------------- evaluation
_scorer = None
SCORER_DEVICE = None           # None = GPU if available; set to "cpu" once the GPU path has failed in this run


def word_check(flac):
    """Speech recogniser over one file (CPU environment, cached). None if the check cannot run."""
    out = flac[:-5] + ".words.json"
    if not os.path.exists(out):
        script = os.path.join(HERE, "speech_check.py")
        if not os.path.exists(CPU_PYTHON) or not os.path.isdir(os.path.join(MA_ROOT, "models", "whisper-small")):
            return None
        try:
            subprocess.run([CPU_PYTHON, script, "--quiet", "--json", out, flac], timeout=900, check=True,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except Exception as e:
            log("speech check failed for %s: %s" % (os.path.basename(flac), e))
            return None
    with open(out, encoding="utf-8") as f:
        r = list(json.load(f).values())[0]
    return dict(windows=r["windows"], word_windows=r["word_windows"],
                texts=[d["text"] for d in r["detail"] if d["real"]][:3],
                min_no_speech=min([d["no_speech"] for d in r["detail"]] or [1.0]))


_baseline = {}


def baseline_flags(tr):
    """Flag levels (and text match) of the current procedural version of the same piece, for comparison."""
    n = tr["name"]
    if n in _baseline:
        return _baseline[n]
    import audiolib as al
    cache = os.path.join(MA_ROOT, "stage", "baseline_eval")
    os.makedirs(cache, exist_ok=True)
    out = os.path.join(cache, n + ".json")
    if os.path.exists(out):
        with open(out, encoding="utf-8") as f:
            _baseline[n] = json.load(f)
        return _baseline[n]
    src = os.path.join(r"Y:\music_backup\procedural", n + ".ogg")
    res = {}
    if os.path.exists(src):
        c = clap_scores(src, tr)
        res = dict(sim=c["sim"], **{k: v["bad_mean"] for k, v in c["flags"].items()})
    with open(out, "w", encoding="utf-8") as f:
        json.dump(res, f)
    _baseline[n] = res
    return res


def clap_scores(path, tr):
    """Audio-text scores for one file: in this process on the GPU, or - once the GPU is out of use - in a child
    process in the "cpu" environment (the ROCm build of torch is far too slow on the CPU)."""
    if SCORER_DEVICE == "cpu" and os.path.exists(CPU_PYTHON):
        out = path + ".clap.tmp.json"
        subprocess.run([CPU_PYTHON, os.path.join(HERE, "clap_score.py"), "--track", tr["name"], "--json", out, path],
                       timeout=1800, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        with open(out, encoding="utf-8") as f:
            res = json.load(f)
        os.remove(out)
        return res
    import audiolib as al
    x, sr = al.read_audio(path)
    return _get_scorer().score(x, sr, tr["describe"], T.FLAG_SETS)


def _get_scorer():
    global _scorer
    if _scorer is None:
        from clap_score import ClapScorer
        _scorer = ClapScorer(device=SCORER_DEVICE)
    return _scorer


STRICT_PLR_DB = 9.0     # true peak minus integrated loudness of the delivered file
STRICT_LRA_LU = 1.5     # loudness range of the delivered file


def _key_pc_of(name):
    import audiolib as al
    return al.key_pc(name)


def evaluate(flac, tr, force=False):
    import audiolib as al
    out = flac[:-5] + ".eval.json"
    if os.path.exists(out) and not force:
        with open(out, encoding="utf-8") as f:
            return json.load(f)
    x, sr = al.read_audio(flac)
    m = al.measure(x, sr, bpm=tr["bpm"], pulse=tr["pulse"], want_s=None)
    c = clap_scores(flac, tr)
    fl = c["flags"]
    rej = list(m["reject"])
    # Unwanted content. Zero-shot labels are noisy (the procedural sands_combat itself reads as "jazz, playful"),
    # so genre and mood only reject a candidate that is flagged AND flagged worse than the procedural baseline.
    base = baseline_flags(tr)
    # Voices: the audio-text model also calls hard percussion "rap vocals", so a suspicion is confirmed with a
    # speech recogniser before a candidate is rejected for it (calibration: tests/test_speech.py, README).
    words = None
    if fl["vocal"]["bad_mean"] > 0.30 or fl["vocal"]["bad_windows"] > 0.2 * c["windows"]:
        words = word_check(flac)
        if words is None:
            if fl["vocal"]["bad_mean"] > 0.45:
                rej.append("sung lyrics / speech suspected (not confirmed: speech check unavailable)")
        elif words["word_windows"] >= 1:
            rej.append("sung or spoken words detected")
    if fl["genre"]["bad_mean"] > max(0.5, base.get("genre", 0.0)):
        rej.append("genre drift (%s)" % fl["genre"]["top"])
    if fl["mood"]["bad_mean"] > max(0.5, base.get("mood", 0.0)):
        rej.append("bright / comedic mood")
    score = 100.0 * c["sim"] - 25.0 * fl["vocal"]["bad_mean"] - 12.0 * fl["genre"]["bad_mean"] - 12.0 * fl["mood"]["bad_mean"]
    if tr["pulse"]:
        score += 12.0 * min(max(m["pulse_near"], m["pulse_global"]), 0.5) - 150.0 * m["tempo_err"]
    score -= 0.5 * max(0.0, m["resonance_db"] - 12.0)
    # small preference for the requested tonal centre and mode (all pieces are asked for in minor)
    want_pc, want_mode = al.key_pc(tr["key"]), tr["key"].split()[1]
    m["key_match"] = bool(m["key_pc"] == want_pc)
    m["mode_match"] = bool(m["key_est"].split()[1] == want_mode)
    score += (1.5 if m["key_match"] else 0.0) + (1.5 if m["mode_match"] else 0.0)
    # the VAE decodes long audio in tiles joined without a crossfade (every 384 latent frames = 15.36 s)
    m["tile_click_db"] = al.boundary_clicks(x, sr, 384 / 25.0)
    m["tile_step"] = al.boundary_steps(x, int(round(384 / 25.0 * sr)), sr)
    if m["tile_step"] > 1.5:
        rej.append("step at a decoder tile boundary")
    res = dict(file=os.path.basename(flac), score=round(score, 2), reject=rej, clap=c, measure=m, words=words)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(res, f, indent=1)
    return res


def finalize(tr, flac, ev, final_dir, model_name):
    """Loop (or trim), master and encode the chosen candidate; verify the encoded file."""
    import numpy as np
    import audiolib as al
    x, sr = al.read_audio(flac)
    info = dict(track=tr["name"], source=os.path.basename(flac), model=model_name, loop=tr["loop_s"] is not None)
    if tr["loop_s"]:
        lp = al.find_loop(x, sr, tr["bpm"], tr["ts"], tr["loop_s"], tr["pulse"])
        y, bi = al.build_loop(x, sr, lp["start"], lp["length"], lp["xfade"])
        info["loop_window"] = lp
        info["loop_build"] = bi
        info["seam_raw"] = al.seam_metrics(y, sr)
        out, mi = al.master(y, sr, tr["lufs"], True)
    else:
        a, b = al.trim_silence(x, sr)          # keep the whole piece, only strip silence at the ends
        y = x[int(a * sr):int(b * sr)].copy()
        n = len(y)
        # narration bed: a soft swell over the last seconds, then a fade to silence
        swell_s, fade_s = 12.0, 4.0
        env = np.ones(n)
        k0, k1 = n - int((swell_s + fade_s) * sr), n - int(fade_s * sr)
        env[k0:k1] = 10.0 ** (np.linspace(0.0, 5.0, k1 - k0) / 20.0)
        env[k1:] = 10.0 ** (5.0 / 20.0)
        y *= env[:, None]
        info["shape"] = dict(swell_db=5.0, swell_s=swell_s, fade_out_s=fade_s)
        out, mi = al.master(y, sr, tr["lufs"], False, fade_in_s=1.5, fade_out_s=fade_s)
    info["master"] = mi
    ogg = os.path.join(final_dir, tr["name"] + ".ogg")
    al.write_ogg(ogg, out, 44100, 0.6)
    d, dsr = al.read_audio(ogg)
    chk = al.measure(d, dsr, bpm=tr["bpm"], pulse=tr["pulse"])
    info["ogg"] = dict(bytes=os.path.getsize(ogg), samples=len(d), sample_rate=dsr, seconds=round(len(d) / dsr, 3),
                       kbps=round(os.path.getsize(ogg) * 8 / (len(d) / dsr) / 1000.0, 1), lufs=chk["lufs"],
                       peak_db=chk["peak_db"], true_peak_db=round(al.true_peak_db(d, dsr), 2), dc=chk["dc"],
                       clip_frac=chk["clip_frac"], tempo=chk["tempo"], tempo_err=chk["tempo_err"], key_est=chk["key_est"],
                       length_exact=bool(len(d) == len(out)))
    problems = []
    if tr["loop_s"]:
        info["seam_ogg"] = al.seam_metrics(d, dsr)
        s = info["seam_ogg"]
        # hf_click_db is only reported: bar-aligned loops start on a beat, so a drum hit sits right at the seam and
        # looks like a "click" to that measure. A real click is a sample step, which jump_vs_p999 catches.
        if s["jump_vs_p999"] > 1.0:
            problems.append("seam discontinuity")
        # level_step_db (the second after the seam against the second before it) is only reported: the first
        # second after the wrap is the original continuation of the last second, so a difference there is the
        # music's own dynamics (a bell stroke, a swell), not a defect of the loop.
        lo, hi = tr["loop_s"]
        if not (lo - 15 <= len(d) / dsr <= hi + 5):
            problems.append("loop length out of range")
    if abs(chk["lufs"] - tr["lufs"]) > 0.6:
        problems.append("loudness off target")
    if info["ogg"]["true_peak_db"] > -0.1 or chk["clip_frac"] > 1e-4:
        problems.append("clipping after encode")
    if not info["ogg"]["length_exact"]:
        problems.append("decoded length differs")
    if chk["dropouts"] or chk["zero_runs"]:
        problems.append("dropout in final")
    if tr.get("strict"):
        # the stricter bar for the last four pieces (tracks.py): minor mode, the tonic of the exploration track,
        # dynamics that are not flat. Thresholds: about the middle of the combat tracks already in the game
        # (PLR 7.9-10.2 dB, LRA 1.0-11.8 LU; tests/dyn_probe.py).
        dyn = al.dynamics(d, dsr)
        info["dynamics"] = dyn
        if chk["key_est"].split()[1] != "minor":
            problems.append("not in a minor mode (%s)" % chk["key_est"])
        want = ev.get("strict_pc")
        if want is not None and al.key_pc(chk["key_est"]) != want:
            problems.append("tonic %s differs from its exploration track's" % chk["key_est"].split()[0])
        if dyn["plr"] < STRICT_PLR_DB:
            problems.append("flat dynamics (PLR %.1f dB < %.1f)" % (dyn["plr"], STRICT_PLR_DB))
        if dyn["lra"] < STRICT_LRA_LU:
            problems.append("flat dynamics (LRA %.1f LU < %.1f)" % (dyn["lra"], STRICT_LRA_LU))
    info["problems"] = problems
    return info


def main():
    global LOGF, SCORER_DEVICE
    ap = argparse.ArgumentParser()
    ap.add_argument("--tracks", default="all")
    ap.add_argument("--stage", default=os.path.join(MA_ROOT, "stage", "ace_xl"))
    ap.add_argument("--device", default="cuda")
    ap.add_argument("--dtype", default=DEFAULT_GPU_DTYPE, help="GPU precision (the CPU fallback always uses float32)")
    ap.add_argument("--no-fallback", action="store_true", help="do not fall back to the CPU if the GPU path fails")
    ap.add_argument("--hang", type=float, default=420.0, help="seconds without a heartbeat before the child is killed")
    ap.add_argument("--model-dir", default="", help="Diffusers-format ACE-Step model folder (default: XL turbo)")
    ap.add_argument("--model-id", default="", help="model name recorded in the metadata")
    ap.add_argument("--only-eval", action="store_true", help="no generation: evaluate / select / master what exists")
    ap.add_argument("--redo-final", action="store_true")
    ap.add_argument("--first-seeds", type=int, default=0, help="use only the first N standard seeds of each piece (fewer candidates, e.g. on the CPU)")
    ap.add_argument("--max-seeds", type=int, default=0, help="use only the last N standard seeds of each piece (quick comparisons)")
    ap.add_argument("--chunk", type=int, default=0, help="work through the tracks N at a time (generate, select, master), so finished pieces exist early")
    ap.add_argument("--deliver", action="store_true", help="after every chunk copy the passing pieces into the game (deliver.py)")
    args = ap.parse_args()

    names = T.PILOT if args.tracks == "pilot" else [t["name"] for t in T.TRACKS] if args.tracks == "all" \
        else [n for n in args.tracks.split(",") if n]
    cand_dir = os.path.join(args.stage, "cand")
    final_dir = os.path.join(args.stage, "final")
    os.makedirs(cand_dir, exist_ok=True)
    os.makedirs(final_dir, exist_ok=True)
    LOGF = os.path.join(args.stage, "produce.log")
    _lock = single_instance(os.path.join(MA_ROOT, "stage", "generation.lock"))
    keep_awake(True)
    import atexit
    running_note(stage=args.stage)
    atexit.register(running_note, clear=True)
    log("=== batch start: %s (device %s)" % (",".join(names), args.device))
    from gen_ace import MODEL_ID
    model_args = []
    if args.model_dir:
        model_args += ["--model", args.model_dir]
    if args.model_id:
        model_args += ["--model-id", args.model_id]
        MODEL_ID = args.model_id
    gen_log = os.path.join(args.stage, "gen.log")
    device = args.device
    seeds = {n: list(T.BY_NAME[n]["seeds"]) for n in names}
    if args.max_seeds > 0:
        seeds = {n: v[-args.max_seeds:] for n, v in seeds.items()}
    if args.first_seeds > 0:        # never drops candidates that already exist on disk
        seeds = {n: [s_ for i, s_ in enumerate(v) if i < args.first_seeds
                     or os.path.exists(os.path.join(cand_dir, "%s__s%d.flac" % (n, s_)))] for n, v in seeds.items()}
    if args.device == "cpu":
        SCORER_DEVICE = "cpu"
    state_path = os.path.join(args.stage, "extra_seeds.json")
    if os.path.exists(state_path):
        with open(state_path, encoding="utf-8") as f:
            for n, extra in json.load(f).items():
                if n in seeds:
                    seeds[n] += [s for s in extra if s not in seeds[n]]

    report = {}
    all_names = names
    step = args.chunk if args.chunk > 0 else len(all_names)
    for c0 in range(0, len(all_names), step):
        names = all_names[c0:c0 + step]
        if step < len(all_names):
            log("--- chunk %d/%d: %s" % (c0 // step + 1, -(-len(all_names) // step), ",".join(names)))
        for rnd in range(EXTRA_SEED_ROUNDS + 1):
            pending = [n for n in names if n not in report or not report[n]["passed"]]
            if not pending:
                break
            if not args.only_eval:
                res = run_generator(pending, seeds, cand_dir, device, args.dtype if device != "cpu" else "float32",
                                    args.hang if device != "cpu" else 3600.0, gen_log, model_args)
                if res == "memory":
                    log("generation stopped: the machine never had enough free memory (re-run the batch later)")
                elif res == "failed" and device != "cpu":
                    if args.no_fallback:
                        log("GPU path failed and fallback is disabled -> stopping generation")
                    else:
                        log("GPU path failed -> the GPU is not used again in this run; falling back to the CPU")
                        device = "cpu"
                        SCORER_DEVICE = "cpu"
                        run_generator(pending, seeds, cand_dir, device, "float32", 3600.0, gen_log, model_args)
            # pairs: pick the exploration track first so the combat choice can prefer the same tonal centre
            order = sorted(pending, key=lambda n: (T.BY_NAME[n]["pair"] or n, not n.endswith("_base")))
            for n in order:
                tr = T.BY_NAME[n]
                evs = []
                for s in seeds[n]:
                    flac = os.path.join(cand_dir, "%s__s%d.flac" % (n, s))
                    if os.path.exists(flac):
                        ev = evaluate(flac, tr)
                        ev["seed"] = s
                        evs.append(ev)
                if not evs:
                    report[n] = dict(passed=False, reason="no candidates were generated", candidates=[])
                    continue
                for ev in evs:
                    ev["pick"] = ev["score"]
                    want_pc = None
                    if tr["pair"] and n.endswith("_combat"):
                        base = report.get(tr["pair"] + "_base") or _load_final(final_dir, tr["pair"] + "_base")
                        if base and base.get("key_pc") is not None:
                            want_pc = base["key_pc"]
                    if tr.get("strict"):
                        # the combat track must share the tonic of the exploration track that is in the game
                        # (a passing generated one, else the procedural one, which is in the requested key)
                        ev["strict_pc"] = None
                        if tr["pair"] and n.endswith("_combat"):
                            b = report.get(tr["pair"] + "_base") or _load_final(final_dir, tr["pair"] + "_base")
                            ev["strict_pc"] = b["key_pc"] if b and b.get("passed") and b.get("key_pc") is not None else _key_pc_of(T.BY_NAME[tr["pair"] + "_base"]["key"])
                    if want_pc is not None:
                        d = (ev["measure"]["key_pc"] - want_pc) % 12
                        ev["pair_interval"] = d
                        ev["pick"] += 4.0 if d == 0 else 2.0 if d in (3, 9, 5, 7) else 0.0   # same centre / relative / fifth
                good = [e for e in evs if not e["reject"]]
                best = max(good or evs, key=lambda e: e["pick"])
                cands = [dict(seed=e["seed"], score=e["score"], pick=round(e["pick"], 2), sim=e["clap"]["sim"],
                              reject=e["reject"], tempo=e["measure"]["tempo"], key=e["measure"]["key_est"]) for e in evs]
                if not good:
                    report[n] = dict(passed=False, reason="every candidate was rejected", candidates=cands, seed=best["seed"])
                    log("%s: all %d candidates rejected: %s" % (n, len(evs), "; ".join(",".join(e["reject"]) for e in evs)))
                    continue
                fj = os.path.join(final_dir, n + ".json")
                prev = _load_final(final_dir, n)
                if prev and prev.get("passed") and not args.redo_final and len(prev.get("candidates", [])) == len(cands) \
                        and os.path.exists(os.path.join(final_dir, n + ".ogg")):
                    report[n] = prev                 # nothing new since the last run
                    continue
                # best-scoring candidate first; if its loop / master does not pass, try the next one
                info = None
                for cand in sorted(good, key=lambda e: e["pick"], reverse=True):
                    flac = os.path.join(cand_dir, "%s__s%d.flac" % (n, cand["seed"]))
                    if cand.get("words") is None:          # every piece that may ship gets the speech check
                        cand["words"] = word_check(flac)
                        if cand["words"] and cand["words"]["word_windows"] >= 1:
                            cand["reject"].append("sung or spoken words detected")
                            log("%s: seed %d rejected: words detected %r" % (n, cand["seed"], cand["words"]["texts"]))
                            continue
                    trial = finalize(tr, flac, cand, final_dir, MODEL_ID)
                    if info is None or not trial["problems"]:
                        info, best = trial, cand
                    if not trial["problems"]:
                        break
                    log("%s: seed %d failed after mastering: %s" % (n, cand["seed"], "; ".join(trial["problems"])))
                if info is None:
                    report[n] = dict(passed=False, reason="every candidate was rejected (words detected)", candidates=cands, seed=best["seed"])
                    continue
                if info["source"] != "%s__s%d.flac" % (n, best["seed"]) or info["problems"]:
                    # the OGG on disk belongs to the last trial: render the kept candidate again
                    info = finalize(tr, os.path.join(cand_dir, "%s__s%d.flac" % (n, best["seed"])), best, final_dir, MODEL_ID)
                info.update(seed=best["seed"], score=best["score"], clap_sim=best["clap"]["sim"], flags={k: v["bad_mean"] for k, v in best["clap"]["flags"].items()},
                            words=best.get("words"),
                            key_pc=best["measure"]["key_pc"], key_est=best["measure"]["key_est"], bpm_target=tr["bpm"],
                            tempo=best["measure"]["tempo"], tempo_err=best["measure"]["tempo_err"], pulse=tr["pulse"],
                            perc_ratio=best["measure"]["perc_ratio"], candidates=cands, passed=not info["problems"],
                            reason="; ".join(info["problems"]))
                with open(fj, "w", encoding="utf-8") as f:
                    json.dump(info, f, indent=1)
                report[n] = info
                log("%s: seed %d, score %.1f, sim %.3f, %.1f s, tempo %.1f, %s" % (
                    n, best["seed"], best["score"], best["clap"]["sim"], info["ogg"]["seconds"], info["ogg"]["tempo"],
                    "PASSED" if info["passed"] else "FAILED: " + info["reason"]))
            failed = [n for n in names if not report[n]["passed"]]
            if not failed or args.only_eval or rnd == EXTRA_SEED_ROUNDS:
                break
            for n in failed:                       # two fresh seeds for the next round
                base = max(seeds[n]) if seeds[n] else 9000
                seeds[n] += [base + 101, base + 202]
            with open(state_path, "w", encoding="utf-8") as f:
                json.dump(seeds, f)
            log("extra seeds for: %s" % ", ".join(failed))

        write_report(args.stage, all_names, report)
        if args.deliver and not args.model_dir:
            try:
                import deliver
                deliver.run(args.stage, False, log)
            except Exception as e:
                log("deliver failed: %s: %s" % (type(e).__name__, e))
    names = all_names
    log("=== batch end: %d/%d passed" % (sum(1 for n in names if report.get(n, {}).get("passed")), len(names)))
    keep_awake(False)


def write_report(stage, names, report):
    with open(os.path.join(stage, "report.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    lines = ["%-15s %-6s %5s %7s %6s %6s %7s %-10s %s" % ("track", "pass", "seed", "sec", "tempo", "sim", "seam", "key", "notes")]
    for n in names:
        if n not in report:
            continue
        r = report[n]
        if "ogg" in r:
            seam = r.get("seam_ogg", {}).get("jump_vs_p999", "-")
            lines.append("%-15s %-6s %5d %7.2f %6.1f %6.3f %7s %-10s %s" % (
                n, "yes" if r["passed"] else "NO", r["seed"], r["ogg"]["seconds"], r["ogg"]["tempo"], r["clap_sim"], seam,
                r["key_est"], r.get("reason", "")))
        else:
            lines.append("%-15s %-6s %s" % (n, "NO", r.get("reason", "")))
    with open(os.path.join(stage, "report.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def _load_final(final_dir, name):
    p = os.path.join(final_dir, name + ".json")
    if os.path.exists(p):
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    return None


if __name__ == "__main__":
    main()
