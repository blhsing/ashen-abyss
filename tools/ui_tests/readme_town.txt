# README gallery: town and the key panels with a geared level-50 縛淵術士 (run with --new=pactbinder --level=50 --paragon=12 --gear --area=town --slot=92)
wait 5
shot town_1
wait 3
shot town_2
give set
give gem
gold 250000
mats 300
points 4
ppoints 30
wait 0.5
open inventory
wait 1.0
shot inventory
hover 1018,505
wait 0.8
shot tip_a
hover 1416,505
wait 0.8
shot tip_b
hover 1529,505
wait 0.8
shot tip_c
hover 1132,562
wait 0.8
shot tip_d
hover 1302,562
wait 0.8
shot tip_e
unhover
close
wait 0.4
open skills
wait 1.0
shot skills
hover 850,142
wait 0.8
shot skills_tip
unhover
close
wait 0.4
open paragon
wait 1.2
shot paragon
click 608,490
wait 0.4
hover 642,490
wait 0.8
shot paragon_tip
unhover
close
wait 0.4
open map
wait 1.0
shot map_area
click @世界
wait 0.8
shot map_world
hover 662,527
wait 0.9
shot map_world_tip
unhover
close
wait 0.4
open vendor:smith
wait 0.9
shot smith
click @精鑄
wait 0.7
click 223,250
wait 0.5
unhover
wait 0.3
shot smith_masterwork
close
wait 0.4
open vendor:merchant
wait 0.9
hover 223,250
wait 0.8
shot merchant
unhover
close
wait 0.4
open quests
wait 0.9
shot quests
close
wait 0.4
open dialogue:intro
wait 2.5
shot dialogue_intro
wait 2.5
shot dialogue_intro2
key Escape
wait 0.8
open dialogue:elder_first
wait 2.5
shot dialogue_elder
key Escape
wait 0.8
close
open tier
wait 1.0
shot tier
close
wait 0.4
open settings
wait 1.0
shot settings_ingame
close
wait 0.5
quit
