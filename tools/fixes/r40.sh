#!/bin/bash
S='Y:\shots\fixes\readme040'
bash runq.sh r40_talk -Priority -GameArgs "--win --mute --slot=94 --new=lampknight --level=1 --area=town --named=none --uidir=$S --uido=@Y:\tools\fixes\scripts\r40_talk.txt --quit-at=40" -Timeout 120 | grep -E "^log:|BUILD"
bash runq.sh r40_book -Priority -NoBuild -GameArgs "--win --mute --slot=94 --new=soulbinder --level=40 --paragon=10 --tier=2 --gear --hands=off --golem=iron --forge --area=town --named=none --uidir=$S --uido=@Y:\tools\fixes\scripts\r40_book.txt --quit-at=60" -Timeout 120 | grep -E "^log:"
bash runq.sh r40_filter -Priority -NoBuild -GameArgs "--win --mute --slot=94 --new=lampknight --level=50 --tier=2 --gear --hands=2h --area=town --named=none --uidir=$S --uido=@Y:\tools\fixes\scripts\r40_filter.txt --quit-at=60" -Timeout 120 | grep -E "^log:"
bash runq.sh r40_class -Priority -NoBuild -GameArgs "--win --mute --uidir=$S --uido=@Y:\tools\fixes\scripts\r40_class.txt --quit-at=60" -Timeout 120 | grep -E "^log:"
echo R40 DONE
