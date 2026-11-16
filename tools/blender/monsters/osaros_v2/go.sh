#!/bin/bash
# build (dev, into osaros_v2/out) and render:  ./go.sh "<build args>" "<render args>"
cd /y/tools/blender/monsters/osaros_v2
powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\blender\monsters\osaros_v2\bl.ps1' dev_build.py "x $1" 2>&1 | tail -${3:-24}
if [ -n "$2" ]; then
powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\blender\monsters\osaros_v2\bl.ps1' ov2_render.py "x $2" 2>&1 | grep -v Deprecation | tail -8
fi
