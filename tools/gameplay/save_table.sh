#!/bin/bash
# Per-case PASS/FAIL table from a verify (or save) run output: save_table.sh [test_out/verify.txt]
f=${1:-Y:/tools/gameplay/test_out/verify.txt}
grep -a "gptest\] \(=== \|PASS\|FAIL\)" "$f" | awk '
  /=== (verify|save) case/ { c=$0; sub(/.*=== (verify|save) case /,"",c); sub(/:.*/,"",c); if (!(c in seen)) { order[++n]=c; seen[c]=1 } ; cur=c; next }
  /PASS/ { p[cur]++ } /FAIL/ { q[cur]++; fl[cur]=fl[cur] "\n      " substr($0, index($0,"FAIL")) }
  END { printf "| case | pass | fail |\n|---|---|---|\n"; for (i=1;i<=n;i++) { c=order[i]; printf "| %s | %d | %d |\n", c, p[c], q[c] } ;
        for (i=1;i<=n;i++) { c=order[i]; if (q[c]) printf "%s:%s\n", c, fl[c] } }'
