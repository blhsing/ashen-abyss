# Builds the web installer  Y:\release_out\installer\AshenAbyss-<ver>-Setup.exe  (and, unless -NoManifest, the release
# manifest from the local parts via make_manifest.ps1). Pure ASCII.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\installer\build_installer.ps1
#       [-Version 0.1.0] [-OutDir Y:\release_out\installer] [-PartsDir Y:\release_out\new] [-NoManifest] [-Art]
#
# -Art          : regenerate the wizard images first (make_art.py, needs uv + Pillow).
# Needs Inno Setup 6.7+ (winget install JRSoftware.InnoSetup --scope user); ISCC.exe is looked up in the usual places.
param(
    [string]$Version = '0.1.0',
    [string]$OutDir = 'Y:\release_out\installer',
    [string]$PartsDir = 'Y:\release_out\new',
    [switch]$NoManifest,
    [switch]$Art
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$iscc = $null
foreach ($c in @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
    if ($c -and (Test-Path -LiteralPath $c)) { $iscc = $c; break }
}
if (-not $iscc) { throw 'ISCC.exe not found - install Inno Setup 6: winget install JRSoftware.InnoSetup --scope user' }

if ($Art) {
    & uv run --no-project --with pillow python -I (Join-Path $here 'make_art.py')
    if ($LASTEXITCODE -ne 0) { throw "make_art.py failed ($LASTEXITCODE)" }
}
foreach ($f in 'art\wizard_164x314.bmp', 'art\small_55.bmp', 'lang\ChineseTraditional.isl', 'licenses\7-Zip-License.txt') {
    if (-not (Test-Path -LiteralPath (Join-Path $here $f))) { throw "missing $f (run with -Art for the images)" }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$t0 = Get-Date
& $iscc /Q "/DAppVer=$Version" "/DOutDir=$OutDir" (Join-Path $here 'AshenAbyss.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
$exe = Join-Path $OutDir "AshenAbyss-$Version-Setup.exe"
Write-Output ("built {0}  {1:N0} bytes  ({2:N0} s)" -f $exe, (Get-Item -LiteralPath $exe).Length, ((Get-Date) - $t0).TotalSeconds)
Write-Output ("sha256 {0}" -f (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant())

if (-not $NoManifest) {
    & (Join-Path $here 'make_manifest.ps1') -PartsDir $PartsDir -Version $Version -OutDir $OutDir
}
