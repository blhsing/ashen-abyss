#!/usr/bin/env bash
# qarun.sh "<game args>" <timeout> [extra run.ps1 switches...]
# Foreground wrapper round Y:\tools\run.ps1. It never outlives QA_DEADLINE seconds (default 560, counted from the
# moment it starts): it waits for the machine-wide run lock only while a whole run still fits, retries while someone
# else's half-finished edit breaks the shared build, and says so and stops when time runs out.
# QA_START (epoch seconds) may be exported by a caller that chains several runs under one deadline.
args="$1"; timeout="${2:-120}"; shift 2
start="${QA_START:-$(date +%s)}"; deadline="${QA_DEADLINE:-560}"
left() { echo $(( deadline - ($(date +%s) - start) )); }
mine_re='^src/(Game|TestBot|TestSuite|TestSuite\.Anims|TestSuite\.Shots|Player|SkillExec|Combat|Actor|Monster|Projectile|GroundZone|Interactable|World|Level|LevelGen|Populate|WorldObjects|WorldEvents|Loot|Quests|Dialogue|BossBrain|EliteFx|Hero|Hero\.Paragon|Hero\.Crafting|Crafting|Paragon|Items|Save|SkillDef|SkillsData|MonstersData|AudioMan|Snd|CharModel|Defs|Log|WinKeys)\.cs\('
probe='$m=New-Object System.Threading.Mutex($false,"Local\AshenAbyssRun"); $ok=$false; try { $ok=$m.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $ok=$true }; if ($ok) { $m.ReleaseMutex(); "FREE" } else { "BUSY" }; $m.Dispose()'
need=$(( timeout + 45 ))
while true; do
  if [ "$(left)" -lt "$need" ]; then echo "NO TIME LEFT for: $args (needs ${need}s, $(left)s left)"; exit 3; fi
  state=$(powershell -NoProfile -Command "$probe" 2>/dev/null | tr -d '\r')
  if [ "$state" != "FREE" ]; then sleep 6; continue; fi
  out=$(cd /y && powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Timeout "$timeout" -Tag qa "$@" 2>&1)
  if echo "$out" | grep -q "BUILD FAILED"; then
    mine=$(echo "$out" | tr '\' '/' | grep -E "$mine_re")
    if [ -n "$mine" ]; then echo "BUILD FAILED IN MY FILES:"; echo "$mine" | head -30; exit 1; fi
    echo "[build broken by others: $(echo "$out" | tr '\' '/' | grep -oE 'src/[A-Za-z.]+\.cs' | sort -u | tr '\n' ' ')]" >&2
    sleep 25
    continue
  fi
  echo "$out"
  exit 0
done
