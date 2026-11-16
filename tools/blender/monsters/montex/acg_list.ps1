param([string]$dir = "Y:\assets_src\montex\_api")
Get-ChildItem -LiteralPath $dir -Filter "acg_*.json" | ForEach-Object {
    $j = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
    "== " + $_.BaseName + "  (" + $j.numberOfResults + ")"
    foreach ($a in $j.foundAssets) {
        $tags = ($a.tags | Select-Object -First 10) -join ","
        "{0} | {1} | {2} | {3}x{4}" -f $a.assetId, $a.displayName, $tags, $a.dimensionX, $a.dimensionY
    }
}
