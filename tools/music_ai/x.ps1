# Run any command inside the music_ai environment:  x.ps1 <exe> <args...>
. "$PSScriptRoot\env.ps1"
$cmd = $args[0]
$rest = @()
if ($args.Length -gt 1) { $rest = $args[1..($args.Length - 1)] }
& $cmd @rest
exit $LASTEXITCODE
