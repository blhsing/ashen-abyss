# Lists the mesh nodes of a character GLB (reads only the JSON chunk): skin, parent, vertex count, bounds of the first primitive; then the bones.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\ui_tests\glb_nodes.ps1 -Id hero_lampknight
param([string]$Id = 'hero_emberblade')
$f = "Y:\game\assets\chars\$Id.glb"
$fs = [IO.File]::OpenRead($f)
try { $br = New-Object IO.BinaryReader($fs); [void]$br.ReadBytes(12); $len = $br.ReadUInt32(); [void]$br.ReadUInt32(); $json = [Text.Encoding]::UTF8.GetString($br.ReadBytes($len)) } finally { $fs.Dispose() }
$j = $json | ConvertFrom-Json
$i = 0
$parent = @{}
foreach ($n in $j.nodes) { if ($n.PSObject.Properties.Name -contains 'children') { foreach ($c in $n.children) { $parent[[int]$c] = $i } }; $i++ }
$i = 0
foreach ($n in $j.nodes) {
  if ($n.PSObject.Properties.Name -contains 'mesh') {
    $sk = if ($n.PSObject.Properties.Name -contains 'skin') { "skin=$($n.skin)" } else { 'no skin' }
    $p = if ($parent.ContainsKey($i)) { $j.nodes[$parent[$i]].name } else { '(root)' }
    $m = $j.meshes[$n.mesh]
    $verts = 0; foreach ($pr in $m.primitives) { $verts += $j.accessors[$pr.attributes.POSITION].count }
    $acc = $j.accessors[$m.primitives[0].attributes.POSITION]
    "{0,-14} {1,-8} parent={2,-12} verts={3,6} min=({4}) max=({5}) T={6} R={7} S={8}" -f $n.name, $sk, $p, $verts, (($acc.min | ForEach-Object { '{0:0.00}' -f $_ }) -join ','), (($acc.max | ForEach-Object { '{0:0.00}' -f $_ }) -join ','), ($n.translation -join ','), ($n.rotation -join ','), ($n.scale -join ',')
  }
  $i++
}
"skins: " + (($j.skins | ForEach-Object { "$($_.name) joints=$(@($_.joints).Count)" }) -join '; ')
"bones: " + (($j.skins[0].joints | ForEach-Object { $j.nodes[$_].name }) -join ' ')
"scenes root nodes: " + (($j.scenes[0].nodes | ForEach-Object { $j.nodes[$_].name }) -join ' ')
