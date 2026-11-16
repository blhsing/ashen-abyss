"""Print the per-track result table (Markdown) from the staging folder and refresh it inside
Y:\\credits\\music_ai.md (between the markers) so the credits always show what is really in the game.

    python report_tables.py [--stage DIR] [--no-write]
"""
import argparse
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tracks as T  # noqa: E402

MA_ROOT = os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai"))
GAME_MUSIC = r"Y:\game\assets\audio\music"
CREDITS = r"Y:\credits\music_ai.md"
BEGIN, END = "<!-- track-table:begin -->", "<!-- track-table:end -->"


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", default=os.path.join(MA_ROOT, "stage", "ace_xl"))
    ap.add_argument("--no-write", action="store_true")
    args = ap.parse_args()
    final_dir = os.path.join(args.stage, "final")
    with open(os.path.join(GAME_MUSIC, "music_info.json"), encoding="utf-8") as f:
        info = json.load(f)["tracks"]
    rows = ["| file | in the game | length | tempo (asked) | key heard (asked) | seed (candidates tried) | rendered on | text match (procedural) | vocal / genre / mood flags | words found | loop seam: jump vs normal motion, click, level step | loudness, true peak |",
            "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for tr in T.TRACKS:
        n = tr["name"]
        fj, fo = os.path.join(final_dir, n + ".json"), os.path.join(final_dir, n + ".ogg")
        game = os.path.join(GAME_MUSIC, n + ".ogg")
        r = json.load(open(fj, encoding="utf-8")) if os.path.exists(fj) else None
        delivered = bool(r and r.get("passed") and os.path.exists(fo) and sha(fo) == sha(game))
        base = {}
        bp = os.path.join(MA_ROOT, "stage", "baseline_eval", n + ".json")
        if os.path.exists(bp):
            base = json.load(open(bp, encoding="utf-8"))
        if not r or "ogg" not in r:
            rows.append("| `%s` | procedural fallback | %s s | - | - | - | - | - (%.2f) | - | - | - | %s |" % (
                n, info[n]["seconds"], base.get("sim", 0), (r or {}).get("reason", "no passing AI candidate")))
            continue
        o = r["ogg"]
        seam = r.get("seam_ogg")
        seam_txt = "%.2fx, %+.1f dB, %+.1f dB" % (seam["jump_vs_p999"], seam["hf_click_db"], seam["level_step_db"]) if seam else "not looped"
        tempo = "%.0f (%d)" % (o["tempo"], tr["bpm"]) if tr["pulse"] else "free (%d)" % tr["bpm"]
        fl = r.get("flags", {})
        cj = os.path.join(args.stage, "cand", "%s__s%d.json" % (n, r["seed"]))
        dev = "?"
        if os.path.exists(cj):
            c = json.load(open(cj, encoding="utf-8"))
            dev = "GPU, bfloat16" if "Radeon" in c.get("device", "") else "CPU, float32"
        w = r.get("words") or {}
        words = "none (%d windows)" % w["windows"] if w and w.get("word_windows") == 0 else ("%d of %d windows" % (w["word_windows"], w["windows"]) if w else "not checked")
        rows.append("| `%s` | %s | %d:%05.2f | %s | %s (%s) | %d (%d) | %s | %.2f (%.2f) | %.2f / %.2f / %.2f | %s | %s | %.1f LUFS, %.1f dBTP |" % (
            n, "AI" if delivered else "procedural fallback (%s)" % (r.get("reason") or "not delivered"),
            o["seconds"] // 60, o["seconds"] % 60, tempo, r["key_est"], tr["key"], r["seed"], len(r.get("candidates", [])), dev,
            r["clap_sim"], base.get("sim", 0), fl.get("vocal", 0), fl.get("genre", 0), fl.get("mood", 0), words, seam_txt,
            o["lufs"], o["true_peak_db"]))
    table = "\n".join(rows)
    print(table)
    if not args.no_write and os.path.exists(CREDITS):
        s = open(CREDITS, encoding="utf-8").read()
        block = BEGIN + "\n" + table + "\n" + END
        if BEGIN in s and END in s:
            s = s[:s.index(BEGIN)] + block + s[s.index(END) + len(END):]
        else:
            s = s.rstrip("\n") + "\n\n" + block + "\n"
        with open(CREDITS, "w", encoding="utf-8", newline="\n") as f:
            f.write(s)


if __name__ == "__main__":
    main()
