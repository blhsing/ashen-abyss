#!/bin/bash
# item 8: clipboard / file / both; reads the clipboard back after each run. Touches the clipboard only while the user's game is closed.
wait_free() { while tasklist | grep -qi AshenAbyss.exe; do sleep 30; done; }
readclip() { powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; \$i=[System.Windows.Forms.Clipboard]::GetImage(); if (\$i) { 'clipboard image ' + \$i.Width + 'x' + \$i.Height } else { 'clipboard: no image (' + [System.Windows.Forms.Clipboard]::GetText() + ')' }"; }
clearclip() { powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetText('fixes test')"; }
one() { # tag build-flag args
  wait_free; clearclip
  out=$(bash Y:/tools/fixes/runq.sh $1 $2 -GameArgs "$3" -Timeout 100)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "== $1: $(echo "$out" | grep -E '^log:' | sed 's/.*lines=/lines=/')"
  [ -n "$L" ] && grep -a -E "\[shot\]|prtsc|f12|PrtSc|ERR|exits without" "$L" | cut -c1-200
  readclip
}
one fixes_clip0 "" "--win --mute --new=hunter --slot=94 --area=town --level=5 --prtsc-at=6 --f12-at=9 --quit-at=12"
one fixes_clip1 -NoBuild "--win --mute --new=hunter --slot=94 --area=town --level=5 --shotmode=1 --prtsc-at=6 --quit-at=10"
one fixes_clip2 -NoBuild "--win --mute --new=hunter --slot=94 --area=town --level=5 --shotmode=2 --f12-at=6 --quit-at=10"
bash Y:/tools/fixes/runq.sh fixes_set -NoBuild -GameArgs "--win --mute --menu=settings --uidir=Y:\shots\fixes\settings --uido=@Y:\tools\fixes\scripts\settings.txt --quit-at=40" -Timeout 90 | grep "^log:"
echo CLIP DONE
