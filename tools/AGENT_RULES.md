# Rules for every agent working on 燼淵 Ashen Abyss (read before you start)

Project root is `Y:\` (a `subst` drive; always use `Y:\` paths). Game: `Y:\game` (Godot 4.7.2 .NET, C#, `game/src`).
Shared docs: `Y:\DESIGN.md`, `Y:\BESTIARY.md`, `Y:\ARCHITECTURE.md` (zh-TW), `Y:\QA_NOTES.md` (zh-TW).
The project is a public GitHub repository (`blhsing/ashen-abyss`); only the lead commits, pushes and uploads.

## Machine safety (a laptop: Ryzen AI Max+ 395, Radeon 8060S iGPU, 64 GB shared memory)
- Blender ONLY through `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\run_blender.ps1 -A @('-b','--factory-startup','--python','<script>','--',…)`.
  It queues on a machine-wide two-job limit, caps threads and forces background mode. Never start blender.exe
  directly, never open any application window on the user's desktop, never use the Blender MCP server. Look at
  pictures with your file-reading tool.
- Never update drivers, never reboot, never change system settings (proxy, power, display).
- Never run `Y:\tools\stop_all.ps1` (lead only). `Y:\tools\who_runs.ps1` is a read-only process listing.
- Never kill processes you did not start yourself.

## The user's game and data
- The user plays the installed game (`%USERPROFILE%\Games\AshenAbyss`). BEFORE EVERY test run of the game:
  `tasklist | findstr /i AshenAbyss` must print nothing; if it does, wait and poll every 30 s. Never start a test
  while the user plays. Test launches use their own instance lock and exit silently while the user's game runs.
- Never touch `%USERPROFILE%\Games\AshenAbyss`, the Start-menu shortcut `燼淵 Ashen Abyss.lnk`, or the user's saves
  (`%APPDATA%\AshenAbyss\saves\slot1..8.*`). Test saves use `--slot=90..99`; afterwards move
  `%APPDATA%\AshenAbyss\saves\slot9*` (and `slot0*`, which the UI test layer writes) into
  `%APPDATA%\AshenAbyss\saves_test_backup\`.
- Never run `Y:\tools\install.ps1` (lead only).

## Building and testing
- Compile check of your files: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\check.ps1 -Files A.cs,B.cs`
  (whole project without `-Files`). Leave every file you touch in a compiling state at all times — others build too.
- Test run: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "<ONE space-separated string>" -Timeout <s> -Tag <you> [-NoBuild] [-Headless] [-EngineArgs "..."]`
  (always `--win --mute` for windowed runs; switches are listed in ARCHITECTURE.md and QA_NOTES.md). It queues on a
  machine-wide run lock. Pictures for an empty profile (no personal save shown): `Y:\tools\repo\fresh_profile_run.ps1`.
- After adding or replacing asset files: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1`.
- Run anything longer than ~2 minutes in the background; never leave detached processes behind without recording
  them (pid, how to stop, how to resume) in your notes file.

## Files and ownership
- No scratch files in the `Y:\` root (it is the repository root). Use your own folder under `Y:\tools\<area>\`.
- Only edit files your brief gives you; for anyone else's file make the smallest additive change and report it.
- Binary assets (models, textures, audio, images) never go into git; the `.gitignore` handles that.
- Keep a running notes/progress file (named in your brief) so the work can be resumed after an interruption.
- Do not commit, push, create releases or upload anything.

## Quality bar (the user's standing requirements)
- Original designs only (no recreation of any existing game's characters, art, music, levels or look).
- All in-game text Traditional Chinese (Taiwan); minimum font 20 px at 1080p.
- Realistic, fine-looking textures — never smooth clay; nothing floating or poking through.
- Dark-fantasy look: dungeons dark, outdoors slightly lifted; characters must not glow brighter than their surroundings.
- Raster art only (no vector icons); ornate, commercial-grade UI framing.
- Report facts: say plainly what you did not test or could not achieve.

## While the user plays
- When `AshenAbyss.exe` is running, start any heavy background job (Python generators, Blender, 7-Zip) at Idle
  priority (`(Get-Process -Id <pid>).PriorityClass = 'Idle'`); run_blender.ps1 does this by itself.
