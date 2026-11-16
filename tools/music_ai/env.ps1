# Dot-source this file: shared environment for every music_ai script.
# Heavy things (Python, venvs, weights, caches, staging) live under a SHORT path, never under Y:.
$global:MA_ROOT = (Join-Path $env:LOCALAPPDATA 'AshenAbyssTools\music_ai')
$env:MA_ROOT = $MA_ROOT
$env:UV_CACHE_DIR = "$MA_ROOT\cache\uv"
$env:UV_PYTHON_INSTALL_DIR = "$MA_ROOT\python"
$env:UV_LINK_MODE = 'hardlink'
$env:PIP_CACHE_DIR = "$MA_ROOT\cache\pip"
$env:HF_HOME = "$MA_ROOT\cache\hf"
$env:MODELSCOPE_CACHE = "$MA_ROOT\cache\modelscope"
$env:TORCH_HOME = "$MA_ROOT\cache\torch"
$env:XDG_CACHE_HOME = "$MA_ROOT\cache\xdg"
$env:NUMBA_CACHE_DIR = "$MA_ROOT\cache\numba"
$env:MIOPEN_USER_DB_PATH = "$MA_ROOT\cache\miopen"
$env:MIOPEN_CUSTOM_CACHE_DIR = "$MA_ROOT\cache\miopen"
$env:ACESTEP_CHECKPOINTS_DIR = "$MA_ROOT\models\acestep"
# no telemetry, no accounts, no tokens
$env:HF_HUB_DISABLE_TELEMETRY = '1'
$env:HF_HUB_DISABLE_IMPLICIT_TOKEN = '1'
$env:HF_HUB_DISABLE_SYMLINKS_WARNING = '1'
$env:DO_NOT_TRACK = '1'
$env:GRADIO_ANALYTICS_ENABLED = 'False'
Remove-Item Env:HF_TOKEN -ErrorAction SilentlyContinue
Remove-Item Env:HUGGING_FACE_HUB_TOKEN -ErrorAction SilentlyContinue
# text
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
# CPU budget: other heavy jobs (Blender renders, speech) run on this machine - never more than 10 threads
$env:OMP_NUM_THREADS = '10'
$env:MKL_NUM_THREADS = '10'
$env:OPENBLAS_NUM_THREADS = '10'
$env:NUMEXPR_NUM_THREADS = '10'
$env:TOKENIZERS_PARALLELISM = 'false'
# ROCm on Windows: let MIOpen pick convolution kernels heuristically instead of benchmarking each layer on first
# use (otherwise the first VAE decode stalls for minutes); make the SDK's helper binaries findable.
$env:MIOPEN_FIND_MODE = 'FAST'
$rocmLlvm = "$MA_ROOT\venvs\ace\Lib\site-packages\_rocm_sdk_core\lib\llvm\bin"
if ((Test-Path $rocmLlvm) -and ($env:PATH -notlike "*$rocmLlvm*")) { $env:PATH = "$rocmLlvm;$env:PATH" }
