#!/bin/sh
# rb.sh <script.py> [args...]  -> runs a script of this folder in background Blender through the throttled launcher
# (output filtered to the [hero] log lines and errors; full output in _work/last_<script>.txt)
s="$1"; shift
a=""
tag="$s"
for x in "$@"; do a="$a,'$x'"; tag="${tag}_$x"; done
tag=$(echo "$tag" | tr -c 'A-Za-z0-9_.\n' '_')
out="Y:/tools/blender/gear/armor/_work/last_$tag.txt"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'Y:/tools/blender/run_blender.ps1' -A @('-b','--factory-startup','--python','Y:/tools/blender/gear/armor/$s','--'$a) 2>&1 | Out-File -Encoding utf8 '$out'; exit \$LASTEXITCODE"
code=$?
grep -a -E "^\[hero\]|Error|Traceback|File \"|assert|Exception|WARNING: tri" "$out" | grep -a -v "Missing image file" | tail -${RB_TAIL:-70}
echo "rb exit=$code"
