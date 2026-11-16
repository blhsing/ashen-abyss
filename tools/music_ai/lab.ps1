# Runs a prompt experiment detached; output goes to %MA_ROOT%\logs\lab_<name>.log
#   lab.ps1 -Spec Y:\tools\music_ai\lab\boss.json -Seconds 30 -Seeds 1,2,3
param([Parameter(Mandatory)][string]$Spec, [double]$Seconds = 30, [string]$Seeds = '1,2,3')
. "$PSScriptRoot\env.ps1"
$env:PYTHONWARNINGS = 'ignore'
$name = [IO.Path]::GetFileNameWithoutExtension($Spec)
$p = Start-Process -FilePath "$MA_ROOT\venvs\ace\Scripts\python.exe" -ArgumentList @("$PSScriptRoot\prompt_lab.py", $Spec, '--seconds', "$Seconds", '--seeds', $Seeds) `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput "$MA_ROOT\logs\lab_$name.log" -RedirectStandardError "$MA_ROOT\logs\lab_$name.err"
"lab pid $($p.Id) -> $MA_ROOT\logs\lab_$name.log"
