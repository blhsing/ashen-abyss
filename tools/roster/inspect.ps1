param([string]$What = "top")
$d = Get-Content -Raw -Encoding UTF8 Y:\game\data\bestiary.json | ConvertFrom-Json
if ($What -eq "top") {
  $d.PSObject.Properties | % { $n=$_.Name; $v=$_.Value; if ($v -is [array]) { "$n : array $($v.Count)" } else { "$n : $($v.GetType().Name)" } }
  "--- meta"; $d.meta | ConvertTo-Json -Depth 6
  "--- type[0]"; $d.types[0] | ConvertTo-Json -Depth 6
  "--- reskin[0]"; $d.reskins[0] | ConvertTo-Json -Depth 6
  "--- family[0]"; $d.families[0] | ConvertTo-Json -Depth 6
  "--- behaviours[0]"; $d.behaviours[0] | ConvertTo-Json -Depth 6
  "--- named keys"; $d.named.PSObject.Properties | % { "$($_.Name) : $($_.Value.Count)" }
  "--- named.rare_elites[0]"; $d.named.rare_elites[0] | ConvertTo-Json -Depth 6
}
