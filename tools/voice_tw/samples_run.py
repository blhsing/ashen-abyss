"""Step 2 of the listening test: synthesise the sample lines with one candidate model (CPU).

    <venvs\\common python> samples_run.py breezy | cosy3 | voxcpm_design | voxcpm_clone

Builds a job file and runs that model's driver in the model's own venv. One model at a time - never
start two of these together. Dry output goes to %VT_ROOT%\\stage\\samples\\<column>\\<key>.wav.

Reference clips (stage\\refs):
  kokoro_<role>.wav  synthetic clips made with Kokoro (mainland pronunciation) - timbre only.
  twb_<role>.wav     the Taiwanese example bundled with the BreezyVoice repo, pitch/formant-shifted
                     per role - timbre AND accent (made by samples_kokoro.py, see samples_spec.TW_SHIFT).
"""
import json
import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # never depend on the console code page
import time

import samples_spec as S
import vt

HERE = os.path.dirname(os.path.abspath(__file__))
THREADS = {"breezy": 6, "cosy": 6, "voxcpm": 8}     # keeps the observed load within 8 cores, see cpu_cap.py


def run_driver(venv: str, script: str, jobs: list[dict], tag: str) -> list[dict]:
    jf = os.path.join(vt.ROOT, "stage", f"jobs_{tag}.json")
    os.makedirs(os.path.dirname(jf), exist_ok=True)
    json.dump(jobs, open(jf, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    py = os.path.join(vt.ROOT, "venvs", venv, "Scripts", "python.exe")
    log = open(os.path.join(vt.ROOT, "logs", f"run_{tag}.log"), "w", encoding="utf-8")
    t0 = time.time()
    env = dict(os.environ, VT_THREADS=str(THREADS.get(venv, 8)), PYTHONUTF8="1")
    p = subprocess.Popen([py, os.path.join(HERE, script), jf], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", env=env)
    rows = []
    for line in p.stdout:
        log.write(line); log.flush()
        if line.startswith("{"):
            try:
                rows.append(json.loads(line))
                print(line.strip()[:400], flush=True)
            except json.JSONDecodeError:
                pass
    p.wait()
    log.close()
    print(f"[{tag}] exit {p.returncode}, {time.time() - t0:.0f} s wall", flush=True)
    if p.returncode != 0:
        print(open(log.name, encoding="utf-8").read()[-3000:])
    return rows


def save_timing(column: str, rows: list[dict]):
    d = os.path.join(S.STAGE, column)
    mine = [r for r in rows if "out" in r and os.path.normcase(os.path.dirname(r["out"])) == os.path.normcase(d)]
    if mine:
        os.makedirs(d, exist_ok=True)
        json.dump(dict(rows=mine), open(os.path.join(d, "timing.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def dry_samples():
    texts = S.line_texts()
    return [(s, texts[s["key"]]) for s in S.SAMPLES if not s.get("same_as")]


def tw_ref(v: str) -> dict:
    return dict(ref_wav=os.path.join(S.REFS, f"twb_{v}.wav"), ref_text=S.BUNDLED_TEXT)


def breezy():
    jobs = []
    # column "breezyvoice": the model's own phonetics (LM unprompted), timbre from a synthetic Kokoro clip
    for s, text in dry_samples():
        v = s["voice"]
        jobs.append(dict(out=os.path.join(S.STAGE, "breezyvoice", s["key"] + ".wav"), text=text, mode="native",
                         ref_wav=os.path.join(S.REFS, f"kokoro_{v}.wav"), ref_text=S.REF_TEXT, seed=1))
    # column "breezyvoice_tw": official zero-shot mode from the bundled Taiwanese example (shifted per role)
    for s, text in dry_samples():
        jobs.append(dict(out=os.path.join(S.STAGE, "breezyvoice_tw", s["key"] + ".wav"), text=text, mode="zero_shot",
                         seed=1, **tw_ref(s["voice"])))
    rows = run_driver("breezy", "synth_breezy.py", jobs, "breezy")
    save_timing("breezyvoice", rows)
    save_timing("breezyvoice_tw", rows)


def cosy3():
    jobs = [dict(out=os.path.join(S.STAGE, "cosyvoice3", s["key"] + ".wav"), text=text, seed=1, speed=1.0, **tw_ref(s["voice"]))
            for s, text in dry_samples()]
    save_timing("cosyvoice3", run_driver("cosy", "synth_cosy3.py", jobs, "cosy3"))


def voxcpm(mode):
    jobs = []
    for s, text in dry_samples():
        j = dict(out=os.path.join(S.STAGE, f"voxcpm2_{mode}", s["key"] + ".wav"), text=text, voice=s["voice"], mode=mode, seed=1)
        if mode == "clone":
            j.update(tw_ref(s["voice"]))
        jobs.append(j)
    save_timing(f"voxcpm2_{mode}", run_driver("voxcpm", "synth_voxcpm2.py", jobs, f"voxcpm_{mode}"))


def main():
    what = sys.argv[1]
    if what == "breezy":
        breezy()
    elif what == "cosy3":
        cosy3()
    elif what in ("voxcpm_design", "voxcpm_clone"):
        voxcpm(what.split("_")[1])
    else:
        raise SystemExit("unknown model " + what)


if __name__ == "__main__":
    main()
