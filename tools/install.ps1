<#
.SYNOPSIS
  Exports Ashen Abyss with the Godot 4.7.2 .NET console executable and installs it for the current user.

.DESCRIPTION
  1. finds the Godot console executable under %LOCALAPPDATA%\Programs\Godot_4.7.2_mono (*_console.exe)
  2. imports the project and exports the "Windows Desktop" preset (release) into a staging folder
  3. copies the loose icon folder next to the executable as assets\icons
     (res://assets/icons carries a .gdignore, is not packed, and is read from disk at runtime)
  4. mirrors the staging folder to %USERPROFILE%\Games\AshenAbyss (see the note at the InstallDir parameter)
  5. creates the Start Menu shortcut "<product name>.lnk" with WScript.Shell (working directory = install folder)

  Safe to run again: every step replaces what the previous run left behind. Saves and settings live in
  %APPDATA%\AshenAbyss and are never touched.

  This file is deliberately pure ASCII: Windows PowerShell 5.1 reads BOM-less scripts as ANSI, which would mangle
  the CJK product name. The name is read from project.godot (config/name) instead.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Y:\tools\install.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Y:\tools\install.ps1 -SkipExport      # reinstall the last export
#>
[CmdletBinding()]
param(
    [string]$Project,
    [string]$GodotDir = (Join-Path $env:LOCALAPPDATA 'Programs\Godot_4.7.2_mono'),
    # NOT under AppData: when this script runs inside the Claude desktop app, everything it creates under AppData
    # lands in the app's private storage (...\Packages\Claude_*\LocalCache), where Explorer and the Start menu
    # cannot see it. A folder in the user profile outside AppData is real for everybody.
    [string]$InstallDir = (Join-Path $env:USERPROFILE 'Games\AshenAbyss'),
    [string]$Staging = (Join-Path ([System.IO.Path]::GetTempPath()) 'AshenAbyss_export'),
    [string]$Preset = 'Windows Desktop',
    [switch]$SkipExport,
    [switch]$NoShortcut,
    [switch]$Force,
    [switch]$Launch
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$ExeName = 'AshenAbyss.exe'
$started = Get-Date
$done = New-Object System.Collections.Generic.List[string]

function Say([string]$Text) { Write-Host "[install] $Text" }
function Warn([string]$Text) { Write-Host "[install] WARNING: $Text" -ForegroundColor Yellow }
function Fail([string]$Text) {
    Write-Host "[install] ERROR: $Text" -ForegroundColor Red
    exit 1
}

function Get-ProductName([string]$ProjectFile) {
    # U+71FC U+6DF5 + " Ashen Abyss"
    $fallback = ([string][char]0x71FC) + ([string][char]0x6DF5) + ' Ashen Abyss'
    try {
        $text = [System.IO.File]::ReadAllText($ProjectFile, [System.Text.Encoding]::UTF8)
        $m = [regex]::Match($text, '(?m)^config/name="([^"]+)"')
        if ($m.Success -and $m.Groups[1].Value.Trim().Length -gt 0) { return $m.Groups[1].Value.Trim() }
    } catch { }
    return $fallback
}

function Get-FolderStats([string]$Path) {
    $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction SilentlyContinue)
    $bytes = 0
    foreach ($f in $files) { $bytes += $f.Length }
    return ('{0} files, {1:N1} MB' -f $files.Count, ($bytes / 1MB))
}

function Copy-Mirror([string]$From, [string]$To, [string[]]$ExcludeFiles) {
    $roboArgs = @($From, $To, '/MIR', '/R:2', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($ExcludeFiles -and $ExcludeFiles.Count -gt 0) { $roboArgs += '/XF'; $roboArgs += $ExcludeFiles }
    & robocopy.exe @roboArgs | Out-Null
    $code = $LASTEXITCODE
    if ($code -ge 8) { Fail "robocopy '$From' -> '$To' failed with exit code $code" }
}

function Invoke-Godot([string]$Exe, [string[]]$GodotArgs, [string]$LogFile) {
    $quoted = foreach ($a in $GodotArgs) { if ($a -match '[\s"]') { '"' + ($a -replace '"', '\"') + '"' } else { $a } }
    $argLine = $quoted -join ' '
    Say "running: $([System.IO.Path]::GetFileName($Exe)) $argLine"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.Arguments = $argLine
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    [void]$proc.Start()
    # both pipes are drained asynchronously so a chatty process can never block on a full buffer
    $outTask = $proc.StandardOutput.ReadToEndAsync()
    $errTask = $proc.StandardError.ReadToEndAsync()
    $proc.WaitForExit()
    $out = $outTask.Result
    $err = $errTask.Result
    $code = $proc.ExitCode
    $proc.Dispose()
    $log = "===== stdout =====`r`n$out`r`n===== stderr =====`r`n$err`r`n===== exit code $code ====="
    [System.IO.File]::WriteAllText($LogFile, $log, (New-Object System.Text.UTF8Encoding($true)))
    return $code
}

function Show-LogTail([string]$LogFile, [int]$Lines = 40) {
    if (-not (Test-Path -LiteralPath $LogFile)) { return }
    Say "last $Lines lines of ${LogFile}:"
    Get-Content -LiteralPath $LogFile -Encoding UTF8 -Tail $Lines | ForEach-Object { Write-Host "    $_" }
}

# ---------------------------------------------------------------- paths

if (-not $Project) {
    if (Test-Path -LiteralPath 'Y:\game\project.godot') { $Project = 'Y:\game' }
    else { $Project = Join-Path (Split-Path -Parent $PSScriptRoot) 'game' }
}
if (-not (Test-Path -LiteralPath (Join-Path $Project 'project.godot'))) { Fail "no project.godot in '$Project'" }
$Project = (Resolve-Path -LiteralPath $Project).Path.TrimEnd('\')
if ($Project -match '^[A-Za-z]:$') { $Project = $Project + '\' }
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
$Staging = [System.IO.Path]::GetFullPath($Staging).TrimEnd('\')
$logDir = $Staging + '_logs'
$productName = Get-ProductName (Join-Path $Project 'project.godot')
$safeName = ($productName -replace '[\\/:*?"<>|]', ' ').Trim()
if ($safeName.Length -eq 0) { $safeName = 'Ashen Abyss' }

Say "product      : $productName"
Say "project      : $Project"
Say "staging      : $Staging"
Say "install dir  : $InstallDir"

foreach ($pair in @(@('install dir', $InstallDir), @('staging', $Staging))) {
    $p = $pair[1]
    if ($p.Length -lt 8 -or ($p -split '\\').Count -lt 3) { Fail "the $($pair[0]) '$p' is too close to a drive root; refusing to mirror into it" }
}
if ($InstallDir -eq $Staging -or $InstallDir.StartsWith($Project + '\', [System.StringComparison]::OrdinalIgnoreCase) -or $InstallDir -eq $Project) {
    Fail 'the install dir must not be the staging folder or lie inside the project'
}
if ((Test-Path -LiteralPath $InstallDir) -and -not $Force) {
    $existing = @(Get-ChildItem -LiteralPath $InstallDir -Force -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0 -and -not (Test-Path -LiteralPath (Join-Path $InstallDir $ExeName))) {
        Fail "'$InstallDir' exists, is not empty and does not look like an earlier install (no $ExeName). Use -Force to replace it anyway."
    }
}

# ---------------------------------------------------------------- export

$stagedExe = Join-Path $Staging $ExeName
$stagedPck = [System.IO.Path]::ChangeExtension($stagedExe, '.pck')

if ($SkipExport) {
    if (-not (Test-Path -LiteralPath $stagedExe)) { Fail "-SkipExport was given but there is no earlier export at '$stagedExe'" }
    Say 'export skipped; reusing the staging folder'
    $done.Add("reused the earlier export in $Staging")
}
else {
    if (-not (Test-Path -LiteralPath $GodotDir)) { Fail "Godot folder not found: $GodotDir" }
    $godot = Get-ChildItem -LiteralPath $GodotDir -Recurse -File -Filter '*_console.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName | Select-Object -First 1
    if (-not $godot) { Fail "no *_console.exe found under $GodotDir" }
    Say "godot        : $($godot.FullName)"

    if (-not (Test-Path -LiteralPath (Join-Path $Project 'export_presets.cfg'))) { Fail "no export_presets.cfg in '$Project'" }
    $presetText = [System.IO.File]::ReadAllText((Join-Path $Project 'export_presets.cfg'), [System.Text.Encoding]::UTF8)
    if ($presetText -notmatch ('(?m)^name="' + [regex]::Escape($Preset) + '"')) { Fail "export preset '$Preset' is not defined in export_presets.cfg" }

    $templates = Join-Path $env:APPDATA 'Godot\export_templates'
    $haveTemplates = (Test-Path -LiteralPath $templates) -and (@(Get-ChildItem -LiteralPath $templates -Directory -Filter '4.7.2*mono*' -ErrorAction SilentlyContinue).Count -gt 0)
    if (-not $haveTemplates) { Warn "no 4.7.2 mono export templates under $templates; the export will fail until they are installed (Editor > Manage Export Templates)" }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Warn 'dotnet is not on PATH; the C# build inside the export will fail' }

    if (Test-Path -LiteralPath $Staging) { Remove-Item -LiteralPath $Staging -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Staging | Out-Null
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null

    $importLog = Join-Path $logDir 'import.log'
    $code = Invoke-Godot $godot.FullName @('--headless', '--path', $Project, '--import') $importLog
    if ($code -ne 0) { Warn "the import pass returned exit code $code (the export imports again, so this is not fatal); see $importLog" }
    else { Say 'import pass finished' }

    $exportLog = Join-Path $logDir 'export.log'
    $t0 = Get-Date
    $code = Invoke-Godot $godot.FullName @('--headless', '--path', $Project, '--export-release', $Preset, $stagedExe) $exportLog
    if ($code -ne 0 -or -not (Test-Path -LiteralPath $stagedExe)) {
        Show-LogTail $exportLog 60
        Fail "the export failed (exit code $code). Full log: $exportLog"
    }
    if (-not (Test-Path -LiteralPath $stagedPck)) {
        Show-LogTail $exportLog 60
        Fail "the export produced no .pck next to the executable ($stagedPck). Full log: $exportLog"
    }
    # Godot returns exit code 0 and writes exe + pck even when the C# part of the export failed (for example when
    # the solution file is missing); the result then has no .NET assemblies and cannot start. Catch that here.
    $exportText = [System.IO.File]::ReadAllText($exportLog, [System.Text.Encoding]::UTF8)
    $dataDirs = @(Get-ChildItem -LiteralPath $Staging -Directory -Filter 'data_*' -ErrorAction SilentlyContinue)
    if ($exportText -match 'ERROR: Export \.NET Project' -or $dataDirs.Count -eq 0) {
        $first = ($exportText -split "`n" | Where-Object { $_ -match 'ERROR: Export \.NET Project|error CS\d+|Build FAILED' } | Select-Object -First 5) -join "`n    "
        Fail "the export finished without the .NET assemblies (no data_* folder next to the executable).`n    $first`n    Full log: $exportLog"
    }
    $secs = [int]((Get-Date) - $t0).TotalSeconds
    Say "exported in $secs s"
    $done.Add("exported preset '$Preset' (release) to $Staging in $secs s; logs in $logDir")
}

# ---------------------------------------------------------------- loose icons

$iconsSrc = Join-Path $Project 'assets\icons'
$iconsDst = Join-Path $Staging 'assets\icons'
if (Test-Path -LiteralPath $iconsSrc) {
    New-Item -ItemType Directory -Force -Path $iconsDst | Out-Null
    Copy-Mirror $iconsSrc $iconsDst @('.gdignore')
    $done.Add("copied the loose icons to assets\icons ($(Get-FolderStats $iconsDst))")
    Say "icons copied : $(Get-FolderStats $iconsDst)"
}
else { Warn "no icon folder at $iconsSrc; the game will draw placeholder icons" }

# ---------------------------------------------------------------- application icon
# The Sun-Forge crest (game/assets/icons/AshenAbyss.ico, built by tools/appicon/make_icon.py) goes into the executable
# (rcedit, if present) and next to it, where the Start-menu shortcut points.
$appIco = Join-Path $Project 'assets\icons\AshenAbyss.ico'
if (Test-Path -LiteralPath $appIco) {
    Copy-Item -LiteralPath $appIco -Destination (Join-Path $Staging 'AshenAbyss.ico') -Force
    $rcedit = Join-Path $env:LOCALAPPDATA 'AshenAbyssTools\rcedit\rcedit-x64.exe'
    if (Test-Path -LiteralPath $rcedit) {
        & $rcedit $stagedExe --set-icon $appIco
        if ($LASTEXITCODE -eq 0) { Say 'icon set in the executable'; $done.Add('set the crest icon in AshenAbyss.exe') }
        else { Warn "rcedit could not set the icon (exit $LASTEXITCODE); the shortcut still uses AshenAbyss.ico" }
    }
    else { Warn "rcedit not found at $rcedit; only the shortcut gets the icon" }
}
else { Warn "no application icon at $appIco" }

# ---------------------------------------------------------------- install

$running = @(Get-Process -Name ([System.IO.Path]::GetFileNameWithoutExtension($ExeName)) -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Fail ("the game is running (process id " + (($running | ForEach-Object { $_.Id }) -join ', ') + '). Close it and run this script again.')
}
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Mirror $Staging $InstallDir @()
$installedExe = Join-Path $InstallDir $ExeName
if (-not (Test-Path -LiteralPath $installedExe)) { Fail "the executable did not arrive at $installedExe" }
$stats = Get-FolderStats $InstallDir
Say "installed    : $stats"
$done.Add("installed the build to $InstallDir ($stats)")

# ---------------------------------------------------------------- Start Menu shortcut

if ($NoShortcut) { Say 'shortcut skipped (-NoShortcut)' }
else {
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    New-Item -ItemType Directory -Force -Path $programs | Out-Null
    $lnk = Join-Path $programs ($safeName + '.lnk')
    # WScript.Shell writes the link through an ANSI path on some systems, so it is created under an ASCII name
    # and then renamed to the real (CJK) name by the file system, which is Unicode throughout.
    $tmp = Join-Path $programs 'AshenAbyss_install_tmp.lnk'
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $sc = $shell.CreateShortcut($tmp)
        $sc.TargetPath = $installedExe
        $sc.WorkingDirectory = $InstallDir
        $ico = Join-Path $InstallDir 'AshenAbyss.ico'
        $sc.IconLocation = if (Test-Path -LiteralPath $ico) { "$ico,0" } else { "$installedExe,0" }
        $sc.Description = 'Ashen Abyss'
        $sc.WindowStyle = 1
        $sc.Save()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($sc)
    }
    finally { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
    if (-not (Test-Path -LiteralPath $tmp)) { Fail "WScript.Shell did not write the shortcut ($tmp)" }
    if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
    Move-Item -LiteralPath $tmp -Destination $lnk -Force
    Say "shortcut     : $lnk"
    $done.Add("created the Start Menu shortcut '$lnk' (target $installedExe, working directory $InstallDir)")
}

# ---------------------------------------------------------------- summary

$total = [int]((Get-Date) - $started).TotalSeconds
Write-Host ''
Say "done in $total s:"
foreach ($line in $done) { Say "  - $line" }
Say "start it from the Start Menu ($safeName) or run: `"$installedExe`""
Say 'test switches go after a double dash, for example:  AshenAbyss.exe -- --win --new=emberblade --level=20 --gear --bot --quit-at=120'

if ($Launch) {
    Say 'launching the game'
    Start-Process -FilePath $installedExe -WorkingDirectory $InstallDir | Out-Null
}
exit 0
