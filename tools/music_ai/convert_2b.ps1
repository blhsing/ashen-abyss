# Optional second candidate: ACE-Step 1.5 turbo (2B DiT).  Converts the original checkpoint
# (models\acestep_orig\acestep-v15-turbo, fetched with jobs_ace_2b.txt) to the Diffusers layout with the official
# conversion script from huggingface/diffusers v0.39.0 (scripts/convert_ace_step_to_diffusers.py, kept in
# $MA_ROOT\src\diffusers_scripts).  The VAE and the text encoder are the same files (same SHA-256) as in the XL
# repository, so they are copied locally instead of being downloaded again.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\convert_2b.ps1
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\env.ps1"
$py = "$MA_ROOT\venvs\ace\Scripts\python.exe"
$xl = "$MA_ROOT\models\acestep-v15-xl-turbo-diffusers"
$orig = "$MA_ROOT\models\acestep_orig"
$out = "$MA_ROOT\models\acestep-v15-turbo-diffusers"
if (-not (Test-Path "$orig\acestep-v15-turbo\model.safetensors")) { throw "missing $orig\acestep-v15-turbo\model.safetensors (run fetch.ps1 -Jobs jobs_ace_2b.txt first)" }
New-Item -ItemType Directory -Force -Path "$orig\vae", "$orig\Qwen3-Embedding-0.6B" | Out-Null
Copy-Item "$xl\vae\*" "$orig\vae\" -Force
Copy-Item "$xl\text_encoder\*" "$orig\Qwen3-Embedding-0.6B\" -Force
Copy-Item "$xl\tokenizer\*" "$orig\Qwen3-Embedding-0.6B\" -Force
& $py "$MA_ROOT\src\diffusers_scripts\convert_ace_step_to_diffusers.py" --checkpoint_dir $orig --dit_config acestep-v15-turbo --output_dir $out --dtype bf16
if ($LASTEXITCODE) { throw 'conversion failed' }
Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum | ForEach-Object { "converted: {0:N1} GB in $out" -f ($_.Sum / 1GB) }
