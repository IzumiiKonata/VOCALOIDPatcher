param([Parameter(Mandatory)][string]$Tag, [int]$Seconds = 30)

$proc = Get-Process VOCALOID7 -ErrorAction Stop
$dir = Join-Path $env:USERPROFILE 'RiderSnapshots\drag-ab'
if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
$out = Join-Path $dir "drag-$Tag.dtp"

Write-Host "Attaching to PID $($proc.Id), profiling $Seconds seconds. Start dragging the track height NOW and keep dragging until it finishes."
dottrace attach $proc.Id --profiling-type=Sampling --timeout="${Seconds}s" --save-to="$out"
Write-Host "Saved: $out"
