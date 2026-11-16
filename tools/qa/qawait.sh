#!/usr/bin/env bash
# qawait.sh [seconds]  — waits (in the foreground, at most the given time, default 150 s) until the run lock is free.
probe='$m=New-Object System.Threading.Mutex($false,"Local\AshenAbyssRun"); $ok=$false; try { $ok=$m.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $ok=$true }; if ($ok) { $m.ReleaseMutex(); "FREE" } else { "BUSY" }; $m.Dispose()'
end=$(( $(date +%s) + ${1:-150} ))
while [ "$(date +%s)" -lt "$end" ]; do
  s=$(powershell -NoProfile -Command "$probe" 2>/dev/null | tr -d '\r')
  [ "$s" = "FREE" ] && { echo FREE; exit 0; }
  sleep 4
done
echo BUSY; exit 1
