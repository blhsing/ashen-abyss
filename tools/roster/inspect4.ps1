$d = Get-Content -Raw -Encoding UTF8 Y:\game\data\bestiary.json | ConvertFrom-Json
$r = $d.regions[0]
"region keys: " + (($r.PSObject.Properties | % Name) -join ', ')
"family[0]: " + ($r.families[0] | ConvertTo-Json -Compress)
foreach ($k in ($r.PSObject.Properties | % Name)) { if ($k -notin 'families','note','name_zh') { "$k => " + (($r.$k | ConvertTo-Json -Compress -Depth 4) -replace '\s+',' ').Substring(0, [Math]::Min(420, (($r.$k | ConvertTo-Json -Compress -Depth 4) -replace '\s+',' ').Length)) } }
"reskin states: " + (($d.reskins | % state | sort -Unique) -join ',')
"builds: " + (($d.types | % build | sort -Unique) -join ',')
"elements: " + (($d.types | % element | sort -Unique) -join ',') + " / " + (($d.reskins | % element | sort -Unique) -join ',')
"body plans: " + (($d.types | % body_plan | sort -Unique) -join ' | ')
"roles: " + (($d.types | % role | sort -Unique) -join ' | ')
"traits: " + (($d.types | % { $_.ai_traits } | sort -Unique) -join ' | ')
"named.rare keys: " + (($d.named.rare_elites[0].PSObject.Properties | % Name) -join ', ')
"dungeon_boss[0]: " + ($d.named.dungeon_bosses[0] | ConvertTo-Json -Compress).Substring(0,300)
