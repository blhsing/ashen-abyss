# Starts the resumable production batch as ONE detached background process (it holds its own keep-awake request
# and a lock, so a second start while one is running exits immediately).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\run_batch.ps1 [-Tracks all|pilot|a,b] [-Device cuda|cpu]
#        [-Stage <folder>] [-ModelDir <Diffusers model folder>] [-ModelId "<name for the metadata>"] [-OnlyEval] [-Chunk 4] [-Deliver] [-MaxSeeds 3]
#
# While it runs, Y:\tools\music_ai\RUNNING.txt holds the process ids, the log path and the stop command;
# the batch deletes that file when it ends.
# Progress:  Get-Content <stage>\produce.log -Tail 20      (generator output: gen.log next to it)
param([string]$Tracks = 'all', [string]$Device = 'cuda', [string]$Stage = '', [string]$ModelDir = '', [string]$ModelId = '', [switch]$OnlyEval, [int]$Chunk = 0, [switch]$Deliver, [int]$MaxSeeds = 0, [int]$FirstSeeds = 0)
. "$PSScriptRoot\env.ps1"
$env:PYTHONWARNINGS = 'ignore'
if (-not $Stage) { $Stage = "$MA_ROOT\stage\ace_xl" }
New-Item -ItemType Directory -Force -Path $Stage | Out-Null
$py = "$MA_ROOT\venvs\ace\Scripts\python.exe"
$argv = @("`"$PSScriptRoot\produce.py`"", '--tracks', $Tracks, '--device', $Device, '--stage', "`"$Stage`"")
if ($ModelDir) { $argv += @('--model-dir', "`"$ModelDir`"") }
if ($ModelId) { $argv += @('--model-id', "`"$ModelId`"") }
if ($OnlyEval) { $argv += '--only-eval' }
if ($Chunk -gt 0) { $argv += @('--chunk', "$Chunk") }
if ($Deliver) { $argv += '--deliver' }
if ($FirstSeeds -gt 0) { $argv += @('--first-seeds', "$FirstSeeds") }
if ($MaxSeeds -gt 0) { $argv += @('--max-seeds', "$MaxSeeds") }
$p = Start-Process -FilePath $py -ArgumentList $argv -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$Stage\produce.out" -RedirectStandardError "$Stage\produce.err"
"batch started (launcher pid $($p.Id)); log: $Stage\produce.log; see Y:\tools\music_ai\RUNNING.txt"
