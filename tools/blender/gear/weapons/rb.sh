#!/bin/sh
# rb.sh <script.py> [args...]  -> runs the script in background Blender through the throttled launcher
s="$1"; shift
a=""
for x in "$@"; do a="$a,'$x'"; done
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'Y:/tools/blender/run_blender.ps1' -A @('-b','--factory-startup','--python','Y:/tools/blender/gear/weapons/$s','--'$a); exit \$LASTEXITCODE"
