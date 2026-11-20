# Beast helpers - how to build creatures with this kit (read fully before you start)

You build game-ready NON-HUMANOID monsters for 燼淵 Ashen Abyss (dark-fantasy ARPG, Godot 4.7.2, top-down camera: pitch 48 deg,
15.6 m away at zoom 1 - a 1 m creature is about 120 px tall on a 1080p screen, so SILHOUETTE, big shapes, palette and motion matter
most; micro detail comes from the baked looks). Mandatory rules: `Y:\tools\AGENT_RULES.md` (Blender ONLY through the
launcher below, background mode, never a window on the user's desktop, no scratch files in `Y:\`).

## Read first (in this order)
1. `Y:\tools\AGENT_RULES.md`, `Y:\tools\blender\beasts\PROGRESS.md` (facts, conventions, rotation signs, design decisions).
2. `blib.py` (skeleton `Sk`, `Pose`: rot / setR / move / scl / leg / aim_world, `Leg`, `gait_foot`, `plant_legs`, `wbones`,
   `Beast`: look / rigid / skin / clip / reach / finalize), `bparts.py` (look kinds + part library), your rig module
   (`rig_b.py` or `rig_c.py`) and the proof spec that uses it (`spec_b.py` dusk_hound / `spec_c.py` sexton_beetle).
3. The monsters kit it builds on: `Y:\tools\blender\monsters\monlib.py` (Part, primitives: tube, loft_z, ellipsoid, box,
   rock, hull, torus, chain, horn, hang, sheet ...; boolean, remesh, decimate) and `looks.py` (look kinds and their
   parameters: bone, metal + PRESET, cloth, leather, skin, rock, glass, wax, wood, emis, spectral, flat, ice, moss,
   mud, fur; plus ours: chitin, scales, slime, bfur, membrane, chalk, bark, glow, cglass).
4. Your creatures in `Y:\game\data\bestiary.json` (`types[]`: id, model, name_zh, family, rig, scale, speed, role, anims,
   silhouette = THE ART BRIEF) and their family chapter in `Y:\BESTIARY.md`.
5. Look at the proofs: `Y:\tools\blender\beasts\out\shots\mon_dusk_hound_tile.png`, `mon_sexton_beetle_tile.png`,
   `Y:\shots\chars\beast_mon_sexton_beetle_anim.jpg`, and the humanoid bar `Y:\shots\preview\monsters_1.jpg`.

## Contract of every file (the game codes against it)
- `Y:\game\assets\chars\<model>.glb` (model = `mon_<id>`): +Y up, on the ground at the origin, metres, built facing -Y in
  Blender (+X = the creature's left). Skinned mesh + armature + EMBEDDED clips named exactly `Idle Walk Run Attack1
  Attack2 Hit Death Spawn`; Idle / Walk / Run loop seamlessly; root bone unkeyed (motion on its first child); 30 fps.
- Author at brief size / `scale` (the game multiplies by scale). `run_speed` = bestiary speed / scale; `walk_speed`
  about 0.3-0.4 of it. Planted feet must not slide more than 3 cm at those speeds (the gait code guarantees it when the
  legs can reach: watch the "IK target out of reach" notes and `foot drift` in the build log).
- Attack clips: `hit=` seconds of the strike; call `reach()` so the meta gets `attack_reach`. What Attack1 / Attack2
  show is in the type's `anims` field.
- Budgets: regular <= 15,000 tris, <= 6 MB, <= 60 bones; swarm-role creatures (role "swarmer"/"swarm": small, many on
  screen) <= 4,000 tris, <= 2.5 MB, <= 28 bones -> `B.Beast(id, sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)`;
  giants (scale >= 1.7) <= 22,000 tris -> `budget=22000`.
- Materials: slots named `skin_*`, `hair_*` (fur), `bone_*` (chitin, horn, teeth, bone), `leather_*`, `metal_*`, `rock_*`,
  `cloth_*`, `emis_*` (glowing parts: only where the brief has an accent). At most 5 slots. Dark palette; nothing glows
  brighter than a torch (emission strength 1.5-2.5).
- Thin parts must be closed slabs (`P.sheet_solid`, `Part.solidify`, `B.two_sided`): the game culls back faces.
- Nothing floating, nothing poking through: the build's audit lines `WARNING floating in rest pose` / `WARNING detaches in
  <clip>` must be gone (sink roots into their host, put joints on the bone head) or explained.
- Realistic surfaces (never clay), silhouettes instantly readable and DISTINCT within the family.

## Your folder and commands (replace hN by your folder)
- Your spec module: `Y:\tools\blender\beasts\hN\spec_hN.py` with `BUILD = {}` and one function per creature id
  (copy the header of `spec_c.py`). Shared files `blib.py`, `bparts.py` and other rigs' modules are READ-ONLY for you:
  put extensions (new look kinds via `L.KINDS.update`, new part functions, subclasses) in your own module and report
  them. The rig module named in your brief is yours to extend (keep the proof creature building).
- Build:  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bb.ps1 bbuild.py "<id> [<id>...] --path=Y:\tools\blender\beasts\hN --spec=spec_hN --out=Y:\tools\blender\beasts\hN\out [--nobake]"`
  (with bake 1-2 min per creature and the file goes to `Y:\game\assets\chars`; `--nobake` = fast grey preview .glb in your
  out folder for skeleton / clip debugging). Blender runs are queued machine-wide (2 at a time): batch several ids
  in one run, use `run_in_background` for anything long, and never start a second copy of a running job.
- Renders: `... bb.ps1 bshots.py "<ids> --tile --game --strip --dbg --out=Y:\tools\blender\beasts\hN\out\shots --meta=Y:\tools\blender\beasts\hN\out"`
  (for a `--nobake` preview add `--src=Y:\tools\blender\beasts\hN\out`). LOOK at `<model>_dbg1.jpg` (Idle 2, Walk 4,
  Run 4, Hit 2) and `<model>_dbg2.jpg` (Attack1 3, Attack2 3, Death 3, Spawn 3), `<model>_tile.png` and `<model>_game.png`
  (true game size) for EVERY creature. Close-ups: `--view=name:yaw:pitch:tx:ty:tz:dist[:clip:time]`.
- Verify (plain python, no Blender): `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I Y:\tools\blender\beasts\bverify.py <ids> --meta=Y:\tools\blender\beasts\hN\out`
  -> every creature must print PASS.
- Strip:  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bstrip.ps1 mon_a,mon_b -Dir Y:\tools\blender\beasts\hN\out\shots`
  -> `Y:\shots\chars\beast_<model>_anim.jpg`.
- Do NOT run `import.ps1`, do not merge the meta, do not touch other agents' files, never run
  `Y:\tools\blender\monsters\verify.py`.
- Keep `Y:\tools\blender\beasts\hN\PROGRESS.md` current (state, what is running, one line per finished creature:
  `model | family | rig | tris | bones | MB | verify | honest weaknesses`).

## Working method
- One creature at a time: skeleton + clips with `--nobake` and dbg sheets first (fix poses, reach, ground contact),
  then geometry and looks with a bake, then verify + shots + strip. Aim at <= 3 build rounds per creature; reuse
  functions between the variants of one base (the bestiary's `variant_of` says which), but every creature needs its own
  silhouette, not a recolour.
- Clips are functions `fn(p, u)` of normalised time; the rig classes have stock clips with `extra=` hooks. Loops must
  use whole-number oscillation counts (`osc(u, n)` with integer n). One-shot clips start and end in the Idle frame-0 pose.
- Death must end on the ground (`floor=`, `settle=`), Spawn may start below it (`under_ok=True`).
- Report honestly at the end (the table, deviations from brief or contract, what you could not check). Your final
  message is read by the lead artist, keep it compact.

## Lessons from the first two helpers (23 creatures) - read them, they cost hours
- NEVER patch a spec with a script that opens it with 'w' directly (one helper truncated his spec to zero bytes): use the
  Edit tool, or write `<file>.new` and `os.replace` it. Never write Python through bash heredocs.
- The audit grows its attachment tree from SKINNED geometry; a creature made only of rigid parts gets the largest rigid
  piece as root, which gives nonsense hosts. Make the main body a skinned part (weights 100 % on one bone:
  `B.wbones(part, ['body'])` then `C.skin(part, look)`) so it is the root.
- The audit only looks 6 cm for a host and judges "inside" by the nearest triangle's normal: attach points 3-5 cm inside
  the host surface; faceted hosts (`M.rock`, boxes) near their edges give false "detaches" - use real overlap.
- Parts that are hidden by bone scale or that fall off (a thread, a dropped cinder, a lid) are added with `free=True`.
  A bone scaled to 0.001 hides its parts in a clip (`p.scl(bone, 0.001)`); remember the REST pose still shows them.
- Looks are baked in the REST pose in object space: build the rest pose in the state that should carry the light /
  pattern (e.g. wings spread, lid shut) and animate away from it.
- Emission: strength 1.0-1.6. Everything above blows out to flat white in the renders and in the game (the game clamps
  `emis_` energy to 1 .. 2.2 and adds a pulse; non-`emis_` slots with an emission texture glow without the pulse).
  Thin glowing lines read much brighter than you expect; dead bodies keep their emission, so keep it modest.
- `bshots.py` frames tiles and strips with a 1.2 margin (the sheet composer keeps the central 86 %): do not crop tighter.
- `bverify.py` foot-drift check: a contact point lower than 1.2 cm above its lowest height counts as planted. Put gait
  phases and duty on the frame grid (multiples of 1 / frames) and lift swinging feet more than 1.2 cm on their first frame.
- A full 360 degree turn inside a looping clip is fine (keys every frame; the loop is closed with the same quaternion).
- Death must END on the ground: `C.clip('Death', dur, fn, floor=<first child bone of root>, settle=(u0, u1))`.
- Judge `<model>_game.png` (400 px = 3.3 m on dark ground, the real size on a 1080p screen): several first-round creatures were specks;
  exaggerate the defining shape or (last resort, report it) build up to 1.4 x brief size.

## Flyers and floaters (rigs E / F)
- The hover height is INSIDE the model: the rest pose and every clip keep the body at its flying height above the origin
  (flyers 1.4-1.8 m to the body centre, small floaters as the brief says); `Flyer(C, h)` / `Rigid(C, bone, hover=h)` write
  `fly_height` and `hover_in_model: true` into the meta. Idle must not touch the ground (verify checks min y > 5 cm),
  Death falls and ends as a corpse on z = 0, Spawn drops in from above (or as the brief says).
- Turrets (speed 0) stand on the ground: Walk and Run are copies of Idle (`Rigid.copy_clip('Idle', 'Walk', 'walk_speed', 0.0)`
  and the same for Run); `hover` stays unset; Death = collapses / goes dark on the spot; Spawn = unfolds / rises.
- Wings are thin: closed slabs only (`RE.membrane`, `P.sheet_solid`). Insect wings that buzz use `buzz=True` (they simply
  alternate up / down every frame - at 30 fps that reads as a blur) and should be small, pale and semi-matte.
- Walk / Run of a flyer have no feet: `walk_speed` / `run_speed` are nominal (run = bestiary speed / scale).
