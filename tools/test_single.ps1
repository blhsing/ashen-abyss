# Starts copy A (windowed bot run), then copy B while A runs; B must exit at once and leave A alone. Pure ASCII.
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$logDir = Join-Path $env:APPDATA 'AshenAbyss\logs'
Push-Location Y:\game; dotnet build AshenAbyss.csproj -c Debug -nologo -v q 2>&1 | Select-String ': error ' | Select-Object -First 5; Pop-Location
$t0 = Get-Date
$a = Start-Process $G -ArgumentList '--path','Y:\game','--windowed','--resolution','1600x900','--position','160,90','--','--win','--mute','--new=emberblade','--slot=95','--area=wastes','--level=5','--gear','--bot','--quit-at=30' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 20
$t1 = Get-Date
$b = Start-Process $G -ArgumentList '--path','Y:\game','--windowed','--resolution','800x450','--','--win','--mute','--new=hunter','--slot=96' -PassThru -WindowStyle Hidden
$bOk = $b.WaitForExit(60000)
"copy B exited by itself: $bOk after $([int]((Get-Date) - $t1).TotalSeconds) s; copy A still running then: $(-not $a.HasExited)"
if (-not $bOk) { try { $b.Kill() } catch { } }
[void]$a.WaitForExit(120000)
"copy A exit after $([int]((Get-Date) - $t0).TotalSeconds) s"
Get-ChildItem $logDir -Filter 'game_*.log' | Where-Object { $_.LastWriteTime -ge $t0 } | Sort-Object CreationTime | ForEach-Object {
    $l = Get-Content $_.FullName -Encoding UTF8
    "== $($_.Name) lines=$($l.Count) ERR=$(@($l | Where-Object { $_ -match ' ERR ' }).Count)"
    $l | Where-Object { $_ -match 'another instance|single-instance|focus|pause menu|final summary|shutdown' } | ForEach-Object { if ($_.Length -gt 200) { $_.Substring(0, 200) } else { $_ } }
}
$s = Join-Path $env:APPDATA 'AshenAbyss\saves'
"slot 96 save written by copy B: $(Test-Path (Join-Path $s 'slot96.json'))"
foreach ($f in 'slot95.json', 'slot95.bak') { $p = Join-Path $s $f; if (Test-Path $p) { Move-Item $p (Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup') -Force } }
