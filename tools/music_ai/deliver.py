"""Copy the tracks that passed every check into the game and write the metadata the game reads.

    python deliver.py [--stage DIR] [--dry-run]

  Y:\\game\\assets\\audio\\music\\<name>.ogg      replaced ONLY for tracks whose final JSON says passed
  Y:\\game\\assets\\audio\\music\\music_info.json  {"combat_mode":"crossfade","tracks":{name:{seconds,bpm,loop,seed,model}}}
  Y:\\game\\assets\\audio\\manifest_audio.json    only the "duration" numbers of the music files are changed; every
                                               other byte of that file is left as it is (verified with a JSON parser)
Tracks without a passing AI version keep the procedural file (a backup of all 19 is in Y:\\music_backup\\procedural).
"""
import argparse
import copy
import hashlib
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tracks as T  # noqa: E402

MA_ROOT = os.environ.get("MA_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "music_ai"))
GAME_MUSIC = r"Y:\game\assets\audio\music"
MANIFEST = r"Y:\game\assets\audio\manifest_audio.json"
BACKUP = r"Y:\music_backup\procedural"
PROCEDURAL = "procedural (own synthesis + MuseScore_General SoundFont)"


def number_spans(text):
    """Walk a JSON text and return {path tuple: (start, end)} for every number token (no re-serialisation)."""
    spans = {}
    ws = " \t\r\n"
    n = len(text)

    def skip(i):
        while i < n and text[i] in ws:
            i += 1
        return i

    def string(i):                       # text[i] == '"'; returns (value, index after the closing quote)
        j = i + 1
        while text[j] != '"':
            j += 2 if text[j] == "\\" else 1
        return json.loads(text[i:j + 1]), j + 1

    def value(i, path):
        i = skip(i)
        c = text[i]
        if c == "{":
            i = skip(i + 1)
            if text[i] == "}":
                return i + 1
            while True:
                key, i = string(skip(i))
                i = skip(i)
                assert text[i] == ":"
                i = skip(value(i + 1, path + (key,)))
                if text[i] == ",":
                    i += 1
                    continue
                assert text[i] == "}"
                return i + 1
        if c == "[":
            i = skip(i + 1)
            if text[i] == "]":
                return i + 1
            k = 0
            while True:
                i = skip(value(i, path + (k,)))
                k += 1
                if text[i] == ",":
                    i += 1
                    continue
                assert text[i] == "]"
                return i + 1
        if c == '"':
            return string(i)[1]
        for lit in ("true", "false", "null"):
            if text.startswith(lit, i):
                return i + len(lit)
        j = i
        while j < n and text[j] in "+-0123456789.eE":
            j += 1
        assert j > i, "unexpected character %r at %d" % (c, i)
        spans[path] = (i, j)
        return j

    end = skip(value(0, ()))
    assert end == n
    return spans


def fmt_seconds(v):
    s = ("%.3f" % v).rstrip("0").rstrip(".")
    return s


def patch_manifest(durations, dry):
    """durations: {track name: seconds}. Returns the number of numbers replaced."""
    with open(MANIFEST, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8")
    obj = json.loads(text)
    expected = copy.deepcopy(obj)
    spans = number_spans(text)
    edits = []
    for name, sec in durations.items():
        new = json.loads(fmt_seconds(sec))
        if name in obj.get("music", {}) and ("music", name, "duration") in spans:
            edits.append((spans[("music", name, "duration")], fmt_seconds(sec)))
            expected["music"][name]["duration"] = new
        for i, e in enumerate(obj.get("files", [])):
            if isinstance(e, dict) and e.get("category") == "music" and e.get("path") == "assets/audio/music/%s.ogg" % name \
                    and ("files", i, "duration") in spans:
                edits.append((spans[("files", i, "duration")], fmt_seconds(sec)))
                expected["files"][i]["duration"] = new
    edits.sort(reverse=True)
    for (a, b), s in edits:
        text = text[:a] + s + text[b:]
    assert json.loads(text) == expected, "manifest patch changed something else"
    if not dry and edits:
        with open(MANIFEST, "wb") as f:
            f.write(text.encode("utf-8"))
    return len(edits)


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        h.update(f.read())
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", default=os.path.join(MA_ROOT, "stage", "ace_xl"))
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    run(args.stage, args.dry_run, print)


def run(stage, dry_run, say):
    import types
    args = types.SimpleNamespace(stage=stage, dry_run=dry_run)
    print = say                                   # noqa: A001 - route messages to the caller's log
    import soundfile as sf
    final_dir = os.path.join(args.stage, "final")
    with open(MANIFEST, encoding="utf-8") as f:
        man = json.load(f)
    old_bpm = {}
    for e in man.get("files", []):
        if isinstance(e, dict) and e.get("category") == "music":
            old_bpm[os.path.basename(e["path"])[:-4]] = e.get("bpm")
    info_path = os.path.join(GAME_MUSIC, "music_info.json")
    tracks_info = {}
    if os.path.exists(info_path):
        with open(info_path, encoding="utf-8") as f:
            tracks_info = json.load(f).get("tracks", {})
    durations = {}
    for tr in T.TRACKS:
        n = tr["name"]
        dst = os.path.join(GAME_MUSIC, n + ".ogg")
        fj = os.path.join(final_dir, n + ".json")
        fo = os.path.join(final_dir, n + ".ogg")
        r = None
        if os.path.exists(fj) and os.path.exists(fo):
            with open(fj, encoding="utf-8") as f:
                r = json.load(f)
        if r and r.get("passed"):
            if not os.path.exists(os.path.join(BACKUP, n + ".ogg")):
                raise SystemExit("no backup of %s.ogg in %s - refusing to overwrite" % (n, BACKUP))
            if not os.path.exists(dst) or sha(dst) != sha(fo):
                print("deliver %-15s %7.2f s  seed %d" % (n, r["ogg"]["seconds"], r["seed"]))
                if not args.dry_run:
                    shutil.copyfile(fo, dst)
            tracks_info[n] = dict(seconds=r["ogg"]["seconds"], bpm=round(r["ogg"]["tempo"], 1) if tr["pulse"] else tr["bpm"],
                                  loop=tr["loop_s"] is not None, seed=r["seed"], model=r["model"])
            durations[n] = r["ogg"]["seconds"]
        else:
            # no passing AI version (any more): the game must play the procedural track
            bk = os.path.join(BACKUP, n + ".ogg")
            if os.path.exists(bk) and (not os.path.exists(dst) or sha(dst) != sha(bk)):
                print("restore %-15s procedural track from the backup (%s)" % (n, (r or {}).get("reason") or "no passing AI version"))
                if not args.dry_run:
                    shutil.copyfile(bk, dst)
            i = sf.info(bk if args.dry_run and os.path.exists(bk) else dst)
            print("keep    %-15s procedural fallback (%s)" % (n, (r or {}).get("reason") or "no passing AI version yet"))
            tracks_info[n] = dict(seconds=round(i.frames / i.samplerate, 3), bpm=old_bpm.get(n), loop=tr["loop_s"] is not None,
                                  seed=None, model=PROCEDURAL)
            durations[n] = tracks_info[n]["seconds"]
    out = dict(combat_mode="crossfade", tracks={t["name"]: tracks_info[t["name"]] for t in T.TRACKS if t["name"] in tracks_info})
    if not args.dry_run:
        with open(info_path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(out, f, indent=2, ensure_ascii=False)
            f.write("\n")
    k = patch_manifest(durations, args.dry_run)
    print("music_info.json: %d tracks (%d AI, %d procedural); manifest: %d duration numbers updated%s" % (
        len(out["tracks"]), sum(1 for v in out["tracks"].values() if v["seed"] is not None),
        sum(1 for v in out["tracks"].values() if v["seed"] is None), k, " (dry run)" if args.dry_run else ""))


if __name__ == "__main__":
    main()
