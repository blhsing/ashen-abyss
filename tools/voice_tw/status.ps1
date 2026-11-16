# One-screen status of the production batch:  powershell -ExecutionPolicy Bypass -File status.ps1
$root = "$env:LOCALAPPDATA\AshenAbyssTools\voice_tw"
$log = "$root\logs\gen.log"
$anch = @(Get-ChildItem "$root\stage\prod\anchors\*.json" -ErrorAction SilentlyContinue | Where-Object { $_.Name -notmatch '@' }).Count
$dry = @(Get-ChildItem "$root\stage\prod\dry\*.json" -ErrorAction SilentlyContinue).Count
$fin = @(Get-ChildItem "$root\stage\prod\final\*.ogg" -ErrorAction SilentlyContinue).Count
$vo = @(Get-ChildItem "Y:\game\assets\audio\vo\*.ogg" -ErrorAction SilentlyContinue).Count
$run = if (Test-Path "$PSScriptRoot\RUNNING.txt") { 'RUNNING' } else { 'not running' }
"{0}  {1} | anchors {2}/23 | dry lines {3}/188 | processed {4} | in game folder {5}" -f (Get-Date -Format HH:mm), $run, $anch, $dry, $fin, $vo
Get-Content $log -Tail 400 -Encoding UTF8 | Where-Object { $_ -match 'WARNING|FAILED|Traceback|Error|drift|too alike|report:|deliver:|speech generated' } | Select-Object -Last 4
Get-Content $log -Tail 3 -Encoding UTF8 | ForEach-Object { if ($_.Length -gt 170) { $_.Substring(0, 170) } else { $_ } }
