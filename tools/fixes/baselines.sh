#!/bin/bash
# builds Y:/tools/fixes/balance_baselines.txt: share of the class's best two-hand setup (bonuses on), levels 30 and 50,
# from the final_* runs (Lampknight from final2_* with 燈誓 off). Format: class<TAB>setup<TAB>lv30<TAB>lv50
cd Y:/tools/fixes
out=balance_baselines.txt
echo -e "# class\tsetup\tlv30\tlv50   (share of the best 2H, fixed gear, 2 samples x 30 s, bonuses on; FIXES $(date +%F))" > $out
for c in forgebody lampknight hunter emberblade wildsoul; do
  p=final; [ $c = lampknight ] && p=final2; [ $c = hunter ] && p=trim; [ $c = emberblade ] && p=trim; [ $c = forgebody ] && p=fm12
  for lv in 30 50; do bash analyze.sh ${p}_${c}_$lv.txt | grep -v starter | awk -v lv=$lv '{s=$3" "$4; print $1"\t"s"\t"lv"\t"$10}'; done
done | awk -F'\t' '{k=$1"\t"$2; v[k,$3]=$4; keys[k]=1} END{for(k in keys) printf "%s\t%s\t%s\n", k, (v[k,30]?v[k,30]:"-"), (v[k,50]?v[k,50]:"-")}' | sort >> $out
cat $out
