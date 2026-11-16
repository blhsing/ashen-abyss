"""Build the listening page Y:\\music_samples\\index.html (self-contained, relative paths, no external resources).

    python make_samples.py [--pieces menu_theme,wastes_base,boss]

Rows = pieces, columns = the current procedural version (baseline, from the backup) and one column per model
listed in samples_models.json. Every file is brought to -18 LUFS (files already within 0.3 LU are copied
unchanged, others are re-levelled and re-encoded) and written as OGG Vorbis next to the page.
"""
import argparse
import html
import json
import os
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tracks as T  # noqa: E402
import audiolib as al  # noqa: E402

MA_ROOT = os.environ.get("MA_ROOT", os.path.join(os.environ.get("LOCALAPPDATA", ""), "AshenAbyssTools", "music_ai"))
OUT = r"Y:\music_samples"
BACKUP = r"Y:\music_backup\procedural"
TARGET = -18.0

PIECE_NOTES = {
    "menu_theme": "Main theme: mournful solo cello over a drone and distant wordless choir; builds once and subsides. Loop.",
    "wastes_base": "Ashen Wastes exploration: cold, sparse, windy; solo viola and breathy textures. Loop.",
    "boss": "Boss fight: relentless low-string ostinato, choir stabs, big drums. Loop.",
}

CSS = """
:root { color-scheme: dark; }
body { background:#14161a; color:#d9d4c7; font:15px/1.5 "Segoe UI", system-ui, sans-serif; margin:0; padding:28px 36px 60px; }
h1 { font-size:24px; font-weight:600; margin:0 0 4px; color:#f0b56a; letter-spacing:.3px; }
h2 { font-size:17px; margin:34px 0 8px; color:#e9dfc9; }
p { max-width:1000px; margin:8px 0; }
.sub { color:#9a958a; margin-bottom:18px; }
table { border-collapse:collapse; margin-top:14px; }
th, td { border:1px solid #2c3038; padding:10px 12px; vertical-align:top; }
th { background:#1c1f25; text-align:left; font-weight:600; color:#e9dfc9; }
th small { display:block; font-weight:400; color:#9a958a; }
td.piece { width:210px; background:#191b20; }
td.piece b { color:#f0b56a; }
td.piece small { display:block; color:#9a958a; margin-top:4px; }
audio { width:300px; height:36px; display:block; }
.meta { font-size:12px; color:#8f8a7f; margin-top:5px; max-width:300px; }
.pick { outline:2px solid #6b5327; }
.model { border-left:3px solid #6b5327; padding:2px 0 2px 14px; margin:14px 0; max-width:1000px; }
.model b { color:#e9dfc9; }
code { background:#1f2229; padding:1px 5px; border-radius:3px; font-size:13px; }
.warn { color:#e0a35f; }
"""


def level_to_target(src, dst):
    """Copy src to dst at -18 LUFS. Returns (lufs before, lufs after, seconds, re-encoded?)."""
    x, sr = al.read_audio(src)
    before = al.lufs(x, sr)
    if abs(before - TARGET) <= 0.3:
        shutil.copyfile(src, dst)
        return before, before, len(x) / sr, False
    y = x * 10.0 ** ((TARGET - before) / 20.0)
    y, _ = al.limit(y, sr, ceiling_db=-1.5)
    al.write_ogg(dst, y, sr, 0.6)
    d, dsr = al.read_audio(dst)
    return before, al.lufs(d, dsr), len(d) / dsr, True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pieces", default=",".join(T.PILOT))
    args = ap.parse_args()
    pieces = [p for p in args.pieces.split(",") if p]
    with open(os.path.join(HERE, "samples_models.json"), encoding="utf-8") as f:
        cfg = json.load(f)
    os.makedirs(OUT, exist_ok=True)
    # text-match numbers come from the batch's own records (no model is loaded here)
    cols = [dict(key="procedural", title="Previous version (procedural)", note="SoundFont-based synthesis, kept as the fallback", baseline=True)]
    cols += [m for m in cfg["models"] if os.path.isdir(m["final_dir"]) and any(
        os.path.exists(os.path.join(m["final_dir"], p + ".ogg")) for p in pieces)]
    rows = []
    for p in pieces:
        tr = T.BY_NAME[p]
        cells = []
        for c in cols:
            if c.get("baseline"):
                src, meta = os.path.join(BACKUP, p + ".ogg"), None
            else:
                src = os.path.join(c["final_dir"], p + ".ogg")
                mj = os.path.join(c["final_dir"], p + ".json")
                meta = json.load(open(mj, encoding="utf-8")) if os.path.exists(mj) else None
            if not os.path.exists(src):
                cells.append(None)
                continue
            name = "%s__%s.ogg" % (p, c["key"])
            before, after, sec, redone = level_to_target(src, os.path.join(OUT, name))
            bits = ["%d:%02d" % (sec // 60, sec % 60), "%.1f LUFS" % after]
            if c.get("baseline"):
                bp = os.path.join(MA_ROOT, "stage", "baseline_eval", p + ".json")
                if os.path.exists(bp):
                    bits.append("text match %.2f" % json.load(open(bp, encoding="utf-8")).get("sim", 0))
            elif meta and "clap_sim" in meta:
                bits.append("text match %.2f" % meta["clap_sim"])
            if meta:
                bits.append("seed %s" % meta.get("seed"))
                if tr["pulse"]:
                    bits.append("%.0f BPM measured (asked %d)" % (meta["ogg"]["tempo"], tr["bpm"]))
                if not meta.get("passed", True):
                    bits.append("FAILED checks: " + meta.get("reason", ""))
            cells.append(dict(file=name, bits=bits, recommended=bool(c.get("recommended"))))
            print("%-12s %-16s %6.1f s  %.2f -> %.2f LUFS%s" % (p, c["key"], sec, before, after, " (re-encoded)" if redone else ""))
        rows.append((p, cells))

    h = ["<!doctype html>", "<html lang='en'><head><meta charset='utf-8'>",
         "<meta name='viewport' content='width=device-width, initial-scale=1'>",
         "<title>Ashen Abyss - music samples</title>", "<style>%s</style></head><body>" % CSS,
         "<h1>燼淵 Ashen Abyss — music model samples</h1>",
         "<div class='sub'>Generated %s on this machine. All files are OGG Vorbis, loudness-matched to about %d LUFS. "
         "Loops are cut and crossfaded the same way as the delivered tracks.</div>" % (time.strftime("%Y-%m-%d %H:%M"), TARGET),
         "<p>%s</p>" % cfg.get("intro", ""),
         "<table><tr><th>Piece</th>"]
    for c in cols:
        h.append("<th>%s<small>%s</small></th>" % (html.escape(c["title"]), html.escape(c.get("note", ""))))
    h.append("</tr>")
    for p, cells in rows:
        h.append("<tr><td class='piece'><b>%s</b><small>%s</small></td>" % (p, html.escape(PIECE_NOTES.get(p, T.BY_NAME[p]["describe"]))))
        for cell in cells:
            if cell is None:
                h.append("<td><div class='meta'>not available</div></td>")
            else:
                h.append("<td%s><audio controls preload='none' src='%s'></audio><div class='meta'>%s</div></td>" % (
                    " class='pick'" if cell["recommended"] else "", cell["file"], html.escape(" · ".join(cell["bits"]))))
        h.append("</tr>")
    h.append("</table>")
    h.append("<h2>Models</h2>")
    for c in [m for m in cols if not m.get("baseline")]:
        h.append("<div class='model'><b>%s</b>%s<br>%s</div>" % (
            html.escape(c["title"]), " — <span class='warn'>recommended, used for the full set</span>" if c.get("recommended") else "",
            c["about_html"]))
    for extra in cfg.get("notes_html", []):
        h.append("<p>%s</p>" % extra)
    h.append("</body></html>")
    with open(os.path.join(OUT, "index.html"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(h) + "\n")
    print("wrote", os.path.join(OUT, "index.html"))


if __name__ == "__main__":
    main()
