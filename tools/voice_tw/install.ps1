# Creates the Python environments under %LOCALAPPDATA%\AshenAbyssTools\voice_tw\venvs (re-runnable).
#   install.ps1 [common] [breezy] [cosy] [voxcpm] [qwen]      (no argument = common breezy cosy voxcpm)
#
# How: uv creates the venv (uv-managed CPython). pip only RESOLVES (dry run -> report.json); the files
# are fetched by pget.py (several at once, IPv4, range requests) into a local wheelhouse, and pip then
# installs offline from that wheelhouse. Reason: on this network the IPv6 route to PyPI's CDN crawls
# at ~30 kB/s and pip downloads one file at a time; this way a 600 MB environment takes minutes, not hours.
# Nothing system-wide is changed (IPv4 preference lives in the venv: _ipv4only.py + ipv4only.pth).
. "$PSScriptRoot\env.ps1"
$want = if ($args.Count) { $args } else { 'common', 'breezy', 'cosy', 'voxcpm' }
$py = @{ common = '3.12'; breezy = '3.10'; cosy = '3.10'; voxcpm = '3.12'; qwen = '3.12' }
# CPU builds of PyTorch from the official index for the environments that pin an old version
$torch = @{
    breezy = @('https://download.pytorch.org/whl/cpu/torch-2.3.1%2Bcpu-cp310-cp310-win_amd64.whl', 'https://download.pytorch.org/whl/cpu/torchaudio-2.3.1%2Bcpu-cp310-cp310-win_amd64.whl')
    cosy   = @('https://download.pytorch.org/whl/cpu/torch-2.3.1%2Bcpu-cp310-cp310-win_amd64.whl', 'https://download.pytorch.org/whl/cpu/torchaudio-2.3.1%2Bcpu-cp310-cp310-win_amd64.whl')
}
$env:PIP_CONSTRAINT = "$PSScriptRoot\req\build_constraints.txt"     # setuptools<81 for old sdists (openai-whisper)
$env:PIP_DISABLE_PIP_VERSION_CHECK = '1'
$env:PIP_NO_INPUT = '1'
$getter = "$VT_ROOT\venvs\common\Scripts\python.exe"                 # any Python can run pget.py
$quiet = '^\s*(Collecting|Downloading|Using cached|Requirement already|Preparing|Getting|Installing build|Attempting|Found existing|Uninstalling|Successfully uninstalled|Looking in|Obtaining|Created wheel|Stored in|Building wheel|Processing|Would install)'
foreach ($name in $want) {
    $venv = "$VT_ROOT\venvs\$name"
    $vpy = "$venv\Scripts\python.exe"
    $wh = "$VT_ROOT\wheels\py$($py[$name].Replace('.', ''))"
    $rep = "$VT_ROOT\wheels\report_$name.json"
    New-Item -ItemType Directory -Force $wh | Out-Null
    Write-Output "=== $name (Python $($py[$name])) $(Get-Date -Format HH:mm:ss)"
    if (!(Test-Path $vpy)) { uv venv --python $py[$name] $venv 2>&1 | ForEach-Object { "$_" } }
    $sp = "$venv\Lib\site-packages"
    Copy-Item "$PSScriptRoot\_ipv4only.py" "$sp\_ipv4only.py" -Force
    [IO.File]::WriteAllText("$sp\ipv4only.pth", "import _ipv4only`n")
    & $vpy -m ensurepip 2>&1 | Select-Object -Last 1
    & $vpy -m pip install --progress-bar off --upgrade pip 'setuptools<81' wheel 2>&1 | Select-Object -Last 1
    if ($name -eq 'common' -or !(Test-Path $getter)) {
        # bootstrap: the small common environment is installed directly
        & $vpy -m pip install --progress-bar off -r "$PSScriptRoot\req\$name.txt" 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -notmatch $quiet }
    } else {
        if ($torch.ContainsKey($name)) { & $getter "$PSScriptRoot\pget.py" $wh @($torch[$name]) 2>&1 | ForEach-Object { "$_" } }
        Write-Output "--- resolve $(Get-Date -Format HH:mm:ss)"
        & $vpy -m pip install --progress-bar off --dry-run --ignore-installed --no-build-isolation --report $rep --find-links $wh -r "$PSScriptRoot\req\$name.txt" 2>&1 |
            ForEach-Object { "$_" } | Where-Object { $_ -notmatch $quiet }
        Write-Output "--- fetch $(Get-Date -Format HH:mm:ss)"
        & $getter "$PSScriptRoot\pget.py" $wh --report $rep 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -notmatch '^(ok |\s+\d)' }
        Write-Output "--- install $(Get-Date -Format HH:mm:ss)"
        & $vpy -m pip install --progress-bar off --no-index --find-links $wh --no-build-isolation -r "$PSScriptRoot\req\$name.txt" 2>&1 |
            ForEach-Object { "$_" } | Where-Object { $_ -notmatch $quiet }
    }
    if ($name -eq 'voxcpm') {
        # the package itself, without its declared dependencies (gradio, funasr, datasets, modelscope, torchcodec ...)
        & $getter "$PSScriptRoot\pget.py" $wh 'https://files.pythonhosted.org/packages/f5/50/76e912427684f7e71d443d9542802ad33df8764ef3bba954b96feeab41ba/voxcpm-2.0.3-py3-none-any.whl' 2>&1 | Select-Object -Last 1
        & $vpy -m pip install --progress-bar off --no-index --find-links $wh --no-deps 'voxcpm==2.0.3' 2>&1 | Select-Object -Last 1
    }
    Write-Output "=== $name exit $LASTEXITCODE $(Get-Date -Format HH:mm:ss)"
}
