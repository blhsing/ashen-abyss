#!/bin/bash
# re-upload the 0.4.1 parts that failed (network drop), then verify all assets and clean up like release_041.sh
set -u
V=0.4.1; R=/y/release_out/v041; log=$R/release.log
say() { echo "$(date +%H:%M:%S) $*" >> $log; }
say "reupload start"
for f in $R/AshenAbyss-$V-win64.7z.* $R/AshenAbyss-$V-assets.7z.*; do
  n=$(basename $f); s=$(stat -c %s $f)
  gh release view v$V --repo blhsing/ashen-abyss --json assets --jq '.assets[] | "\(.name) \(.size)"' 2>/dev/null | grep -q "^$n $s$" && { say "already up $n"; continue; }
  for try in 1 2 3 4 5 6; do
    say "reupload $n try $try"
    gh release upload v$V --repo blhsing/ashen-abyss "$f" --clobber >> $log 2>&1 && break
    sleep $((try * 60))
  done
done
gh release view v$V --repo blhsing/ashen-abyss --json assets --jq '.assets[] | "\(.name) \(.size)"' >> $log 2>&1
ok=1; for f in $R/AshenAbyss-$V-win64.7z.* $R/AshenAbyss-$V-assets.7z.* $R/installer/AshenAbyss-$V-Setup.exe $R/installer/AshenAbyss-$V-manifest.json; do
  n=$(basename $f); s=$(stat -c %s $f); grep -q "^$n $s$" <(gh release view v$V --repo blhsing/ashen-abyss --json assets --jq '.assets[] | "\(.name) \(.size)"') || { ok=0; say "verify FAIL $n"; }
done
if [ $ok = 1 ]; then rm -rf $R/*.7z.* $R/installer "$LOCALAPPDATA/Temp/AshenAbyss_export"; say "cleanup done"; else say "cleanup skipped (verify failed)"; fi
say "reupload finished"
