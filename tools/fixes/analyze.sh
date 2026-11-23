#!/bin/bash
# analyze.sh file.txt : mean single-target dps per class/level/loadout over samples, ratio to best and weakest 2H
grep -a ": aps" "$1" | awk '{
  cls=$2; lv=$3; sub(/^lv/,"",lv);
  set=$0; sub(/^dps-bases [a-z]+ lv[0-9]+ /,"",set); sub(/: aps.*/,"",set);
  match($0,/single ([0-9]+)\/s/,m); k=cls"|"lv"|"set; s[k]+=m[1]; n[k]++; c2=cls"|"lv; seen[c2]=1; sets[k]=1; ss[k]+=m[1]*m[1]
} END {
  for (k in s) { split(k,a,"|"); mean=s[k]/n[k]; mu[k]=mean; sd[k]=sqrt(ss[k]/n[k]-mean*mean); if (a[3] ~ /^2h /) { c2=a[1]"|"a[2]; if (mean>b2[c2]) b2[c2]=mean; if (!(c2 in w2) || mean<w2[c2]) w2[c2]=mean } }
  for (k in s) { split(k,a,"|"); c2=a[1]"|"a[2]; printf "%-11s lv%-3s %-24s n=%d mean %6.0f (sd %4.0f%%)  %.2f of best 2H  %.2f of weakest 2H\n", a[1], a[2], a[3], n[k], mu[k], 100*sd[k]/mu[k], mu[k]/b2[c2], mu[k]/w2[c2] }
}' | sort -k1,1 -k2,2V -k3,3
