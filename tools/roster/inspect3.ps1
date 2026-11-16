$d = Get-Content -Raw -Encoding UTF8 Y:\game\data\bestiary.json | ConvertFrom-Json
$r = $d.regions[1]
$r | ConvertTo-Json -Depth 5 -Compress | % { $_.Substring(0, [Math]::Min(3500, $_.Length)) }
"--- anims of non-A rigs + a few A"
foreach ($t in $d.types) { if ($t.rig -ne 'A' -or $t.build -ne 'built') { "{0} [{1}] {2} || {3}" -f $t.id,$t.rig,$t.anims,$t.rig_note } }
