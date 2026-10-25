[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$stateFile = Join-Path (Join-Path ([System.IO.Path]::GetTempPath()) 'ChessPlatform') 'run-state.json'
if (-not (Test-Path $stateFile)) {
    Write-Host 'No ChessPlatform process state file was found.'
    return
}

$state = Get-Content $stateFile -Raw | ConvertFrom-Json
foreach ($entry in $state.Processes) {
    $process = Get-Process -Id $entry.Pid -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Id $entry.Pid -Force
        Write-Host "Stopped $($entry.Name) (PID $($entry.Pid))."
    }
}
Remove-Item -LiteralPath $stateFile -Force
Write-Host 'Kafka and Redis containers were left running. Stop them separately with docker compose -f backend/docker-compose.kafka.yml down.'
