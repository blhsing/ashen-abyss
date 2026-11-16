# Show the machine's total network throughput over N seconds, and who has the most connections.
param([int]$Seconds = 10)
$a = Get-NetAdapterStatistics | Where-Object { $_.ReceivedBytes -gt 0 }
Start-Sleep -Seconds $Seconds
$b = Get-NetAdapterStatistics | Where-Object { $_.ReceivedBytes -gt 0 }
foreach ($x in $b) {
    $y = $a | Where-Object Name -eq $x.Name
    if ($y) { "{0,-28} down {1,8:N0} kB/s   up {2,8:N0} kB/s" -f $x.Name, (($x.ReceivedBytes - $y.ReceivedBytes) / 1024 / $Seconds), (($x.SentBytes - $y.SentBytes) / 1024 / $Seconds) }
}
Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue | Group-Object OwningProcess | Sort-Object Count -Descending |
    Select-Object -First 8 | ForEach-Object {
        $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Name)" -ErrorAction SilentlyContinue
        $cl = if ($p -and $p.CommandLine) { $p.CommandLine.Substring(0, [Math]::Min(150, $p.CommandLine.Length)) } else { '' }
        "{0,3} conns  pid {1,6}  {2}" -f $_.Count, $_.Name, $cl
    }
