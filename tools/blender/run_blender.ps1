# Throttled Blender launcher. EVERY Blender run for this project must go through this script.
#
#   & Y:\tools\blender\run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\tools\x.py','--','arg1')
#
# Why: several Blender processes at once (Cycles bakes on all 32 threads + EEVEE on the integrated GPU) hung this
# laptop when Windows started Modern Standby in the middle of the renders. This wrapper therefore
#   * lets at most 2 Blender processes run at the same time machine-wide (named semaphore), waiting for a slot;
#   * holds the system and display awake for the duration of the run, so standby cannot start mid-render;
#   * caps Blender's thread count (-t, default 12; override with $env:ASHEN_BLENDER_THREADS);
#   * runs Blender at BelowNormal priority so the desktop stays responsive.
# Output (stdout + stderr) is written to the pipeline line by line; the exit code is Blender's ($LASTEXITCODE).
param([string[]]$A)
if (-not $A -or $A.Count -eq 0) { $A = $args }
if (-not $A -or $A.Count -eq 0) { Write-Output "run_blender.ps1: no arguments"; exit 2 }

$bl = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
$threads = 12
if ($env:ASHEN_BLENDER_THREADS -match '^\d+$') { $threads = [Math]::Max(1, [Math]::Min(32, [int]$env:ASHEN_BLENDER_THREADS)) }

if (-not ('AshenAbyss.Power' -as [type])) {
    Add-Type -Namespace AshenAbyss -Name Power -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);'
}

function Quote-Arg([string]$s) {
    if ($s -eq '') { return '""' }
    if ($s -notmatch '[\s"]') { return $s }
    return '"' + ($s -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

$sem = New-Object System.Threading.Semaphore(2, 2, 'Local\AshenAbyssBlender')
$got = $false
$code = 1
try {
    # wait for a slot (up to 60 minutes; a crashed holder must not block the queue forever)
    $got = $sem.WaitOne([TimeSpan]::FromMinutes(60))
    if (-not $got) { Write-Output "run_blender.ps1: WARNING no slot after 60 min, continuing anyway" }
    [void][AshenAbyss.Power]::SetThreadExecutionState([uint32]2147483651)   # ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED

    $all = @('-t', "$threads") + $A
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $bl
    $psi.Arguments = ($all | ForEach-Object { Quote-Arg $_ }) -join ' '
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    [void]$p.Start()
    try { $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal } catch { }
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    $p.WaitForExit()
    $code = $p.ExitCode
    foreach ($txt in @($outTask.Result, $errTask.Result)) {
        if ($txt) { $txt -split "`r?`n" | ForEach-Object { if ($_ -ne '') { Write-Output $_ } } }
    }
}
finally {
    [void][AshenAbyss.Power]::SetThreadExecutionState([uint32]2147483648)   # ES_CONTINUOUS (clear)
    if ($got) { [void]$sem.Release() }
    $sem.Dispose()
}
exit $code
