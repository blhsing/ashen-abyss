# make_imports.ps1 - pre-authors Godot .import sidecars for the environment PBR textures.
#
# Godot reads the [params] block of an existing .import file when it imports a source file for the first time, so
# writing these sidecars BEFORE the editor ever scans the project makes the first import produce GPU-compressed
# (S3TC / RGTC), mip-mapped textures instead of hundreds of lossless 2K images that eat VRAM and shimmer.
#
#   albedo / arm / emission / opacity / height : VRAM compressed, mipmaps, not a normal map
#   normal                                     : VRAM compressed as a normal map (two-channel RGTC), mipmaps
#
# A sidecar that already exists is never touched (Godot owns it once it has imported the file) unless -Force is given.
# Usage:  powershell -ExecutionPolicy Bypass -File Y:\tools\env\make_imports.ps1 [-Root <dir>] [-Force]
# The script only writes small text files; it does not start Godot.
param(
    [string]$Root = 'Y:\game\assets\env\tex',
    [string]$ProjectRoot = 'Y:\game',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path 'Y:\')) {
    cmd /c subst Y: "$(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)" | Out-Null
}
if (-not (Test-Path $Root)) { throw "texture root not found: $Root" }

$projFull = (Get-Item $ProjectRoot).FullName.TrimEnd('\')
$utf8 = New-Object System.Text.UTF8Encoding($false)
$written = 0; $skipped = 0; $normals = 0
$bySet = @{}

$files = Get-ChildItem -Path $Root -Recurse -File | Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|webp|tga|bmp)$' } | Sort-Object FullName
foreach ($f in $files) {
    $side = $f.FullName + '.import'
    if ((Test-Path $side) -and -not $Force) { $skipped++; continue }
    if (-not $f.FullName.StartsWith($projFull, [System.StringComparison]::OrdinalIgnoreCase)) { throw "file outside the project: $($f.FullName)" }

    $res = 'res://' + $f.FullName.Substring($projFull.Length).TrimStart('\').Replace('\', '/')
    $base = $f.BaseName.ToLowerInvariant()
    $isNormal = ($base -eq 'normal') -or $base.EndsWith('_normal') -or $base.EndsWith('_nor')
    $normalMap = if ($isNormal) { 1 } else { 0 }
    # height maps are only ever sampled coarsely: cap them at 1K to save disk and VRAM
    $sizeLimit = if ($base -eq 'height') { 1024 } else { 0 }

    $lines = @(
        '[remap]',
        '',
        'importer="texture"',
        'type="CompressedTexture2D"',
        '',
        '[deps]',
        '',
        "source_file=`"$res`"",
        '',
        '[params]',
        '',
        'compress/mode=2',
        'compress/high_quality=false',
        'compress/lossy_quality=0.7',
        'compress/hdr_compression=1',
        "compress/normal_map=$normalMap",
        'compress/channel_pack=0',
        'mipmaps/generate=true',
        'mipmaps/limit=-1',
        'roughness/mode=1',
        'roughness/src_normal=""',
        'process/fix_alpha_border=false',
        'process/premult_alpha=false',
        'process/normal_map_invert_y=false',
        'process/hdr_as_srgb=false',
        'process/hdr_clamp_exposure=false',
        "process/size_limit=$sizeLimit",
        'detect_3d/compress_to=0',
        ''
    )
    [System.IO.File]::WriteAllText($side, ($lines -join "`n"), $utf8)
    $written++
    if ($isNormal) { $normals++ }
    $set = $f.Directory.Name
    if ($bySet.ContainsKey($set)) { $bySet[$set]++ } else { $bySet[$set] = 1 }
}

Write-Output ("make_imports: {0} texture files under {1}" -f $files.Count, $Root)
Write-Output ("  sidecars written : {0} ({1} normal maps) in {2} sets" -f $written, $normals, $bySet.Count)
Write-Output ("  already present  : {0} (left untouched{1})" -f $skipped, $(if ($Force) { ', -Force was given so none' } else { '' }))
