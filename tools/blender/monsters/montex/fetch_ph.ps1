# Download CC0 PBR texture sets from Poly Haven (1K JPG maps) into Y:\assets_src\montex\<id>\ .  Data only, nothing is executed.
param([string]$idlist, [string]$res = "1k")
$ids = $idlist.Split(",")
$root = "Y:\assets_src\montex"
$api = Join-Path $root "_api"
$ua = "Mozilla/5.0"
foreach ($id in $ids) {
    $dst = Join-Path $root $id
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    $fj = Join-Path $api ("ph_files_" + $id + ".json")
    if (-not (Test-Path $fj)) { & curl.exe -sS -A $ua -o $fj ("https://api.polyhaven.com/files/" + $id) }
    $j = Get-Content -Raw -LiteralPath $fj | ConvertFrom-Json
    $maps = @{ "Diffuse" = "diff"; "nor_gl" = "nor_gl"; "Rough" = "rough"; "Displacement" = "disp"; "AO" = "ao" }
    $got = @()
    foreach ($k in $maps.Keys) {
        $m = $j.$k
        if (-not $m) { continue }
        $r = $m.$res
        if (-not $r) { continue }
        $f = $r.jpg
        if (-not $f) { continue }
        $out = Join-Path $dst ($maps[$k] + "_" + $res + ".jpg")
        if (-not (Test-Path $out) -or (Get-Item $out).Length -lt 1000) {
            & curl.exe -sS -L -A $ua -o $out $f.url
        }
        $got += ("{0}:{1}k" -f $maps[$k], [int]((Get-Item $out).Length / 1024))
    }
    "{0}  {1}" -f $id, ($got -join " ")
}
