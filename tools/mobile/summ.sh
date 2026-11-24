#!/usr/bin/env bash
# one line per boss run: class area mobile | fight (killed, secs, deaths) | boss marks (threats/dodged/taken) | other damage | shots on the move | end
awk 'function out(){ if (cls!="") printf "%-12s %-11s %-14s %-38s %-22s %-15s %-30s | %s\n", cls, area, m, f, b, o, k, e; cls="" }
/^===/{out(); cls=$4; area=$5; m=$7; f=b=o=k=e=""}
/TEST FIGHT/{match($0,/killed=[A-Za-z]+ secs=[0-9]+ deaths=[0-9]+/); f=substr($0,RSTART,RLENGTH)}
/TEST RESULT/{match($0,/\(boss [0-9\/]+\)/); b=substr($0,RSTART,RLENGTH); match($0,/dmgOther=[0-9]+/); o=substr($0,RSTART,RLENGTH); match($0,/kiteShots=[0-9]+ dodgeShots=[0-9]+/); k=substr($0,RSTART,RLENGTH); match($0,/end=[a-z ]+t=[0-9]+s/); e=substr($0,RSTART,RLENGTH)}
END{out()}' "${1:-Y:/tools/mobile/boss_results.txt}"
