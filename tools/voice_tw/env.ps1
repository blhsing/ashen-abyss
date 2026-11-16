# Dot-source this file: shared environment for every voice_tw script.
# Heavy things (Python, venvs, weights, caches, staging) live under a SHORT path, never under Y:.
$global:VT_ROOT = (Join-Path $env:LOCALAPPDATA 'AshenAbyssTools\voice_tw')
$env:VT_ROOT = $VT_ROOT
$env:UV_CACHE_DIR = "$VT_ROOT\cache\uv"
$env:UV_PYTHON_INSTALL_DIR = "$VT_ROOT\python"
$env:UV_LINK_MODE = 'hardlink'
$env:PIP_CACHE_DIR = "$VT_ROOT\cache\pip"
$env:HF_HOME = "$VT_ROOT\cache\hf"
$env:MODELSCOPE_CACHE = "$VT_ROOT\cache\modelscope"
$env:TORCH_HOME = "$VT_ROOT\cache\torch"
$env:XDG_CACHE_HOME = "$VT_ROOT\cache\xdg"
$env:NLTK_DATA = "$VT_ROOT\cache\nltk"
$env:NUMBA_CACHE_DIR = "$VT_ROOT\cache\numba"
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
# CPU budget: other heavy jobs run on this machine, never use more than 8 threads
$env:OMP_NUM_THREADS = '8'
$env:MKL_NUM_THREADS = '8'
$env:OPENBLAS_NUM_THREADS = '8'
$env:NUMEXPR_NUM_THREADS = '8'
$env:TOKENIZERS_PARALLELISM = 'false'
