#!/bin/bash
# 0.4.0: export + install, pack win64 + assets (0.2.0 names), test, manifest + installer, create release, upload.
set -u
V=0.4.0; R=/y/release_out/v040; log=$R/release.log; mkdir -p $R/installer; : > $log
say() { echo "$(date +%H:%M:%S) $*" >> $log; }
while tasklist 2>/dev/null | grep -qi ashenabyss; do sleep 15; done
say "export + install"
powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\install.ps1' 2>&1 | grep -E 'exported in|icon set|installed  |ERROR|done in' >> $log
S="$LOCALAPPDATA/Temp/AshenAbyss_export"
[ -f "$S/AshenAbyss.ico" ] && [ -d "$S/data_AshenAbyss_windows_x86_64" ] || { say "STOP: export incomplete"; exit 1; }
cd /y && git ls-files --others --ignored --exclude-standard game/assets > $R/assets_list.txt
rm -f $R/AshenAbyss-$V-*.7z.*
say "pack build"
( cd "$S" && "/c/Program Files/7-Zip/7z.exe" a -t7z -mx=3 -mmt=8 -v1900m $(cygpath -w $R)/AshenAbyss-$V-win64.7z AshenAbyss.exe AshenAbyss.ico AshenAbyss.pck data_AshenAbyss_windows_x86_64 assets -bso0 -bsp0 ) >> $log 2>&1
say "pack assets"
"/c/Program Files/7-Zip/7z.exe" a -t7z -mx=3 -mmt=8 -v1900m -spf2 $(cygpath -w $R)/AshenAbyss-$V-assets.7z @$(cygpath -w $R)/assets_list.txt -bso0 -bsp0 >> $log 2>&1
for a in win64 assets; do "/c/Program Files/7-Zip/7z.exe" t $R/AshenAbyss-$V-$a.7z.001 -bso0 -bsp0 >> $log 2>&1 && say "archive $a OK" || { say "STOP: archive $a failed test"; exit 1; }; done
ls -la $R/*.7z.* | awk '{print $5, $9}' >> $log
say "manifest + installer"
powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\installer\make_manifest.ps1' -Version $V -PartsDir "$(cygpath -w $R)" -OutDir "$(cygpath -w $R/installer)" >> $log 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\installer\build_installer.ps1' -Version $V -PartsDir "$(cygpath -w $R)" -OutDir "$(cygpath -w $R/installer)" -NoManifest >> $log 2>&1
[ -f $R/installer/AshenAbyss-$V-Setup.exe ] && [ -f $R/installer/AshenAbyss-$V-manifest.json ] || { say "STOP: installer or manifest missing"; exit 1; }
ls -la $R/installer | awk 'NF>5{print $5, $9}' >> $log
say "create release"
gh release view v$V --repo blhsing/ashen-abyss >/dev/null 2>&1 && { say "replacing existing v$V (user: re-release as 0.4.0)"; gh release delete v$V --repo blhsing/ashen-abyss --yes --cleanup-tag >> $log 2>&1; }
gh release create v$V --repo blhsing/ashen-abyss --title "燼淵 Ashen Abyss $V" --notes-file /y/release_out/notes_040.md --target main --latest $R/installer/AshenAbyss-$V-Setup.exe $R/installer/AshenAbyss-$V-manifest.json >> $log 2>&1 || { say "STOP: release create failed"; exit 1; }
for f in $R/AshenAbyss-$V-win64.7z.* $R/AshenAbyss-$V-assets.7z.*; do
  for try in 1 2 3; do say "upload $(basename $f) try $try"; gh release upload v$V --repo blhsing/ashen-abyss "$f" --clobber >> $log 2>&1 && break; sleep 20; done
done
gh release view v$V --repo blhsing/ashen-abyss --json assets --jq '.assets[] | "\(.name) \(.size)"' >> $log 2>&1
say "finished"
# cleanup (user: don't keep old builds): only after every asset is verified on GitHub
ok=1; for f in $R/AshenAbyss-$V-win64.7z.* $R/AshenAbyss-$V-assets.7z.* $R/installer/AshenAbyss-$V-Setup.exe $R/installer/AshenAbyss-$V-manifest.json; do
  n=$(basename $f); s=$(stat -c %s $f); grep -q "^$n $s$" <(gh release view v$V --repo blhsing/ashen-abyss --json assets --jq '.assets[] | "\(.name) \(.size)"') || { ok=0; say "verify FAIL $n"; }
done
if [ $ok = 1 ]; then rm -rf $R/*.7z.* $R/installer "$LOCALAPPDATA/Temp/AshenAbyss_export"; say "cleanup done"; else say "cleanup skipped (verify failed)"; fi
