# Builds the release manifest that the web installer (AshenAbyss-<ver>-Setup.exe) reads before it downloads anything:
#   AshenAbyss-<ver>-manifest.json = { version, tag, archive, parts:[{name,size,sha256}], install_size, files, exe, top_level }
# It is computed from the LOCAL split parts exactly as they are (read-only; the parts are never touched), so the lead
# can upload it next to them. Pure ASCII.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\installer\make_manifest.ps1
#       [-PartsDir Y:\release_out\new] [-Version 0.1.0] [-OutDir Y:\release_out\installer] [-SevenZip <7z.exe>]
#
# install_size / files / top_level come from the archive listing (7-Zip's 7z.exe); without 7z.exe they are estimated
# (install_size = 1.15 x download size) and top_level falls back to the known layout of the Godot export.
param(
    [string]$PartsDir = 'Y:\release_out\new',
    [string]$Version = '0.1.0',
    [string]$OutDir = 'Y:\release_out\installer',
    [string]$SevenZip = ''
)
$ErrorActionPreference = 'Stop'
$prefix = "AshenAbyss-$Version-win64.7z."
$parts = @(Get-ChildItem -LiteralPath $PartsDir -File | Where-Object { $_.Name.StartsWith($prefix) -and $_.Name.Substring($prefix.Length) -match '^\d{3}$' } | Sort-Object Name)
if ($parts.Count -eq 0) { throw "no parts named $prefix### in $PartsDir" }
for ($i = 0; $i -lt $parts.Count; $i++) {
    $want = $prefix + ('{0:D3}' -f ($i + 1))
    if ($parts[$i].Name -ne $want) { throw "part sequence broken: expected $want, found $($parts[$i].Name)" }
}

$entries = @()
$total = [int64]0
foreach ($p in $parts) {
    $t0 = Get-Date
    $h = (Get-FileHash -LiteralPath $p.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output ("{0}  {1,14:N0} bytes  sha256 {2}  ({3:N0} s)" -f $p.Name, $p.Length, $h, ((Get-Date) - $t0).TotalSeconds)
    $entries += [pscustomobject]@{ name = $p.Name; size = [int64]$p.Length; sha256 = $h }
    $total += $p.Length
}

# archive listing: unpacked size, file count and the top-level items (used for in-place upgrades and the uninstaller)
if (-not $SevenZip) {
    foreach ($c in @("$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe")) { if ($c -and (Test-Path -LiteralPath $c)) { $SevenZip = $c; break } }
}
$installSize = [int64]0; $files = 0; $top = New-Object System.Collections.Generic.SortedSet[string] ([StringComparer]::OrdinalIgnoreCase)
if ($SevenZip) {
    $path = $null; $isDir = $false
    $lines = & $SevenZip l -slt -ba $parts[0].FullName
    if ($LASTEXITCODE -ne 0) { throw "7z listing failed ($LASTEXITCODE)" }
    foreach ($l in ($lines + '')) {
        if ($l -match '^Path = (.+)$') { $path = $Matches[1]; $isDir = $false; $size = [int64]0 }
        elseif ($l -match '^Size = (\d+)$') { $size = [int64]$Matches[1] }
        elseif ($l -match '^Attributes = (.*)$') { $isDir = $Matches[1].StartsWith('D') }
        elseif ($l -eq '' -and $path) {
            [void]$top.Add(($path -split '[\\/]')[0])
            if (-not $isDir) { $installSize += $size; $files++ }
            $path = $null
        }
    }
} else {
    Write-Output 'warning: 7z.exe not found - install_size estimated, top_level from the known layout'
    $installSize = [int64]($total * 1.15)
    foreach ($n in 'AshenAbyss.exe', 'AshenAbyss.ico', 'AshenAbyss.pck', 'assets', 'data_AshenAbyss_windows_x86_64') { [void]$top.Add($n) }
}
if (-not $top.Contains('AshenAbyss.exe')) { throw 'AshenAbyss.exe is not at the top level of the archive' }

# hand-written JSON (stable layout, one part per line; PowerShell 5's ConvertTo-Json formatting is ugly)
$nl = "`n"
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append('{' + $nl)
[void]$sb.Append('  "format": 1,' + $nl)
[void]$sb.Append('  "product": "AshenAbyss",' + $nl)
[void]$sb.Append("  ""version"": ""$Version""," + $nl)
[void]$sb.Append("  ""tag"": ""v$Version""," + $nl)
[void]$sb.Append("  ""archive"": ""$($parts[0].Name)""," + $nl)
[void]$sb.Append('  "parts": [' + $nl)
for ($i = 0; $i -lt $entries.Count; $i++) {
    $e = $entries[$i]; $sep = if ($i -lt $entries.Count - 1) { ',' } else { '' }
    [void]$sb.Append("    { ""name"": ""$($e.name)"", ""size"": $($e.size), ""sha256"": ""$($e.sha256)"" }$sep" + $nl)
}
[void]$sb.Append('  ],' + $nl)
[void]$sb.Append("  ""download_size"": $total," + $nl)
[void]$sb.Append("  ""install_size"": $installSize," + $nl)
[void]$sb.Append("  ""files"": $files," + $nl)
[void]$sb.Append('  "exe": "AshenAbyss.exe",' + $nl)
[void]$sb.Append('  "top_level": [' + (($top | ForEach-Object { '"' + $_ + '"' }) -join ', ') + ']' + $nl)
[void]$sb.Append('}' + $nl)

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir "AshenAbyss-$Version-manifest.json"
[IO.File]::WriteAllText($out, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
Write-Output ("manifest: {0}  ({1} parts, download {2:N0} bytes, install {3:N0} bytes, {4} files)" -f $out, $entries.Count, $total, $installSize, $files)
