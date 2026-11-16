# Renders the same 10 seconds (same prompt, same seed, same initial noise) on every acceleration path, one after
# the other (never two at once), then compares each result with the CPU reference.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\bench_paths.ps1 [-Paths cpu,rocm32,rocm16,dml32] [-Seconds 10]
param([string[]]$Paths = @('cpu', 'rocm32', 'rocm16', 'dml32'), [double]$Seconds = 10, [string]$Track = 'boss', [int]$Seed = 1)
. "$PSScriptRoot\env.ps1"
$env:PYTHONWARNINGS = 'ignore'
$out = "$MA_ROOT\stage\bench"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Set-Location $out
$cfg = @{
    cpu    = @{ venv = 'cpu'; args = @('--device', 'cpu', '--dtype', 'float32') }
    rocm32 = @{ venv = 'ace'; args = @('--device', 'cuda', '--dtype', 'float32') }
    rocm16 = @{ venv = 'ace'; args = @('--device', 'cuda', '--dtype', 'float16') }
    rocmbf = @{ venv = 'ace'; args = @('--device', 'cuda', '--dtype', 'bfloat16') }
    dml32  = @{ venv = 'dml'; args = @('--device', 'dml', '--dtype', 'float32') }
    dml16  = @{ venv = 'dml'; args = @('--device', 'dml', '--dtype', 'float16') }
}
$Paths = @($Paths | ForEach-Object { $_ -split "," } | Where-Object { $_ })   # -File passes "a,b" as one string
$files = @()
foreach ($p in $Paths) {
    $c = $cfg[$p]
    $py = "$MA_ROOT\venvs\$($c.venv)\Scripts\python.exe"
    $tag = "_$($p)_$([int]$Seconds)s"
    "== $p"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $py "$PSScriptRoot\gen_ace.py" --out $out --tracks $Track --seeds $Seed --seconds $Seconds --suffix $tag @($c.args) 2>&1 |
        ForEach-Object { "$_" } | Tee-Object -FilePath "$out\log$tag.txt" | Select-Object -Last 6
    "   exit code $LASTEXITCODE, wall time {0:N0} s" -f $sw.Elapsed.TotalSeconds
    $f = "$out\$($Track)__s$Seed$tag.flac"
    if (Test-Path $f) { $files += $f }
}
if ($files.Count -ge 1) {
    & "$MA_ROOT\venvs\ace\Scripts\python.exe" "$PSScriptRoot\compare_paths.py" @files 2>&1 | ForEach-Object { "$_" }
}
