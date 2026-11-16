# Extra test environments (not needed for the GPU production path):
#   -Venv cpu   plain PyPI torch 2.9.1 (CPU build with a real BLAS). Needed because AMD's ROCm wheel is built with
#               USE_EIGEN_FOR_BLAS=ON / USE_MKL=OFF: its CPU matmul is about 100x slower than a normal build, so the CPU
#               fallback and the CPU reference for the GPU sanity check must not use the ROCm environment.
#   -Venv dml   torch-directml 0.2.5.dev240914 (last release, September 2024), which pins torch 2.4.1 - only to
#               test acceleration path (b), DirectML.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\install_extra.ps1 -Venv cpu|dml
param([Parameter(Mandatory)][ValidateSet('cpu', 'dml')][string]$Venv)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\env.ps1"
$uv = (Get-Command uv).Source
$venvDir = "$MA_ROOT\venvs\$Venv"
$py = "$venvDir\Scripts\python.exe"
$basePy = "$MA_ROOT\python\cpython-3.12.16-windows-x86_64-none\python.exe"
if (-not (Test-Path $py)) { & $uv venv --python $basePy $venvDir; if ($LASTEXITCODE) { throw 'venv failed' } }
if (-not (Test-Path "$venvDir\Lib\site-packages\pip")) { & $py -m ensurepip | Out-Null }   # bundled pip, works offline
$pth = "$venvDir\Lib\site-packages\ipv4_only.pth"
if (-not (Test-Path $pth)) {
    Set-Content -Path $pth -Encoding ascii -Value 'import socket; socket.getaddrinfo = (lambda _g, _i: (lambda h, p, f=0, *a, **k: _g(h, p, _i, *a, **k)))(socket.getaddrinfo, socket.AF_INET)'
}
& $py -m pip install --no-input --disable-pip-version-check -r "$PSScriptRoot\req\$Venv.txt"; if ($LASTEXITCODE) { throw 'install failed' }
if ($Venv -eq 'dml') {
    & $py -c "import torch, torch_directml as d; print('torch', torch.__version__, '| DirectML devices:', [d.device_name(i) for i in range(d.device_count())])"
} else {
    & $py -c "import torch, time; a = torch.randn(2048, 2048); t = time.time(); a @ a; print('torch', torch.__version__, '| 2048x2048 matmul %.3f s' % (time.time() - t))"
}
