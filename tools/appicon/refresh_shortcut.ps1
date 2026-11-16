# Verifies the Start-menu shortcut and the icon file it points to, then makes Windows refresh its icon cache.
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shell = New-Object -ComObject WScript.Shell
Get-ChildItem $programs -Filter *.lnk | Where-Object { $_.Name -match 'Ashen' } | ForEach-Object {
    $t = Join-Path $env:TEMP 'aa_probe.lnk'; Copy-Item -LiteralPath $_.FullName $t -Force
    $l = $shell.CreateShortcut($t)
    "shortcut  : $($_.Name -replace '[^\x20-\x7E]', '?')  written $($_.LastWriteTime.ToString('MM-dd HH:mm'))"
    "target    : $($l.TargetPath)"
    "icon      : $($l.IconLocation)"
    $ico = ($l.IconLocation -split ',')[0]
    "icon file : $(if (Test-Path -LiteralPath $ico) { 'exists, ' + (Get-Item -LiteralPath $ico).Length + ' bytes' } else { 'MISSING' })"
    Remove-Item $t
    (Get-Item -LiteralPath $_.FullName).LastWriteTime = Get-Date      # a changed timestamp makes the shell re-read it
}
# other copies of the shortcut (all-users Start menu, desktop) that could be the one the user sees
foreach ($d in @("$env:ProgramData\Microsoft\Windows\Start Menu\Programs", [Environment]::GetFolderPath('Desktop'))) {
    Get-ChildItem $d -Filter *.lnk -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'Ashen' } | ForEach-Object { "other copy: $($_.FullName -replace '[^\x20-\x7E]', '?')" }
}
Add-Type -Namespace W -Name S -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, System.IntPtr a, System.IntPtr b);'
[W.S]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)   # SHCNE_ASSOCCHANGED: shell drops cached icons
& "$env:SystemRoot\System32\ie4uinit.exe" -show
"icon cache refresh requested"
