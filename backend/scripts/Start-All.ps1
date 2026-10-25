[CmdletBinding()]
param(
    [switch]$SkipFrontend,
    [switch]$SkipDependencies,
    [switch]$Lan
)

$ErrorActionPreference = 'Stop'
$backendRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$workspaceRoot = (Resolve-Path (Join-Path $backendRoot '..')).Path
$frontendRoot = Join-Path $workspaceRoot 'frontend'
$stateRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'ChessPlatform'
$logRoot = Join-Path $stateRoot 'logs'
$stateFile = Join-Path $stateRoot 'run-state.json'
$lanAddress = $null
if ($Lan) {
    $defaultRoute = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
        Sort-Object RouteMetric | Select-Object -First 1
    if ($defaultRoute) {
        $lanAddress = Get-NetIPAddress -InterfaceIndex $defaultRoute.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
            Select-Object -First 1 -ExpandProperty IPAddress
    }
    if (-not $lanAddress) { throw 'Could not determine this computer''s LAN IPv4 address. Connect to your Wi-Fi/Ethernet network and retry.' }
}

$services = @(
    @{ Name = 'Identity'; Project = 'src/Services/Identity/Identity.Api/Identity.Api.csproj'; Assembly = 'Identity.Api.dll'; Urls = 'https://localhost:7052;http://localhost:5170' },
    @{ Name = 'Game'; Project = 'src/Services/Game/Game.Api/Game.Api.csproj'; Assembly = 'Game.Api.dll'; Urls = 'https://localhost:7051;http://localhost:5230' },
    @{ Name = 'Realtime'; Project = 'src/Services/Realtime/Realtime/Realtime.csproj'; Assembly = 'Realtime.dll'; Urls = 'https://localhost:7091;http://localhost:5293' },
    @{ Name = 'Puzzle'; Project = 'src/Services/Puzzle/Puzzle.Api/Puzzle.Api.csproj'; Assembly = 'Puzzle.Api.dll'; Urls = 'https://localhost:7220;http://localhost:5103' },
    @{ Name = 'Rating'; Project = 'src/Services/Rating/Rating.Api/Rating.Api.csproj'; Assembly = 'Rating.Api.dll'; Urls = 'https://localhost:7049;http://localhost:5134' },
    @{ Name = 'Analysis'; Project = 'src/Services/Analysis/Analysis.Api/Analysis.Api.csproj'; Assembly = 'Analysis.Api.dll'; Urls = 'https://localhost:7222;http://localhost:5105' },
    @{ Name = 'Tournament'; Project = 'src/Services/Tournament/Tournament.Api/Tournament.Api.csproj'; Assembly = 'Tournament.Api.dll'; Urls = 'https://localhost:7221;http://localhost:5104' },
    @{ Name = 'Gateway'; Project = 'src/Gateway/Gateway/Gateway.csproj'; Assembly = 'Gateway.dll'; Urls = 'http://localhost:5272' }
)

if (Test-Path $stateFile) {
    $oldState = Get-Content $stateFile -Raw | ConvertFrom-Json
    $running = @($oldState.Processes | Where-Object { Get-Process -Id $_.Pid -ErrorAction SilentlyContinue })
    if ($running.Count -gt 0) {
        $names = ($running | ForEach-Object { $_.Name }) -join ', '
        throw "ChessPlatform processes already appear to be running ($names). Stop them with .\backend\scripts\Stop-All.ps1 first."
    }
}

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$started = [System.Collections.Generic.List[object]]::new()
$oldAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$oldUrls = $env:ASPNETCORE_URLS
$oldCorsOrigin = $env:Cors__AllowedOrigins__1
$oldViteApiBaseUrl = $env:VITE_API_BASE_URL

try {
    Push-Location $backendRoot
    . .\scripts\Load-Env.ps1

    if (-not $SkipDependencies) {
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
            throw 'Docker CLI was not found. Install/start Docker Desktop, or pass -SkipDependencies if Kafka and Redis are already running.'
        }
        $dockerVersion = & docker info --format '{{.ServerVersion}}' 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($dockerVersion -join '').Trim())) {
            throw 'Docker Desktop is installed, but its Linux container engine is not running. Start Docker Desktop, wait until it says it is running, then retry. Verify with: docker info'
        }
        $allContainers = @(& docker ps --all --format '{{.Names}}')
        if ($LASTEXITCODE -ne 0) { throw 'Could not list Docker containers. Check Docker Desktop and retry.' }
        $runningContainers = @(& docker ps --format '{{.Names}}')
        if ($LASTEXITCODE -ne 0) { throw 'Could not check running Docker containers. Check Docker Desktop and retry.' }

        $missingServices = [System.Collections.Generic.List[string]]::new()
        foreach ($containerName in @('chessplatform-redis', 'chessplatform-kafka')) {
            if ($allContainers -contains $containerName) {
                if ($runningContainers -contains $containerName) {
                    Write-Host "Reusing running dependency container: $containerName"
                }
                else {
                    & docker start $containerName | Out-Null
                    if ($LASTEXITCODE -ne 0) { throw "Could not start existing Docker container '$containerName'." }
                    Write-Host "Started existing dependency container: $containerName"
                }
            }
            else {
                $serviceName = if ($containerName -eq 'chessplatform-redis') { 'redis' } else { 'kafka' }
                $missingServices.Add($serviceName)
            }
        }
        if ($missingServices.Count -gt 0) {
            & docker compose -f docker-compose.kafka.yml up -d @($missingServices)
            if ($LASTEXITCODE -ne 0) { throw 'Docker is running, but missing Kafka/Redis containers could not be created.' }
        }
    }

    $postgresHost = if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { 'localhost' }
    $postgresPort = if ($env:POSTGRES_PORT) { [int]$env:POSTGRES_PORT } else { 5432 }
    if (-not (Test-NetConnection -ComputerName $postgresHost -Port $postgresPort -InformationLevel Quiet)) {
        throw "PostgreSQL is not reachable at ${postgresHost}:$postgresPort. Start PostgreSQL before launching the services."
    }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'

    Write-Host 'Building the eight API projects (no tests are run)...' -ForegroundColor Cyan
    foreach ($service in $services) {
        & dotnet build $service.Project --configuration Debug --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $($service.Name)." }
    }

    foreach ($service in $services) {
        $projectPath = Join-Path $backendRoot $service.Project
        $projectDirectory = Split-Path $projectPath -Parent
        $assemblyPath = Join-Path (Join-Path $projectDirectory 'bin/Debug/net10.0') $service.Assembly
        if (-not (Test-Path $assemblyPath)) { throw "Build output not found: $assemblyPath" }

        $serviceUrls = $service.Urls
        if ($Lan -and $service.Name -eq 'Gateway') {
            $serviceUrls = 'http://0.0.0.0:5272'
            $env:Cors__AllowedOrigins__1 = "http://${lanAddress}:3000"
        }
        $env:ASPNETCORE_URLS = $serviceUrls
        $process = Start-Process -FilePath 'dotnet' `
            -ArgumentList @($assemblyPath) `
            -WorkingDirectory $projectDirectory `
            -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logRoot "$($service.Name).out.log") `
            -RedirectStandardError (Join-Path $logRoot "$($service.Name).err.log")
        $started.Add([pscustomobject]@{ Name = $service.Name; Pid = $process.Id })
        Write-Host ("Started {0} (PID {1})" -f $service.Name, $process.Id)
        if ($Lan -and $service.Name -eq 'Gateway') { $env:Cors__AllowedOrigins__1 = $oldCorsOrigin }
    }

    if (-not $SkipFrontend) {
        $nodeModules = Join-Path $frontendRoot 'node_modules'
        $viteEntry = Join-Path $nodeModules 'vite/bin/vite.js'
        $node = Get-Command node -ErrorAction SilentlyContinue
        if (-not $node -or -not (Test-Path $viteEntry)) {
            throw 'Frontend dependencies or Node.js are missing. Run `npm install` in frontend, or pass -SkipFrontend.'
        }
        $viteHost = if ($Lan) { '0.0.0.0' } else { 'localhost' }
        if ($Lan) { $env:VITE_API_BASE_URL = "http://${lanAddress}:5272" }
        $process = Start-Process -FilePath $node.Source `
            -ArgumentList @($viteEntry, '--host', $viteHost) `
            -WorkingDirectory $frontendRoot `
            -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logRoot 'Frontend.out.log') `
            -RedirectStandardError (Join-Path $logRoot 'Frontend.err.log')
        $started.Add([pscustomobject]@{ Name = 'Frontend'; Pid = $process.Id })
        Write-Host ("Started Frontend (PID {0})" -f $process.Id)
        $env:VITE_API_BASE_URL = $oldViteApiBaseUrl
    }

    [pscustomobject]@{ Processes = @($started); StartedAt = (Get-Date).ToString('o') } |
        ConvertTo-Json -Depth 4 | Set-Content -Path $stateFile -Encoding UTF8

    if ($Lan) {
        Write-Host "`nAll requested processes were launched for LAN testing. Frontend: http://${lanAddress}:3000 | Gateway: http://${lanAddress}:5272" -ForegroundColor Green
        Write-Host 'Share the Frontend URL with people on the same Wi-Fi/LAN. Allow ports 3000 and 5272 through Windows Firewall for Private networks if prompted.'
    }
    else {
        Write-Host "`nAll requested processes were launched. Frontend: http://localhost:3000 | Gateway: http://localhost:5272" -ForegroundColor Green
    }
    Write-Host "Logs: $logRoot"
    Write-Host 'Stop launched processes with: .\backend\scripts\Stop-All.ps1'
    $stockfishPath = if ($env:Engine__Path) { $env:Engine__Path } else { 'stockfish' }
    $stockfishAvailable = (Test-Path -LiteralPath $stockfishPath -PathType Leaf) -or
        ($null -ne (Get-Command $stockfishPath -ErrorAction SilentlyContinue))
    if (-not $stockfishAvailable) {
        Write-Warning "Stockfish was not found at '$stockfishPath'. Set STOCKFISH_PATH in backend\.env; Analysis starts, but engine-backed analysis may not work until configured."
    }
}
catch {
    foreach ($entry in $started) { Stop-Process -Id $entry.Pid -Force -ErrorAction SilentlyContinue }
    throw
}
finally {
    Pop-Location
    $env:ASPNETCORE_ENVIRONMENT = $oldAspNetEnvironment
    $env:DOTNET_ENVIRONMENT = $oldDotnetEnvironment
    $env:ASPNETCORE_URLS = $oldUrls
    $env:Cors__AllowedOrigins__1 = $oldCorsOrigin
    $env:VITE_API_BASE_URL = $oldViteApiBaseUrl
}
