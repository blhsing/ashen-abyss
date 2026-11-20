## STATE WHEN PAUSED (2026-11-21 00:56, lead's order: all work paused for the 0.3.0 release)
- **All 46 creatures are in `Y:\game\assets\chars`, complete, and all 46 PASS `bverify.py`** (full run 00:40 on `out`,
  `h1\out` .. `h4\out`; `lurk_leech` rebuilt and re-verified 00:53). `rigs_bf_meta.json` = exactly these 46 models, every
  entry has all contract fields (10 with `hover_in_model`, 5 with a `light` entry: lantern, flicker, lure, gadfly, fiend
  wing). Godot's import cache is newer than every one of the files (checked 00:55; `import.ps1` exit 0).
- No Blender job, import or game run of mine is running or queued; all four helpers have ended.
- Delivered: 46 strips `Y:\shots\chars\beast_mon_<id>_anim.jpg`, 18 family sheets `beast_<family>.jpg`, four dark game-view
  sheets `beast_gameview_1..4.jpg` (the leech tile in sheet 3 and in `beast_lurk.jpg` is one small colour tweak behind:
  re-run `bshots.py "lurk_leech --tile --game"` is done, re-run `scratch\family_sheets.ps1` to refresh the sheets),
  proof overview `beast_proofs_def.jpg`, five proof WebPs `Y:\shots\preview\beast_<b..f>_proof.webp`, credits
  `Y:\credits\beasts.md`, the table at the end of this file (`python -I btable.py` regenerates `out\table.md`).
- **NOT DONE: the in-engine check.** No creature of rigs B-F has been looked at in the game by me. The check is
  prepared: `bash scratch/engine_check.sh 1 11` (eleven `--bestiary --spawn` line-ups on test slot 96, pictures
  `out\engine\g<N>_<1|2>.png`, never runs while AshenAbyss.exe is open, moves slot96* to saves_test_backup at the end).
  It is blocked by the file `scratch\PAUSED`: delete that file first. Unchecked in the engine: material prefixes
  (`hair_`, `spectral_ghost` on the lantern's smoke), emission clamp / pulse, hover heights, the meta lights, the
  `rust_gear` wheel spin, near-zero and non-uniform bone scales, 360-degree turns in looping clips (`hush_serpent`).
- **Next when resumed:** (1) delete `scratch\PAUSED`, run the engine check, look at every picture, fix what it shows;
  (2) `powershell -File scratch\family_sheets.ps1`; (3) final report to the lead (table, shared skeletons, deviations,
  what could not be checked).
- Lead's review bar (2026-11-20/21, proofs approved): every creature must read as a creature at game-camera size on
  dark ground - real head / face, S-bends instead of straight columns, one bright accent, enlarged where it was a
  speck. Applied after the helpers finished: crow (slate feathers, smouldering primaries), locust (violet static),
  digger (paler soil), hound (larger heat pits), spitter (S-bend, dark feathered frill with glowing tips), elder
  (dimmer belly), leech x1.3 (lighter skin, pale sucker lip), grub x1.25.
- Kit additions since the first pause: `Beast.rescale(K)`; `bshots.py` game view = the REAL camera (CameraRig.cs: pitch 48,
  15.6 m, FOV 32 -> 121 px per metre; ARCHITECTURE.md's 24 m is out of date) on dark ground, `_game2.png` = Run,
  `spectral_*` materials previewed as additive ghost matter, a preview light from the meta `light` entry, margins 1.2;
  look kinds `slick`, `warm`, `ghostface`, `frond`, `sglow`, `tipglow`, `cglass`; `rig_d.Looper` rewritten (flat end links,
  solved hump), `Rearer.tailwave` runs backwards; meta `light` entry (the roster engineer hangs a soft shadowless light
  on the creature, max 8 live; `bmeta.py` LIGHTS table); `bsheets.py`, `btable.py`; `HELPERS.md` has the helpers' lessons.
- Rule learnt the hard way: never write Python through bash heredocs (quotes break); write a patch file with the
  Write tool (`scratch\patch_*.py`, `.new` + `os.replace`) and run it. In bash, quote Windows paths ('Y:\...').

