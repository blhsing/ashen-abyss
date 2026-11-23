# AUDIO agent 2026-11-22: follow-up redesigns, one voice after the other (gen.py all --voice <v>).
$env:VT_THREADS = '8'
$py = "$env:LOCALAPPDATA\AshenAbyssTools\voice_tw\venvs\voxcpm\Scripts\python.exe"
foreach ($v in $args) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\voice_tw\x.ps1 $py Y:\tools\voice_tw\gen.py all --voice $v
}
