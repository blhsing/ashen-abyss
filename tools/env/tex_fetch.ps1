# Phase 1: resolve download URLs for every texture set in tex_spec.json and fetch the raw files.
#   Poly Haven -> Y:\assets_src\env\polyhaven\tex\<id>\<map>_<res>.jpg (+ info.json)
#   ambientCG  -> Y:\assets_src\env\ambientcg\<id>\<id>_<RES>-JPG.zip (extracted next to it, + info.json)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$ua = 'Mozilla/5.0 (compatible; asset-fetch/1.0)'
$spec = Get-Content Y:\tools\env\tex_spec.json -Raw | ConvertFrom-Json
$cfg = New-Object System.Collections.Generic.List[string]
foreach ($s in $spec) {
  if ($s.src -eq 'ph') {
    $dir = "Y:\assets_src\env\polyhaven\tex\$($s.id)"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $fj = Join-Path $dir 'files.json'
    if (-not (Test-Path $fj)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/files/$($s.id)" -UserAgent $ua -OutFile $fj -UseBasicParsing }
    $ij = Join-Path $dir 'info.json'
    if (-not (Test-Path $ij)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/info/$($s.id)" -UserAgent $ua -OutFile $ij -UseBasicParsing }
    $f = Get-Content $fj -Raw | ConvertFrom-Json
    $maps = @{ albedo = @('Diffuse', 'diff', 'Color', 'col_1', 'col1'); normal = @('nor_gl'); arm = @('arm'); height = @('Displacement', 'disp'); ao = @('AO'); rough = @('Rough'); metal = @('Metal') }
    foreach ($m in $maps.Keys) {
      $key = $maps[$m] | Where-Object { $f.PSObject.Properties.Name -contains $_ } | Select-Object -First 1
      if (-not $key) { if ($m -in 'albedo', 'normal', 'height') { Write-Host "MISSING $m for $($s.id): keys = $($f.PSObject.Properties.Name -join ',')" }; continue }
      $r = $f.$key.($s.res)
      if (-not $r -or -not $r.jpg) { Write-Host "no $($s.res) jpg for $($s.id) $key"; continue }
      $o = Join-Path $dir "$m`_$($s.res).jpg"
      if (-not (Test-Path $o) -or (Get-Item $o).Length -ne $r.jpg.size) {
        $cfg.Add("url = `"$($r.jpg.url)`""); $cfg.Add("output = `"$($o -replace '\\','/')`"")
      }
    }
  }
  else {
    $dir = "Y:\assets_src\env\ambientcg\$($s.id)"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $ij = Join-Path $dir 'info.json'
    if (-not (Test-Path $ij)) { Invoke-WebRequest -Uri "https://ambientcg.com/api/v2/full_json?id=$($s.id)&include=downloadData,dimensionsData,tagData" -UserAgent $ua -OutFile $ij -UseBasicParsing }
    $z = Join-Path $dir "$($s.id)_$($s.res.ToUpper())-JPG.zip"
    if (-not (Test-Path $z)) {
      $cfg.Add("url = `"https://ambientcg.com/get?file=$($s.id)_$($s.res.ToUpper())-JPG.zip`""); $cfg.Add("output = `"$($z -replace '\\','/')`"")
    }
  }
}
$cf = 'Y:\assets_src\env\tex_curl.cfg'
[IO.File]::WriteAllLines($cf, $cfg)
Write-Host "to download: $($cfg.Count / 2) files"
if ($cfg.Count -gt 0) {
  & curl.exe --parallel --parallel-max 8 -L -s -S --retry 3 --fail -A $ua -K $cf
  Write-Host "curl exit $LASTEXITCODE"
}
# extract ambientCG zips (each into its own folder)
foreach ($s in $spec | Where-Object { $_.src -eq 'acg' }) {
  $dir = "Y:\assets_src\env\ambientcg\$($s.id)"
  $z = Join-Path $dir "$($s.id)_$($s.res.ToUpper())-JPG.zip"
  $x = Join-Path $dir 'x'
  if ((Test-Path $z) -and -not (Test-Path $x)) { Expand-Archive -LiteralPath $z -DestinationPath $x; Write-Host "extracted $($s.id): $((Get-ChildItem $x).Name -join ', ')" }
}
