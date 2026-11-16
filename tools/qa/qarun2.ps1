# qarun2.ps1 -GameArgs "<switches>" -Timeout <s> [-LockWait <s>] [-Headless]
# Foreground wrapper round Y:\tools\run.ps1 for QA. It queues on the machine-wide run lock like every other caller
# (a blocking wait, so it gets its turn), but only for -LockWait seconds: a tool command must end within three
# minutes here, so the whole call is bounded by LockWait + build + Timeout. The lock is taken on this thread and
# run.ps1, called in-process, re-enters it; build and run are still done by run.ps1 alone.
param([string]$GameArgs = '', [int]$Timeout = 100, [int]$LockWait = 45, [switch]$Headless)
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
$sw = [Diagnostics.Stopwatch]::StartNew()
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromSeconds($LockWait)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "LOCK BUSY for ${LockWait}s - nothing was run"; exit 4 }
    "lock after $([int]$sw.Elapsed.TotalSeconds)s"
    if ($Headless) { & Y:\tools\run.ps1 -GameArgs $GameArgs -Timeout $Timeout -Tag qa -Headless }
    else { & Y:\tools\run.ps1 -GameArgs $GameArgs -Timeout $Timeout -Tag qa }
    "total $([int]$sw.Elapsed.TotalSeconds)s"
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
