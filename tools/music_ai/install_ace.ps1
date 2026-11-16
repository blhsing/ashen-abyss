# Builds the music_ai Python environment: self-contained CPython 3.12 + AMD's PyTorch-for-ROCm wheels (Windows)
# + Diffusers (ACE-Step pipeline) + the analysis packages.  Re-runnable; nothing outside $MA_ROOT is touched.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\install_ace.ps1 [-Venv ace] [-Rocm 7.1.1]
#
# Order matters:
#   1. packages that do not depend on torch (req\base.txt)
#   2. AMD's wheels from the local folder dl\rocm<ver> (downloaded with fetch.py, jobs_rocm_*.txt)
#   3. packages that depend on torch (req\torchdeps.txt) - pip then keeps AMD's torch instead of pulling PyPI's
# Notes:
#   * uv is only used to fetch the interpreter and create the venv; packages are installed with pip, because
#     downloads must go over IPv4 on this network (venv\Lib\site-packages\ipv4_only.pth) and uv cannot do that.
#   * rocm_sdk_devel (2.5 GB) is an optional extra of the 'rocm' meta package and is not installed.
param([string]$Venv = 'ace', [string]$Rocm = '7.1.1')
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\env.ps1"
$uv = (Get-Command uv).Source
$venvDir = "$MA_ROOT\venvs\$Venv"
$py = "$venvDir\Scripts\python.exe"
$basePy = "$MA_ROOT\python\cpython-3.12.16-windows-x86_64-none\python.exe"
if (-not (Test-Path $basePy)) { & $uv python install 3.12.16 --no-registry --no-bin }
if (-not (Test-Path $py)) { & $uv venv --python $basePy $venvDir; if ($LASTEXITCODE) { throw 'venv failed' } }
if (-not (Test-Path "$venvDir\Lib\site-packages\pip")) { & $py -m ensurepip | Out-Null }   # bundled pip, works offline
$pth = "$venvDir\Lib\site-packages\ipv4_only.pth"
if (-not (Test-Path $pth)) {
    Set-Content -Path $pth -Encoding ascii -Value 'import socket; socket.getaddrinfo = (lambda _g, _i: (lambda h, p, f=0, *a, **k: _g(h, p, _i, *a, **k)))(socket.getaddrinfo, socket.AF_INET)'
}
$pip = @('-m', 'pip', 'install', '--no-input', '--disable-pip-version-check')
Write-Host '== 1/3 torch-free packages'
& $py @pip -r "$PSScriptRoot\req\base.txt"; if ($LASTEXITCODE) { throw 'base install failed' }
Write-Host "== 2/3 AMD ROCm $Rocm wheels (local files)"
$dl = "$MA_ROOT\dl\rocm$($Rocm -replace '\.', '')"
$wheels = @(Get-ChildItem $dl -Filter 'rocm_sdk_core-*.whl') + @(Get-ChildItem $dl -Filter 'rocm_sdk_libraries_custom-*.whl') + @(Get-ChildItem $dl -Filter 'rocm-*.tar.gz')
& $py @pip --find-links $dl @($wheels | ForEach-Object FullName); if ($LASTEXITCODE) { throw 'rocm sdk install failed' }
$tw = @(Get-ChildItem $dl -Filter 'torch-*.whl') + @(Get-ChildItem $dl -Filter 'torchaudio-*.whl') + @(Get-ChildItem $dl -Filter 'torchvision-*.whl')
& $py @pip --find-links $dl @($tw | ForEach-Object FullName); if ($LASTEXITCODE) { throw 'torch install failed' }
Write-Host '== 3/3 packages that depend on torch'
& $py @pip -r "$PSScriptRoot\req\torchdeps.txt"; if ($LASTEXITCODE) { throw 'torch-dependent install failed' }
& $py -c "import torch; print('torch', torch.__version__, '| GPU available:', torch.cuda.is_available(), '|', torch.cuda.get_device_name(0) if torch.cuda.is_available() else '-')"
