$d = Get-Content -Raw -Encoding UTF8 Y:\game\data\bestiary.json | ConvertFrom-Json
"--- types: id | family | role | ai | rig | build | variant_of | model | el | hp dmg spd scale | w stage | status"
foreach ($t in $d.types) { "{0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} {10} {11} {12} | {13} {14} | {15}" -f $t.id,$t.family,$t.role,$t.ai,$t.rig,$t.build,$t.variant_of,$t.model,$t.element,$t.hp,$t.dmg,$t.speed,$t.scale,$t.spawn_weight,$t.stage,$t.status }
