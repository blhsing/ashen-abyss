param([string]$json = "Y:\assets_src\montex\_api\ph_textures.json", [string]$pat = ".")
$j = Get-Content -Raw -LiteralPath $json | ConvertFrom-Json
foreach ($p in $j.PSObject.Properties) {
    $a = $p.Value
    $cats = ($a.categories -join ",")
    $line = "{0} | {1} | {2} | {3}" -f $p.Name, $cats, (($a.authors.PSObject.Properties | ForEach-Object { $_.Name }) -join "; "), ($a.dimensions -join "x")
    if ($line -match $pat) { $line }
}
