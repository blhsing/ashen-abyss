"""Image generation through the user's existing Codex (ChatGPT) login -- no API key, nothing installed.

Uses the Codex CLI that ships with the Codex desktop app and the built-in image tool of its `$imagegen` skill
(official doc: learn.chatgpt.com/codex/image-generation).  Run with Blender's bundled Python:

  PY = "C:\\Program Files\\Blender Foundation\\Blender 5.2\\5.2\\python\\bin\\python.exe"
  PY -I Y:\\tools\\icons\\imagegen.py sheets [job id ...]     icon sheets defined in Y:\\tools\\icons\\gen_jobs.json
  PY -I Y:\\tools\\icons\\imagegen.py one <out.png> "<picture description>"   any single picture (frames, emblems ...)

Every result is cached: a sheet that already exists in Y:\\assets_src\\icons_gen\\sheets is never generated again
(delete the PNG to redo it).  Next to each PNG a JSON keeps the full prompt, date, CLI version and Codex thread id.
One call takes about 90 s and counts toward the user's Codex allowance, so calls are made one at a time with a pause.
Only the picture description is sent: Codex runs in an empty folder, read-only, as an ephemeral session.
"""
import sys, os, json, glob, shutil, subprocess, time, datetime

GEN = r"Y:\assets_src\icons_gen"
JOBS = r"Y:\tools\icons\gen_jobs.json"
HOME = os.path.expanduser("~")
WORK_CANDIDATES = [
    os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "icons_gen_work"),
    os.path.join(os.environ.get("LOCALAPPDATA", HOME), "AshenAbyssTools", "icons_gen_work"),
]
PAUSE = 6          # seconds between calls

SHEET_PREFIX = (
    "$imagegen Generate exactly one square image and do nothing else (no files, no shell commands). "
    "Asset type: sprite sheet of nine game icons. The image is a 3 by 3 grid of nine separate square icons on a pure "
    "black background with wide black gaps between the cells; nothing may cross from one cell into another. Each icon is "
    "one centred subject filling about 80 percent of its cell, a realistic dark-fantasy 3D render with physically real "
    "materials (worn steel, leather, bone, cloth, stone, glowing ember), warm key light from the upper left and a cold "
    "blue rim light from the right, strong readable silhouette, saturated colour only in the magical effect. "
    "No text, no letters, no numbers, no borders, no frames, no watermark. ")


def codex_exe():
    c = sorted(glob.glob(os.path.join(HOME, "AppData", "Local", "OpenAI", "Codex", "bin", "*", "codex.exe")), key=os.path.getmtime)
    if not c:
        raise SystemExit("codex.exe not found under %LOCALAPPDATA%\\OpenAI\\Codex\\bin (is the Codex desktop app installed?)")
    return c[-1]


def workdir():
    for w in WORK_CANDIDATES:
        try:
            os.makedirs(os.path.join(w, "empty"), exist_ok=True)
            return w
        except Exception:
            continue
    raise SystemExit("no work folder")


def sheet_prompt(job):
    s = job["subjects"]
    g = int(job.get("grid", 3))
    rows = []
    for r in range(g):
        rows.append("Row %d: " % (r + 1) + "; ".join("(%d) %s" % (r * g + c + 1, s[r * g + c]) for c in range(g)) + ".")
    return SHEET_PREFIX + job.get("extra", "") + " ".join(rows)


def generate(prompt, out_png, images=()):
    """one Codex call; returns (ok, info dict). The picture is copied to out_png."""
    exe = codex_exe(); wd = workdir()
    cmd = [exe, "exec", "--skip-git-repo-check", "--ephemeral", "-s", "read-only", "-C", os.path.join(wd, "empty"), "--json"]
    for im in images:
        cmd += ["-i", im]
    cmd.append(prompt)
    t0 = time.time()
    p = subprocess.run(cmd, stdin=subprocess.DEVNULL, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=900)
    thread = None; msgs = []
    for line in p.stdout.splitlines():
        try:
            ev = json.loads(line)
        except Exception:
            continue
        if ev.get("type") == "thread.started":
            thread = ev.get("thread_id")
        if ev.get("type") == "item.completed" and ev.get("item", {}).get("type") == "agent_message":
            msgs.append(ev["item"].get("text", ""))
    info = dict(thread_id=thread, seconds=round(time.time() - t0, 1), exit=p.returncode, messages=[m for m in msgs if m][:3])
    if not thread:
        info["error"] = (p.stderr or "")[-600:]
        return False, info
    pics = sorted(glob.glob(os.path.join(HOME, ".codex", "generated_images", thread, "*.png")), key=os.path.getmtime)
    if not pics:
        info["error"] = "no picture in generated_images\\%s; stderr tail: %s" % (thread, (p.stderr or "")[-400:])
        return False, info
    os.makedirs(os.path.dirname(out_png), exist_ok=True)
    shutil.copyfile(pics[-1], out_png)
    info["source"] = pics[-1]
    return True, info


def version():
    try:
        return subprocess.run([codex_exe(), "--version"], capture_output=True, text=True, stdin=subprocess.DEVNULL, timeout=60).stdout.strip()
    except Exception:
        return "codex-cli ?"


def do_sheets(sel):
    jobs = json.load(open(JOBS, encoding="utf-8"))
    ver = None
    for job in jobs:
        sid = job["id"]
        if sel and sid not in sel:
            continue
        png = os.path.join(GEN, "sheets", sid + ".png")
        if os.path.isfile(png):
            print("cached   %s" % sid); continue
        ver = ver or version()
        prompt = sheet_prompt(job)
        ok, info = generate(prompt, png, job.get("images", ()))
        meta = dict(id=sid, grid=job.get("grid", 3), cells=job["cells"], prompt=prompt, date=datetime.datetime.now().isoformat(timespec="seconds"),
                    tool=ver + " exec, built-in image_gen of the $imagegen skill (gpt-image-2 per the official doc)", **info)
        if ok:
            json.dump(meta, open(os.path.join(GEN, "sheets", sid + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            print("generated %s  %.0f s" % (sid, info["seconds"]))
        else:
            json.dump(meta, open(os.path.join(GEN, "sheets", sid + ".failed.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            print("FAILED   %s  %s" % (sid, info.get("error", "")[:300]))
        sys.stdout.flush()
        time.sleep(PAUSE)


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "sheets"
    if mode == "sheets":
        do_sheets(sys.argv[2:])
    elif mode == "one":
        out, desc = sys.argv[2], sys.argv[3]
        if os.path.isfile(out):
            print("cached", out)
        else:
            prompt = "$imagegen Generate exactly one image and do nothing else (no files, no shell commands). " + desc
            ok, info = generate(prompt, out, sys.argv[4:])
            json.dump(dict(prompt=prompt, date=datetime.datetime.now().isoformat(timespec="seconds"), tool=version(), **info),
                      open(os.path.splitext(out)[0] + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            print("generated" if ok else "FAILED", out, info)
