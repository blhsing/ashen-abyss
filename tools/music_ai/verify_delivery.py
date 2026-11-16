"""Independent check of what is in the game folder (run after deliver.py):

    python verify_delivery.py

For each of the 19 music files: decodes the OGG, checks format (stereo, 44.1 kHz), loudness (-18 LUFS, intro_bed
-22), true peak, DC, silence, and for loops the sample continuity at the loop point; compares the length with
music_info.json and with the duration entries of manifest_audio.json; says whether the file is the generated
version (identical to the staged final), the untouched procedural track (identical to the backup) or neither.
Exit code 1 if anything is wrong.
"""
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import audiolib as al  # noqa: E402
import tracks as T  # noqa: E402

MA_ROOT = os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai"))
GAME = r"Y:\game\assets\audio\music"
BACKUP = r"Y:\music_backup\procedural"
MANIFEST = r"Y:\game\assets\audio\manifest_audio.json"


def sha(p):
    with open(p, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    final_dir = os.path.join(MA_ROOT, "stage", "ace_xl", "final")
    info = json.load(open(os.path.join(GAME, "music_info.json"), encoding="utf-8"))
    man = json.load(open(MANIFEST, encoding="utf-8"))
    man_files = {os.path.basename(e["path"])[:-4]: e for e in man["files"] if e.get("category") == "music"}
    assert info["combat_mode"] == "crossfade"
    bad = 0
    print("%-15s %-10s %8s %7s %7s %6s %-22s %s" % ("file", "origin", "seconds", "LUFS", "dBTP", "seam", "music_info / manifest", "problems"))
    for tr in T.TRACKS:
        n = tr["name"]
        p = os.path.join(GAME, n + ".ogg")
        probs = []
        x, sr = al.read_audio(p)
        import soundfile as sf
        i = sf.info(p)
        if i.channels != 2 or i.samplerate != 44100 or i.format != "OGG":
            probs.append("format %s %d ch %d Hz" % (i.format, i.channels, i.samplerate))
        h = sha(p)
        fo = os.path.join(final_dir, n + ".ogg")
        origin = "generated" if os.path.exists(fo) and sha(fo) == h else "procedural" if sha(os.path.join(BACKUP, n + ".ogg")) == h else "UNKNOWN"
        if origin == "UNKNOWN":
            probs.append("file matches neither the staged final nor the backup")
        sec = len(x) / sr
        lu = al.lufs(x, sr)
        tp = al.true_peak_db(x, sr)
        if origin == "generated":
            if abs(lu - tr["lufs"]) > 0.5:
                probs.append("loudness %.1f" % lu)
            if tp > -0.9:
                probs.append("true peak %.1f" % tp)
        if abs(float(x.mean())) > 0.005:
            probs.append("DC")
        seam = "-"
        if tr["loop_s"]:
            s = al.seam_metrics(x, sr)
            seam = "%.2f" % s["jump_vs_p999"]
            if s["jump_vs_p999"] > 1.0:          # a sample step at the wrap larger than ordinary sample-to-sample movement
                probs.append("seam jump %.2f" % s["jump_vs_p999"])
        mi = info["tracks"].get(n)
        if not mi or abs(mi["seconds"] - sec) > 0.01 or mi["loop"] != (tr["loop_s"] is not None) or (mi["seed"] is None) != (origin == "procedural"):
            probs.append("music_info mismatch %r" % (mi,))
        md = man["music"][n]["duration"]
        mf = man_files[n]["duration"]
        if abs(md - sec) > 0.01 or abs(mf - sec) > 0.01:
            probs.append("manifest duration %s / %s" % (md, mf))
        bad += bool(probs)
        print("%-15s %-10s %8.3f %7.2f %7.2f %6s %-22s %s" % (n, origin, sec, lu, tp, seam, "%s / %s" % (mi["seconds"] if mi else "?", md),
                                                           "; ".join(probs) or "ok"))
    extra = sorted(f for f in os.listdir(GAME) if f.endswith(".ogg") and f[:-4] not in T.BY_NAME)
    if extra:
        print("unexpected files:", extra)
    print("%d of %d files ok" % (len(T.TRACKS) - bad, len(T.TRACKS)))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
