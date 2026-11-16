# Fetch 2K .hdr skies from Poly Haven into Y:\assets_src\env\polyhaven\hdri\<id>\ and copy to Y:\game\assets\env\sky\<name>.hdr
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$ua = 'Mozilla/5.0 (compatible; asset-fetch/1.0)'
$skies = [ordered]@{
  dusk          = 'qwantani_dusk_2_puresky'
  night         = 'kloppenheim_02_puresky'
  night_dark    = 'kloppenheim_07_puresky'
  cave          = 'drachenfels_cellar'
  overcast      = 'mud_road_puresky'
  snow_overcast = 'snow_field_2_puresky'
  desert_dusk   = 'syferfontein_0d_clear_puresky'
  swamp_fog     = 'kloofendal_misty_morning_puresky'
}
$map = @()
foreach ($k in $skies.Keys) {
  $id = $skies[$k]
  $dir = "Y:\assets_src\env\polyhaven\hdri\$id"
  New-Item -ItemType Directory -Force $dir | Out-Null
  $fj = "$dir\files.json"; $ij = "$dir\info.json"
  if (-not (Test-Path $fj)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/files/$id" -UserAgent $ua -OutFile $fj -UseBasicParsing }
  if (-not (Test-Path $ij)) { Invoke-WebRequest -Uri "https://api.polyhaven.com/info/$id" -UserAgent $ua -OutFile $ij -UseBasicParsing }
  $f = Get-Content $fj -Raw | ConvertFrom-Json
  $h = $f.hdri.'2k'.hdr
  $o = "$dir\$id`_2k.hdr"
  if (-not (Test-Path $o) -or (Get-Item $o).Length -ne $h.size) { & curl.exe -L -s -S --retry 3 --fail -A $ua -o $o $h.url }
  Copy-Item $o "Y:\game\assets\env\sky\$k.hdr" -Force
  $b = [IO.File]::ReadAllBytes($o)[0..9]; $magic = [Text.Encoding]::ASCII.GetString($b)
  Write-Host ("{0,-14} {1,-36} {2,6:N1} MB  magic '{3}'" -f $k, $id, ((Get-Item $o).Length / 1MB), $magic.Trim())
  $map += [pscustomobject]@{ name = $k; id = $id }
}
$map | ConvertTo-Json | Set-Content Y:\assets_src\env\polyhaven\hdri\map.json -Encoding utf8
