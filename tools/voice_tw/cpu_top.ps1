# Top CPU consumers over a short interval (percent of one core) and total load.
param([int]$Seconds = 4)
$a = Get-Process | Select-Object Id, ProcessName, CPU
Start-Sleep -Seconds $Seconds
$b = Get-Process | Select-Object Id, ProcessName, CPU
$rows = foreach ($p in $b) { $q = $a | Where-Object Id -eq $p.Id; if ($q -and $p.CPU -ne $null) { [pscustomobject]@{ Id = $p.Id; Name = $p.ProcessName; Cores = [math]::Round(($p.CPU - $q.CPU) / $Seconds, 2) } } }
$rows | Sort-Object Cores -Descending | Select-Object -First 10 | ForEach-Object { "{0,6} {1,-28} {2,6} cores" -f $_.Id, $_.Name, $_.Cores }
"total: {0:N1} of {1} logical cores busy" -f (($rows | Measure-Object Cores -Sum).Sum), [Environment]::ProcessorCount
