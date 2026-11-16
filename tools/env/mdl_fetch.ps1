# Fetch Poly Haven glTF models (gltf + bin + textures) listed in mdl_ids.txt ("id res" per line, res default 1k)
# into Y:\assets_src\env\polyhaven\models\<id>\  (relative paths preserved, plus files.json / info.json)
param([string]$List = 'Y:\tools\env\mdl_ids.txt')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$ua = 'Mozilla/5.0 (compatible; asset-fetch/1.0)'
$cfg = New-Object System.Collections.Generic.List[string]
$total = 0
foreach ($line in Get-Content $List) {
  $line = $line.Trim(); if (-not $line -or $line.StartsWith('#')) { continue }
  $parts = $line -split '\s+'; $id = $parts[0]; $res = if ($parts.Count -gt 1) { $parts[1] } else { '1k' }
  $dir = "Y:\assets_src\env\polyhaven\models\$id"
  New-Item -ItemType Directory -Force "$dir\textures" | Out-Null
  $fj = Join-Path $dir 'files.json'
  if (-not (Test-Path $fj)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/files/$id" -UserAgent $ua -OutFile $fj -UseBasicParsing }
  $ij = Join-Path $dir 'info.json'
  if (-not (Test-Path $ij)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/info/$id" -UserAgent $ua -OutFile $ij -UseBasicParsing }
  $f = Get-Content $fj -Raw | ConvertFrom-Json
  $g = $f.gltf.$res.gltf
  if (-not $g) { Write-Host "NO gltf $res for $id"; continue }
  $o = Join-Path $dir "$id.gltf"
  # the .gltf differs per resolution: keep a marker so a res change re-downloads
  $mark = Join-Path $dir "res_$res.txt"
  if (-not (Test-Path $mark)) { Remove-Item $o -ErrorAction SilentlyContinue; Remove-Item "$dir\textures\*" -ErrorAction SilentlyContinue; Remove-Item "$dir\res_*.txt" -ErrorAction SilentlyContinue; Set-Content $mark $res }
  if (-not (Test-Path $o) -or (Get-Item $o).Length -ne $g.size) { $cfg.Add("url = `"$($g.url)`""); $cfg.Add("output = `"$($o -replace '\\','/')`""); $total += $g.size }
  foreach ($p in $g.include.PSObject.Properties) {
    $rel = $p.Name -replace '/', '\'
    if ($rel -match '\.\.' -or [IO.Path]::IsPathRooted($rel)) { Write-Host "skip odd path $rel"; continue }
    $op = Join-Path $dir $rel
    New-Item -ItemType Directory -Force (Split-Path $op) | Out-Null
    if (-not (Test-Path $op) -or (Get-Item $op).Length -ne $p.Value.size) { $cfg.Add("url = `"$($p.Value.url)`""); $cfg.Add("output = `"$($op -replace '\\','/')`""); $total += $p.Value.size }
  }
}
$cf = 'Y:\assets_src\env\mdl_curl.cfg'
[IO.File]::WriteAllLines($cf, $cfg)
Write-Host ("to download: {0} files, {1:N1} MB" -f ($cfg.Count / 2), ($total / 1MB))
if ($cfg.Count -gt 0) {
  & curl.exe --parallel --parallel-max 10 -L -s -S --retry 3 --fail -A $ua -K $cf
  Write-Host "curl exit $LASTEXITCODE"
}
