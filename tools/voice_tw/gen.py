"""Production voice-over generator: VoxCPM2 voice design, 23 designed voices, no reference audio.
Run with the `voxcpm` venv (see README.md). Every step is resumable: finished work is skipped.

    gen.py all                         anchors -> lines -> drift check and repair -> effects -> report -> deliver
    gen.py anchors [--voice v]         the one take per voice made from its description
    gen.py lines [--only id,id] [--voice v] [--force]
                                       dry takes (each line continues its character's anchor), ASR-checked with retries
    gen.py drift [--fix]               speaker similarity of every line to its anchor; --fix regenerates lines that drift
    gen.py fx [--only id,id] [--voice v] [--strength dry|light|medium]
                                       dry take -> polish / boss chain -> OGG in staging (no speech is generated)
    gen.py report                      report.json (Y:\\tools\\voice_tw\\report.json)
    gen.py deliver                     copy validated OGG files to Y:\\game\\assets\\audio\\vo
    gen.py osaros --variant NAME [--strength S]
                                       switch the final boss to another description from the choice page

One line again:        gen.py lines --only elder_first_02 --force   then   gen.py fx --only elder_first_02 ; gen.py deliver
Another boss strength: gen.py fx --voice boss_osaros --strength light ; gen.py deliver        (seconds, from the dry takes)
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time

import numpy as np
import soundfile as sf

import engine as E
import fx
import profiles
import voices
import vt

LOG = os.path.join(vt.ROOT, "logs", "gen.log")
GAME_VO = r"Y:\game\assets\audio\vo"
REPORT = os.path.join(E.HERE, "report.json")
BOSS_FX = os.path.join(E.HERE, "boss_fx.json")           # {"boss_osaros": "medium", ...}; missing = medium
OGG_QUALITY = 0.5                                        # Vorbis VBR quality, same as the fallback tool
FINAL_LIMIT = 0.15                                       # largest acceptable error rate of the FINAL file
DRIFT_ABS, DRIFT_REL = 0.55, 0.20                        # a line drifts if its similarity to the anchor is below
                                                         # 0.55, or more than 0.20 under its voice's median
TOO_SIMILAR = 0.75                                       # two anchors closer than this are not distinct voices
LOCKED = {"boss_osaros"}                                 # effect strength fixed by the user: a failing line is re-taken, never weakened
USER_APPROVED = {"boss_osaros"}                          # voice chosen by the user by ear: audited, but never redesigned automatically
AUDIT = os.path.join(E.PROD, "audit.json")
DECISION = os.path.join(E.HERE, "DECISION.md")


def script() -> list[dict]:
    return json.load(open(vt.VO_LINES, encoding="utf-8"))


def select(lines: list[dict], args) -> list[dict]:
    only = set(args.only.split(",")) if getattr(args, "only", None) else None
    voice = getattr(args, "voice", None)
    return [l for l in lines if (not only or l["id"] in only) and (not voice or l["voice"] == voice)]


def profile_name(voice: str) -> str:
    return profiles.resolve(voice)[0]


def voice_key(voice: str) -> str:
    """Key of the designed voice: the name itself, a new neutral boss voice for an unknown boss, else the narrator."""
    return voices.ensure(voice)


def load_json(path: str, default=None):
    try:
        return json.load(open(path, encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        return default


def median_f0(path: str) -> float | None:
    x, sr = vt.read_wav(path)
    return vt.voice_stats(x, sr)["f0"]


# ------------------------------------------------------------------ anchors

def do_anchors(eng: E.Engine, only_voice: str | None = None) -> dict:
    anchors = {}
    for v in voices.VOICES:
        if only_voice and v != only_voice:
            continue
        a = eng.anchor(v)
        anchors[v] = a
        if not a["ok"] or not a.get("in_range", True):
            E.log(f"WARNING anchor {v}: cer {a['cer']}, median pitch {a.get('median_f0')} Hz, wanted {a.get('f0_wanted')}", LOG)
    if only_voice:
        return anchors
    # the 23 voices must be distinguishable: no two anchors may be near-identical
    for attempt in range(3):
        emb = E.embeddings([a["wav"] for a in anchors.values()])
        names = list(anchors)
        clash = [(E.cos(emb[anchors[p]["wav"]], emb[anchors[q]["wav"]]), p, q) for i, p in enumerate(names) for q in names[i + 1:]]
        clash = sorted([c for c in clash if c[0] > TOO_SIMILAR], reverse=True)
        if not clash:
            break
        used = {load_json(os.path.join(E.DRY, f), {}).get("voice") for f in (os.listdir(E.DRY) if os.path.isdir(E.DRY) else []) if f.endswith(".json")}
        redone = set()
        for sim, p, q in clash:
            if q in used and p not in used:                         # redesign the voice that has no lines yet
                p, q = q, p
            if q in used or q in redone or p in redone:
                continue
            redone.add(q)
            E.log(f"anchors {p} and {q} are too alike ({sim:.2f}); redesigning {q}", LOG)
            os.replace(os.path.join(E.ANCHORS, q + ".wav"), os.path.join(E.ANCHORS, q + f".too_alike{attempt}.wav"))
            os.remove(os.path.join(E.ANCHORS, q + ".json"))
            anchors[q] = eng.anchor(q, seed_base=10 * (attempt + 1))      # other seeds = other voices, same pitch gate
        if not redone:
            break
    return anchors


# ------------------------------------------------------------------ lines

def do_lines(eng: E.Engine, lines: list[dict], force: bool = False) -> None:
    t0 = time.time()
    todo = [l for l in lines if force or not os.path.exists(os.path.join(E.DRY, l["id"] + ".json"))]
    E.log(f"lines: {len(lines)} selected, {len(todo)} to generate", LOG)
    for n, l in enumerate(lines, 1):
        v = voice_key(l["voice"])
        a = eng.anchor(v)
        fresh = force or not os.path.exists(os.path.join(E.DRY, l["id"] + ".json"))
        m = eng.line(l["id"], l["voice"], l["text"], a, force=force)
        if fresh:
            done = sum(1 for x in lines[:n] if os.path.exists(os.path.join(E.DRY, x["id"] + ".json")))
            rate = eng.compute / max(eng.audio, 1)
            E.log(f"[{n}/{len(lines)}] {l['id']} ({l['voice']}): cer {m['cer']:.3f} seed {m['seed']} tries {len(m['tries'])} "
                  f"{'ok' if m['ok'] else 'FAILED'} | {rate:.1f} s per s of audio, {(time.time() - t0) / 60:.0f} min elapsed", LOG)


# ------------------------------------------------------------------ drift

def similarities(lines: list[dict]) -> dict:
    """{id: similarity of the dry take to its voice's anchor}, plus per-voice statistics."""
    have = [l for l in lines if os.path.exists(os.path.join(E.DRY, l["id"] + ".wav"))]
    anchor_wav = {v: os.path.join(E.ANCHORS, v + ".wav") for v in {voice_key(l["voice"]) for l in have}}
    emb = E.embeddings([os.path.join(E.DRY, l["id"] + ".wav") for l in have] + list(anchor_wav.values()))
    sim = {l["id"]: round(E.cos(emb[os.path.join(E.DRY, l["id"] + ".wav")], emb[anchor_wav[voice_key(l["voice"])]]), 3) for l in have}
    per_voice = {}
    for v in sorted({l["voice"] for l in have}):
        ids = [l["id"] for l in have if l["voice"] == v]
        s = [sim[i] for i in ids]
        embs = [emb[os.path.join(E.DRY, i + ".wav")] for i in ids]
        pairs = [E.cos(embs[i], embs[j]) for i in range(len(embs)) for j in range(i + 1, len(embs))]
        per_voice[v] = dict(lines=len(ids), to_anchor_mean=round(float(np.mean(s)), 3), to_anchor_min=round(float(np.min(s)), 3),
                            worst_line=ids[int(np.argmin(s))], median=round(float(np.median(s)), 3),
                            between_lines_mean=round(float(np.mean(pairs)), 3) if pairs else None)
    names = sorted(anchor_wav)
    cross = [E.cos(emb[anchor_wav[p]], emb[anchor_wav[q]]) for i, p in enumerate(names) for q in names[i + 1:]]
    nearest = {p: max(((round(E.cos(emb[anchor_wav[p]], emb[anchor_wav[q]]), 2), q) for q in names if q != p), default=(None, None)) for p in names}
    return dict(sim=sim, per_voice=per_voice, cross_mean=round(float(np.mean(cross)), 3) if cross else None,
                cross_max=round(float(np.max(cross)), 3) if cross else None, nearest=nearest)


def drifting(lines: list[dict], s: dict) -> list[str]:
    out = []
    for l in lines:
        v = s["sim"].get(l["id"])
        if v is None:
            continue
        med = s["per_voice"][l["voice"]]["median"]
        if v < DRIFT_ABS or v < med - DRIFT_REL:
            out.append(l["id"])
    return out


def ensure_anchor(eng: E.Engine, v: str, seed_base: int = 0) -> dict:
    """Anchor of one voice; a freshly designed voice that is too close to an existing one is designed again."""
    old = load_json(os.path.join(E.ANCHORS, v + ".json"))
    fresh = not old or old.get("desc") != voices.VOICES[v]["desc"]
    a = eng.anchor(v, seed_base=seed_base)
    for attempt in range(2):
        if not fresh:
            break
        others = [f[:-5] for f in os.listdir(E.ANCHORS) if f.endswith(".json") and "@" not in f and f[:-5] != v]
        if not others:
            break
        emb = E.embeddings([a["wav"]] + [os.path.join(E.ANCHORS, o + ".wav") for o in others])
        sim, near = max((E.cos(emb[a["wav"]], emb[os.path.join(E.ANCHORS, o + ".wav")]), o) for o in others)
        E.log(f"anchor {v}: nearest other voice {near} at {sim:.2f}", LOG)
        if sim <= TOO_SIMILAR:
            break
        E.log(f"anchor {v} is too close to {near} ({sim:.2f} > {TOO_SIMILAR}); designing it again", LOG)
        os.replace(a["wav"], a["wav"][:-4] + f".too_alike{attempt}.wav")
        os.remove(os.path.join(E.ANCHORS, v + ".json"))
        a = eng.anchor(v, seed_base=seed_base + 100 * (attempt + 1))
    if not a["ok"] or not a.get("in_range", True):
        E.log(f"WARNING anchor {v}: best design has cer {a['cer']}, median pitch {a.get('median_f0')} Hz (wanted {a.get('f0_wanted')}), "
              f"harmonics-to-noise {a.get('hnr')} dB (at least {a.get('hnr_min')})", LOG)
    return a


def audit_voice(v: str, vlines: list[dict]) -> dict:
    """Does the voice MEASURE as its character? Median pitch and the male / female classifier, on the anchor
    and on the given lines (lines may sit up to 10 % beyond the pitch range of the anchor)."""
    vk = voice_key(v)
    spec = voices.VOICES[vk]
    a = load_json(os.path.join(E.ANCHORS, vk + ".json")) or {}
    paths = [os.path.join(E.DRY, l["id"] + ".wav") for l in vlines if os.path.exists(os.path.join(E.DRY, l["id"] + ".wav"))]
    f0s = [f for f in (median_f0(q) for q in paths) if f]
    lo, hi = spec["f0"]
    sex = spec["sex"]
    anchor_f0 = a.get("median_f0") or (median_f0(a["wav"]) if a.get("wav") else None)
    pm_a = a.get("p_male") if a.get("p_male") is not None else (E.sex_score(a["wav"]) if a.get("wav") else None)
    pm_l = E.sex_score(*paths) if paths else None
    lines_f0 = round(float(np.median(f0s))) if f0s else None
    pitch_ok = bool(anchor_f0 and lo <= anchor_f0 <= hi and (lines_f0 is None or (lines_f0 <= hi * 1.1 if sex == "m" else lines_f0 >= lo * 0.9)))
    sex_ok = bool(E.sex_fits(pm_a, sex) and E.sex_fits(pm_l, sex))
    return dict(voice=v, who=spec.get("who", ""), sex=sex, f0_wanted=[lo, hi], anchor_f0=anchor_f0, anchor_p_male=pm_a, lines_checked=len(paths),
                lines_f0=lines_f0, lines_p_male=pm_l, pitch_ok=pitch_ok, sex_ok=sex_ok, passed=bool(pitch_ok and sex_ok),
                designs_tried=a.get("designs_tried"), design_kept=a.get("design_kept"), hnr=a.get("hnr"))


def save_audit(v: str, res: dict, action: str, history: list) -> None:
    au = load_json(AUDIT, {}) or {}
    au[v] = dict(res, action=action, history=history)
    json.dump(au, open(AUDIT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    write_audit_table()


def write_audit_table() -> None:
    """The audit of every designed voice, as a table in DECISION.md (between two markers, rewritten each time)."""
    au = load_json(AUDIT, {}) or {}
    g = (load_json(os.path.join(E.HERE, "gender_model.json"), {}) or {}).get("info", {})
    rows = ["<!-- voice-audit-start -->", "## Voice audit - does every designed voice measure as its character?", "",
            "Written by `gen.py` (" + time.strftime("%Y-%m-%d %H:%M") + "). Gates: (1) median pitch of the anchor inside the range wanted for the",
            "character, and of its first three lines within 10 % of it (men at most 150 Hz - the elder 140 Hz -, women at least",
            "165 Hz); (2) the male / female classifier of `gender.py` (CAM++ speaker embeddings, calibrated on the 100 labelled",
            f"Kokoro speakers: {g.get('cross_validation_accuracy', '?')} cross-validated accuracy there; on VoxCPM2 voices a man must score at least",
            f"{E.SEX_M_MIN}, a woman at most {E.SEX_F_MAX}). A voice that fails is designed again (other seeds) and all of its lines with it.", "",
            "| character | intended | pitch wanted (Hz) | anchor pitch | lines pitch (n) | p(male) anchor / lines | gate | action |",
            "|---|---|---|---|---|---|---|---|"]
    for v in list(voices.VOICES) + [x for x in au if x not in voices.VOICES]:
        if v not in au:
            rows.append(f"| {v} | {voices.VOICES[v].get('who', '')} | {voices.VOICES[v]['f0'][0]}-{voices.VOICES[v]['f0'][1]} | - | - | - | not designed yet | - |")
            continue
        r = au[v]
        for h in r.get("history", []):
            rows.append(f"| {v} (rejected) | {r['who']} | {r['f0_wanted'][0]}-{r['f0_wanted'][1]} | {h.get('anchor_f0')} | {h.get('lines_f0')} ({h.get('lines_checked')}) | "
                        f"{h.get('anchor_p_male')} / {h.get('lines_p_male')} | **failed** ({'pitch' if not h.get('pitch_ok') else ''}{' ' if not h.get('pitch_ok') and not h.get('sex_ok') else ''}{'classifier' if not h.get('sex_ok') else ''}) | {h.get('action', 'rejected, designed again')} |")
        gate = "pass" if r["passed"] else "**failed** (" + " ".join(x for x, ok in (("pitch", r["pitch_ok"]), ("classifier", r["sex_ok"])) if not ok) + ")"
        rows.append(f"| {v} | {r['who']} | {r['f0_wanted'][0]}-{r['f0_wanted'][1]} | {r['anchor_f0']} | {r['lines_f0']} ({r['lines_checked']}) | "
                    f"{r['anchor_p_male']} / {r['lines_p_male']} | {gate} | {r['action']} |")
    rows.append("<!-- voice-audit-end -->")
    text = open(DECISION, encoding="utf-8").read() if os.path.exists(DECISION) else ""
    block = "\n".join(rows)
    if "<!-- voice-audit-start -->" in text and "<!-- voice-audit-end -->" in text:
        a, b = text.index("<!-- voice-audit-start -->"), text.index("<!-- voice-audit-end -->") + len("<!-- voice-audit-end -->")
        text = text[:a] + block + text[b:]
    else:
        text = text.rstrip("\n") + "\n\n" + block + "\n"
    open(DECISION, "w", encoding="utf-8", newline="\n").write(text)


def reject_anchor(v: str, vl: list[dict], n: int) -> None:
    """Put a failed design aside and remove everything generated from it."""
    vk = voice_key(v)
    rej = os.path.join(E.ANCHORS, "_rejected")
    os.makedirs(rej, exist_ok=True)
    for ext in (".wav", ".json"):
        src = os.path.join(E.ANCHORS, vk + ext)
        if os.path.exists(src):
            os.replace(src, os.path.join(rej, f"{vk}_{n}{ext}"))
    for l in vl:
        for d, exts in ((E.DRY, (".wav", ".json")), (E.FINAL, (".ogg", ".json"))):
            for ext in exts:
                q = os.path.join(d, l["id"] + ext)
                if os.path.exists(q):
                    os.remove(q)


def designed_voice(eng: E.Engine, v: str, vl: list[dict], force: bool = False) -> None:
    """Anchor + first three lines, audited; a voice that does not measure as its character is designed again
    (up to three designs rounds), and everything made from the rejected anchor is discarded."""
    vk = voice_key(v)
    history = ((load_json(AUDIT, {}) or {}).get(v) or {}).get("history", [])
    for attempt in range(3):
        ensure_anchor(eng, vk, seed_base=voices.VOICES[vk].get("seed_base", 0) + 10 * len(history))
        do_lines(eng, vl[:3], force=force)
        res = audit_voice(v, vl[:3])
        E.log(f"audit {v}: anchor {res['anchor_f0']} Hz, lines {res['lines_f0']} Hz (wanted {res['f0_wanted']}), p(male) {res['anchor_p_male']} / "
              f"{res['lines_p_male']} ({res['sex']}) -> {'pass' if res['passed'] else 'FAIL'}", LOG)
        if res["passed"]:
            save_audit(v, res, "kept" if not history else f"kept (design round {len(history) + 1})", history)
            return
        if v in USER_APPROVED:
            save_audit(v, res, "kept: chosen by the user by ear on the choice page", history)
            return
        if attempt == 2:
            save_audit(v, res, "kept after three design rounds - no design passed both gates: LISTEN", history)
            E.log(f"WARNING {v}: no design passed the audit after three rounds; keeping the last one", LOG)
            return
        history.append(dict(res, action="rejected, designed again; its lines were discarded"))
        save_audit(v, res, "being redesigned", history)
        reject_anchor(v, vl, len(history))


def retake_locked(eng: E.Engine, asr: vt.Asr, vl: list[dict]) -> None:
    """A boss whose effect strength the user fixed: a line that does not read back at that strength gets a new
    take (other seeds) instead of a weaker chain."""
    for l in vl:
        if l["voice"] not in LOCKED:
            continue
        for rnd in range(3):
            f = load_json(os.path.join(E.FINAL, l["id"] + ".json"))
            if not f or f.get("ok"):
                break
            E.log(f"{l['id']}: final cer {f.get('final_cer')} at the fixed strength - new take", LOG)
            eng.line(l["id"], l["voice"], l["text"], eng.anchor(voice_key(l["voice"])), force=True, seeds=tuple(21 + 10 * rnd + k for k in range(3)))
            do_fx(asr, [l])


def do_drift(eng: E.Engine | None, lines: list[dict], fix: bool, save: bool = True) -> dict:
    lines = [l for l in lines if os.path.exists(os.path.join(E.DRY, l["id"] + ".wav"))]
    if not lines:
        return {}
    s = similarities(lines)
    bad = drifting(lines, s)
    E.log(f"drift: {len(bad)} of {len(s['sim'])} lines below the consistency limit: {bad}", LOG)
    regenerated = load_json(os.path.join(E.PROD, "regenerated.json"), {})
    if fix and bad and eng is not None:
        by_id = {l["id"]: l for l in lines}
        for i in bad:
            l = by_id[i]
            before = s["sim"][i]
            wav, meta = os.path.join(E.DRY, i + ".wav"), os.path.join(E.DRY, i + ".json")
            keep = (wav + ".keep", meta + ".keep")
            shutil.copyfile(wav, keep[0]); shutil.copyfile(meta, keep[1])
            old = load_json(meta)
            m = eng.line(i, l["voice"], l["text"], eng.anchor(voice_key(l["voice"])), force=True, seeds=(11, 12, 13))
            after = similarities([l])["sim"][i]
            better = m["ok"] and (not old.get("ok") or after > before + 0.03)
            if not better:                                        # the first take was the better one: put it back
                shutil.copyfile(keep[0], wav); shutil.copyfile(keep[1], meta)
            for k in keep:
                os.remove(k)
            regenerated[i] = dict(voice=l["voice"], before=before, after=after, kept="new" if better else "old",
                                  old_seed=old.get("seed"), new_seed=m.get("seed"))
            E.log(f"drift fix {i}: similarity {before:.2f} -> {after:.2f}, kept the {'new' if better else 'old'} take", LOG)
        json.dump(regenerated, open(os.path.join(E.PROD, "regenerated.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        s = similarities(lines)
    if save:
        json.dump(s, open(os.path.join(E.PROD, "similarity.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return s


# ------------------------------------------------------------------ effects, encoding, final check

def encode_ogg(path: str, y: np.ndarray) -> tuple[np.ndarray, dict]:
    """Write mono 44.1 kHz OGG Vorbis and measure the DECODED file; lower the level if the codec overshoots."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for _ in range(4):
        with sf.SoundFile(path, "w", samplerate=fx.OUT_RATE, channels=1, format="OGG", subtype="VORBIS", compression_level=1.0 - OGG_QUALITY) as f:
            for k in range(0, len(y), 16384):                      # short blocks: long single writes are a known libsndfile hazard
                f.write(y[k:k + 16384])
        z, sr = sf.read(path, dtype="float32")
        m = vt.measure(z, sr)
        if m["true_peak_db"] <= fx.MAX_TRUE_PEAK_DB - 0.05:
            return z, m
        y = y * fx.lin(fx.MAX_TRUE_PEAK_DB - 0.3 - m["true_peak_db"])
    return z, m


def boss_strength(voice: str, override: str | None = None) -> str:
    if override:
        return override
    return (load_json(BOSS_FX, {}) or {}).get(voice, "medium")


def fx_signature(dry_meta: dict, prof: dict, want: str) -> str:
    """What a processed file depends on: the dry take, the chain, the strength, the encoder setting."""
    import hashlib
    blob = json.dumps([dry_meta.get("anchor_uid"), dry_meta.get("seed"), dry_meta.get("text"), dry_meta.get("tts_text"), want, prof, OGG_QUALITY,
                       fx.TARGET_LUFS], sort_keys=True, ensure_ascii=False, default=str)
    return hashlib.sha1(blob.encode("utf-8")).hexdigest()[:16]


def do_fx(asr: vt.Asr, lines: list[dict], strength: str | None = None, force: bool = False) -> None:
    if strength:                                                   # remember the choice for the selected boss voices
        cfg = load_json(BOSS_FX, {}) or {}
        for v in {l["voice"] for l in lines if profiles.PROFILES.get(profile_name(l["voice"]), {}).get("boss")}:
            cfg[v] = strength
        json.dump(cfg, open(BOSS_FX, "w", encoding="utf-8"), indent=1)
    os.makedirs(E.FINAL, exist_ok=True)
    order = ["medium", "light", "dry"]
    for l in lines:
        i = l["id"]
        dry_wav, dry_meta = os.path.join(E.DRY, i + ".wav"), load_json(os.path.join(E.DRY, i + ".json"))
        if not dry_meta or not os.path.exists(dry_wav):
            continue
        out, meta = os.path.join(E.FINAL, i + ".ogg"), os.path.join(E.FINAL, i + ".json")
        pname, prof = profiles.resolve(l["voice"])
        dry, sr = vt.read_wav(dry_wav)
        base = fx.resample(dry, sr, fx.OUT_RATE)
        target = fx.TARGET_LUFS + prof.get("loudness_offset", 0.0)
        is_boss = bool(prof.get("boss"))
        want = boss_strength(l["voice"], strength) if is_boss else "polish"
        sig = fx_signature(dry_meta, prof, want)
        done = load_json(meta)
        if not force and done and done.get("sig") == sig and os.path.exists(out):
            continue                                                # already processed from this very take with this chain
        chain = ["polish"] if not is_boss else [want] if l["voice"] in LOCKED else order[order.index(want):]
        res = None
        for st in chain:
            if st == "polish":
                y = fx.apply(prof, base, fx.OUT_RATE, seed=7)
            elif st == "dry":
                y = base
            else:
                y = fx.apply(profiles.scaled(prof, profiles.BOSS_STRENGTH[st]), base, fx.OUT_RATE, seed=7)
            z, m = encode_ogg(out, fx.finish(y, target))
            heard = asr.transcribe(z, fx.OUT_RATE)["text"]
            cer = round(vt.cer_hint(l["text"], heard), 3)
            res = dict(id=i, voice=l["voice"], profile=pname, strength=st, wanted=want, final_heard=vt.s2t(heard), final_cer=cer,
                       final_cer_strict=round(vt.cer_strict(l["text"], heard), 3), bytes=os.path.getsize(out), sig=sig, **m)
            if cer <= FINAL_LIMIT or not is_boss:
                break
            E.log(f"fx {i}: final cer {cer} at {st} strength - trying a weaker chain", LOG)
        n = max(1, len(vt.for_compare(l["text"])))
        res["chars_per_s"] = round(n / max(res["duration"] - 2 * fx.EDGE_SILENCE, 0.1), 2)
        res["ok"] = bool(dry_meta.get("ok") and res["final_cer"] <= FINAL_LIMIT and not res["nan"] and res["true_peak_db"] < fx.MAX_TRUE_PEAK_DB
                         and abs(res["lufs"] - target) <= 1.0)
        json.dump(res, open(meta, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        E.log(f"fx {i}: {res['strength']} final cer {res['final_cer']:.3f} {res['duration']:.1f}s {res['lufs']:.1f} LUFS {res['true_peak_db']:.1f} dBTP "
              f"{'ok' if res['ok'] else 'FAILED'}", LOG)


# ------------------------------------------------------------------ report and delivery

def do_report(lines: list[dict], s: dict | None = None) -> dict:
    s = s or load_json(os.path.join(E.PROD, "similarity.json"), {}) or {}
    rows, failed, missing = [], [], []
    for l in lines:
        d = load_json(os.path.join(E.DRY, l["id"] + ".json"))
        f = load_json(os.path.join(E.FINAL, l["id"] + ".json"))
        if not d or not f:
            missing.append(l["id"])
            continue
        row = dict(id=l["id"], speaker=l["speaker"], voice=l["voice"], text=l["text"], tts_text=d["tts_text"], seed=d["seed"], tries=len(d["tries"]),
                   dry_cer=d["cer"], dry_cer_strict=d["cer_strict"], dry_heard=d["heard"], similarity_to_anchor=(s.get("sim") or {}).get(l["id"]),
                   strength=f["strength"], final_cer=f["final_cer"], final_heard=f["final_heard"], duration=f["duration"], chars_per_s=f["chars_per_s"],
                   lufs=f["lufs"], true_peak_db=f["true_peak_db"], ok=bool(f["ok"]))
        rows.append(row)
        if not row["ok"]:
            failed.append(dict(id=l["id"], voice=l["voice"], text=l["text"], heard_dry=d["heard"], heard_final=f["final_heard"],
                               dry_cer=d["cer"], final_cer=f["final_cer"]))
    anchors = {}
    for v in voices.VOICES:
        a = load_json(os.path.join(E.ANCHORS, v + ".json"))
        if a:
            anchors[v] = dict(description=a["desc"], anchor_text=a["text"], seed=a["seed"], cer=a["cer"], seconds=a["seconds"],
                              median_f0=median_f0(a["wav"]), nearest_other_voice=(s.get("nearest") or {}).get(v))
    sp = load_json(os.path.join(E.PROD, "speed.json"), {}) or {}
    rep = dict(model="VoxCPM2 (openbmb/VoxCPM2), voice design, no reference audio", generated=time.strftime("%Y-%m-%d %H:%M"),
               device=f"CPU, {E.cpu_cap.THREADS} threads, float32",
               speed=dict(compute_seconds=sp.get("compute"), audio_seconds=sp.get("audio"),
                          seconds_per_second=round(sp["compute"] / sp["audio"], 1) if sp.get("audio") else None),
               lines_total=len(lines), lines_ok=sum(r["ok"] for r in rows),
               lines_failed=failed, lines_missing=missing, retried=sum(1 for r in rows if r["tries"] > 1),
               regenerated_for_drift=load_json(os.path.join(E.PROD, "regenerated.json"), {}),
               consistency=dict(per_voice=s.get("per_voice"), different_voices_mean=s.get("cross_mean"), different_voices_max=s.get("cross_max")),
               boss_strength=load_json(BOSS_FX, {}) or {}, voices=anchors, lines=rows)
    json.dump(rep, open(REPORT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    E.log(f"report: {rep['lines_ok']} ok, {len(failed)} failed, {len(missing)} missing -> {REPORT}", LOG)
    return rep


def other_generator_running() -> list[str]:
    """Command lines of processes that belong to the fallback tool Y:\\tools\\voice (not ...\\voice_tw)."""
    # voice.exe is the tool's own program (also what `dotnet run` starts); a dotnet process counts when its
    # command line names the tool's folder (regex: tools[\\/]voice not followed by _tw)
    ps = ("Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'voice.exe' -or ($_.Name -match '^dotnet' -and "
          "$_.CommandLine -match 'tools[\\\\/]voice(?!_tw)') } | ForEach-Object { $_.Name + ' ' + $_.CommandLine }")
    r = subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, text=True, encoding="utf-8", errors="replace")
    return [x for x in r.stdout.splitlines() if x.strip() and "Get-CimInstance" not in x]


def do_deliver(lines: list[dict]) -> dict:
    for attempt in range(6):
        busy = other_generator_running()
        if not busy:
            break
        E.log(f"deliver: the fallback generator is running ({busy[0][:120]}); waiting 30 s", LOG)
        time.sleep(30)
    else:
        E.log("deliver: the fallback generator is still running - nothing copied", LOG)
        return dict(copied=0, skipped="fallback generator running")
    os.makedirs(GAME_VO, exist_ok=True)
    copied, same, left = [], [], []
    for l in lines:
        f = load_json(os.path.join(E.FINAL, l["id"] + ".json"))
        src = os.path.join(E.FINAL, l["id"] + ".ogg")
        dst = os.path.join(GAME_VO, l["id"] + ".ogg")
        if f and f.get("ok") and os.path.exists(src):
            if os.path.exists(dst) and os.path.getsize(dst) == os.path.getsize(src) and open(dst, "rb").read() == open(src, "rb").read():
                same.append(l["id"])                                # identical already: do not touch it (no re-import in the engine)
            else:
                shutil.copyfile(src, dst)
                copied.append(l["id"])
        else:
            left.append(l["id"])
    E.log(f"deliver: {len(copied)} files copied to {GAME_VO}, {len(same)} already identical; {len(left)} left as they are: {left}", LOG)
    return dict(copied=len(copied), same=len(same), left=left)


def adopt_osaros(variant: str) -> None:
    """Make one of the choice-page descriptions the production voice of boss_osaros."""
    src = os.path.join(E.ANCHORS, f"boss_osaros@{variant}")
    a = load_json(src + ".json")
    if not a:
        raise SystemExit(f"no anchor for variant {variant}; run osaros_page.py first")
    if voices.VOICES["boss_osaros"]["desc"] != a["desc"]:
        raise SystemExit(f"set OSAROS_DEFAULT = \"{variant}\" in voices.py first (the description must match the anchor)")
    shutil.copyfile(src + ".wav", os.path.join(E.ANCHORS, "boss_osaros.wav"))
    a.update(key="boss_osaros", wav=os.path.join(E.ANCHORS, "boss_osaros.wav"), uid=a.get("uid") or f"boss_osaros@{variant}-{a.get('seed')}")
    json.dump(a, open(os.path.join(E.ANCHORS, "boss_osaros.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    take = os.path.join(vt.ROOT, "stage", "osaros", f"osaros_{variant}")
    m = load_json(take + ".json")
    if m:                                                           # the intro line was already made with this anchor
        os.makedirs(E.DRY, exist_ok=True)
        shutil.copyfile(take + ".wav", os.path.join(E.DRY, "boss_osaros_intro.wav"))
        m.update(anchor_key="boss_osaros", anchor_uid=a["uid"], wav=os.path.join(E.DRY, "boss_osaros_intro.wav"))
        json.dump(m, open(os.path.join(E.DRY, "boss_osaros_intro.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    for i in ("boss_osaros_phase", "boss_osaros_death"):            # made with another anchor, if at all: regenerate
        for ext in (".wav", ".json"):
            p = os.path.join(E.DRY, i + ext)
            d = load_json(os.path.join(E.DRY, i + ".json"))
            if os.path.exists(p) and d and d.get("anchor_uid") != a.get("uid"):
                os.remove(p)
    E.log(f"boss_osaros now uses the description '{variant}'", LOG)


# ------------------------------------------------------------------ command line

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["all", "anchors", "lines", "drift", "fx", "report", "deliver", "osaros", "audit"])
    ap.add_argument("--only"); ap.add_argument("--voice"); ap.add_argument("--force", action="store_true")
    ap.add_argument("--fix", action="store_true"); ap.add_argument("--strength", choices=["dry", "light", "medium"])
    ap.add_argument("--variant")
    args = ap.parse_args()
    lines = script()
    sel = select(lines, args)
    if args.cmd == "osaros":
        adopt_osaros(args.variant)
        if args.strength:
            cfg = load_json(BOSS_FX, {}) or {}
            cfg["boss_osaros"] = args.strength
            json.dump(cfg, open(BOSS_FX, "w", encoding="utf-8"), indent=1)
        print("now run:  gen.py lines --voice boss_osaros ; gen.py fx --voice boss_osaros ; gen.py report ; gen.py deliver")
        return
    if args.cmd == "audit":                                         # measure what exists, rewrite the table in DECISION.md
        for v in dict.fromkeys(l["voice"] for l in sel):
            if os.path.exists(os.path.join(E.ANCHORS, voice_key(v) + ".json")):
                vl = [l for l in lines if l["voice"] == v]
                old = (load_json(AUDIT, {}) or {}).get(v) or {}
                res = audit_voice(v, vl[:3])
                action = old.get("action") or ("kept" if res["passed"] else "kept: chosen by the user by ear on the choice page" if v in USER_APPROVED else "FAILED - to be redesigned")
                save_audit(v, res, action, old.get("history", []))
                print(v, res)
        write_audit_table()
        return
    need_model = args.cmd in ("all", "anchors", "lines") or (args.cmd == "drift" and args.fix)
    long_job = args.cmd in ("all", "anchors", "lines")
    eng = None
    try:
        if long_job:
            E.running_file("gen.py " + " ".join(sys.argv[1:]), LOG)
        eng = E.Engine(LOG) if need_model else None
        asr = eng.asr if eng else (vt.Asr(threads=4) if args.cmd == "fx" else None)
        if args.cmd == "all":
            # One voice at a time, start to finish: anchor (designed, measured, checked against the other voices),
            # lines, consistency repair, effects, delivery, report. An interrupted run loses at most one voice's
            # work in progress, and finished voices are already in the game. Voices that have an anchor go first.
            order = sorted(dict.fromkeys(l["voice"] for l in sel),
                           key=lambda v: (0 if os.path.exists(os.path.join(E.ANCHORS, voice_key(v) + ".json")) else 1,
                                          0 if v.startswith("boss_") else 1))
            for n, v in enumerate(order, 1):
                vl = [l for l in sel if l["voice"] == v]
                E.log(f"=== voice {n}/{len(order)}: {v} ({len(vl)} lines)", LOG)
                designed_voice(eng, v, vl, force=args.force)       # anchor + three lines, audited (pitch, male / female)
                do_lines(eng, vl, force=args.force)
                do_drift(eng, vl, fix=True, save=False)
                do_fx(asr, vl)
                retake_locked(eng, asr, vl)
                do_deliver(vl)
                do_report(lines, do_drift(None, lines, fix=False))
                E.log(f"speech so far in this run: {eng.audio:.0f} s in {eng.compute:.0f} s of compute "
                      f"({eng.compute / max(eng.audio, 1):.1f} s per second of audio)", LOG)
            return
        if args.cmd == "anchors":
            do_anchors(eng, args.voice)
        if args.cmd == "lines":
            do_lines(eng, sel, force=args.force)
        s = None
        if args.cmd == "drift":
            s = do_drift(eng, sel, fix=args.fix)
        if args.cmd == "fx":
            do_fx(asr, sel, strength=args.strength, force=args.force)
        if args.cmd == "report":
            do_report(lines, do_drift(None, lines, fix=False))
        if args.cmd == "deliver":
            do_deliver(sel)
        if eng:
            E.log(f"speech generated in this run: {eng.audio:.0f} s in {eng.compute:.0f} s of compute "
                  f"({eng.compute / max(eng.audio, 1):.1f} s per second of audio)", LOG)
    finally:
        if eng and eng.audio > 0:                                   # running totals for the report
            sp = load_json(os.path.join(E.PROD, "speed.json"), {}) or {}
            sp["compute"] = round(sp.get("compute", 0) + eng.compute)
            sp["audio"] = round(sp.get("audio", 0) + eng.audio, 1)
            json.dump(sp, open(os.path.join(E.PROD, "speed.json"), "w", encoding="utf-8"))
        if long_job:
            E.done()


if __name__ == "__main__":
    main()
