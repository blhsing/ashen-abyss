# Compile-check the game without touching the shared build folders, so several people can check at the same time.
# Each call compiles into its own temp folder and prints a compact error summary.
#   powershell -NoProfile -File Y:\tools\check.ps1                 # totals per file + first errors
#   powershell -NoProfile -File Y:\tools\check.ps1 -Files Game.cs,Hud.cs   # only errors located in these files (all of them)
#   powershell -NoProfile -File Y:\tools\check.ps1 -Max 80         # show up to 80 error lines
param([string[]]$Files = @(), [int]$Max = 40, [switch]$Warnings)
if (-not (Test-Path Y:\)) { cmd /c subst Y: "$(Split-Path $PSScriptRoot -Parent)" | Out-Null }
if ($Files.Count -eq 1 -and $Files[0] -match ',') { $Files = $Files[0].Split(',') }
$tmp = Join-Path $env:TEMP ("aachk_" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $tmp | Out-Null
try {
    Push-Location Y:\game
    if (-not (Test-Path 'Y:\game\.godot\mono\temp\obj\project.assets.json')) { dotnet restore AshenAbyss.csproj -nologo -v q | Out-Null }
    $out = dotnet build AshenAbyss.csproj -c Debug --no-restore -nologo -v q -nodeReuse:false "-p:IntermediateOutputPath=$tmp\obj\" "-p:OutputPath=$tmp\bin\" "-p:GenerateFullPaths=false" 2>&1 | ForEach-Object { "$_" }
    Pop-Location
    $pat = if ($Warnings) { ': (error|warning) ' } else { ': error ' }
    $errs = $out | Where-Object { $_ -match $pat } | ForEach-Object { ($_ -replace '\s*\[Y:\\game\\AshenAbyss\.csproj\]\s*$', '') -replace '^(.*\\)?src\\', '' -replace '^Y:\\game\\', '' } | Sort-Object -Unique
    if ($Files.Count -gt 0) {
        $mine = $errs | Where-Object { $l = $_; ($Files | Where-Object { $l.StartsWith($_ + '(') }).Count -gt 0 }
        "TOTAL errors in project: $($errs.Count); in [$($Files -join ', ')]: $($mine.Count)"
        $mine | Select-Object -First $Max
        if ($mine.Count -gt $Max) { "... $($mine.Count - $Max) more" }
    }
    else {
        "TOTAL errors: $($errs.Count)"
        $errs | ForEach-Object { ($_ -split '\(')[0] } | Group-Object | Sort-Object Count -Descending | ForEach-Object { "{0,5}  {1}" -f $_.Count, $_.Name }
        "--- by code ---"
        $errs | ForEach-Object { if ($_ -match 'error (\w+):') { $Matches[1] } } | Group-Object | Sort-Object Count -Descending | Select-Object -First 12 | ForEach-Object { "{0,5}  {1}" -f $_.Count, $_.Name }
        "--- first $Max ---"
        $errs | Select-Object -First $Max
    }
    if ($errs.Count -eq 0) { if ($out | Where-Object { $_ -match 'Build succeeded' }) { "BUILD OK" } else { "NO ERRORS PARSED - raw tail:"; $out | Select-Object -Last 15 } }
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
