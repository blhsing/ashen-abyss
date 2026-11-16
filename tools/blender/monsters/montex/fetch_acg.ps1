# Download CC0 PBR texture sets from ambientCG (1K JPG zip) into Y:\assets_src\montex\<AssetId>\ and unpack the maps there.
# Data only: the archives contain images (and a usdc/mtlx description that is not used); nothing is executed.
param([string]$idlist, [string]$res = "1K")
$ids = $idlist.Split(",")
$root = "Y:\assets_src\montex"
$ua = "Mozilla/5.0"
foreach ($id in $ids) {
    $dst = Join-Path $root $id
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    $zip = Join-Path $dst ("{0}_{1}-JPG.zip" -f $id, $res)
    if (-not (Test-Path $zip) -or (Get-Item $zip).Length -lt 10000) {
        & curl.exe -sS -L -A $ua -o $zip ("https://ambientcg.com/get?file={0}_{1}-JPG.zip" -f $id, $res)
    }
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $z = [System.IO.Compression.ZipFile]::OpenRead($zip)
        foreach ($e in $z.Entries) {
            if ($e.Name -match '\.(jpg|png)$' -and $e.Name -notmatch '[\/]') {
                $out = Join-Path $dst $e.Name
                if (-not (Test-Path $out)) { [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $out, $false) }
            }
        }
        $z.Dispose()
        "{0}  {1}" -f $id, ((Get-ChildItem -LiteralPath $dst -Filter *.jpg | ForEach-Object { $_.Name -replace "^.*-JPG_", "" -replace "\.jpg$", "" }) -join " ")
    } catch { "{0}  FAILED: {1}" -f $id, $_.Exception.Message }
}
