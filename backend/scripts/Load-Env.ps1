<#
Loads the root .env file into the current PowerShell process and maps it to the
configuration keys expected by the .NET services and the EF Core design-time tools.
Usage from the solution root:  . .\scripts\Load-Env.ps1
Keep the same PowerShell session open for `dotnet run` / `dotnet ef`.
#>
$ErrorActionPreference = 'Stop'
$envFile = Join-Path (Get-Location) '.env'

if (-not (Test-Path $envFile)) {
    throw "Could not find '$envFile'. Copy .env.example to .env and fill it in first."
}

Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if (-not $line -or $line.StartsWith('#')) { return }

    $separator = $line.IndexOf('=')
    if ($separator -lt 1) { return }

    $key = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1).Trim()
    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'"))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    [Environment]::SetEnvironmentVariable($key, $value, 'Process')
}

function Assert-Value([string]$name, [int]$minLength = 1) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value) -or ($value -like 'CHANGE_ME*' -or $value -like 'GENERATE_*' -or $value -like 'REPLACE_*') -or $value.Length -lt $minLength) {
        throw "Set $name in the root .env file (see .env.example)."
    }
}

Assert-Value 'POSTGRES_PASSWORD'
Assert-Value 'JWT_SECRET_KEY' 32
Assert-Value 'INTERNAL_TOURNAMENT_KEY' 32

$hostName = if ($env:POSTGRES_HOST) { $env:POSTGRES_HOST } else { 'localhost' }
$port = if ($env:POSTGRES_PORT) { $env:POSTGRES_PORT } else { '5432' }
$user = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'chessplatform_app' }

function New-PostgresConnectionString([string]$variableName) {
    $database = [Environment]::GetEnvironmentVariable($variableName, 'Process')
    if ([string]::IsNullOrWhiteSpace($database)) { throw "Set $variableName in the root .env file." }
    return "Host=$hostName;Port=$port;Database=$database;Username=$user;Password=$($env:POSTGRES_PASSWORD)"
}

# Database connection strings (also used by `dotnet ef` and Puzzle.Importer)
$env:ConnectionStrings__IdentityDatabase   = New-PostgresConnectionString 'IDENTITY_DATABASE_NAME'
$env:ConnectionStrings__GameDatabase       = New-PostgresConnectionString 'GAME_DATABASE_NAME'
$env:ConnectionStrings__PuzzleDatabase     = New-PostgresConnectionString 'PUZZLE_DATABASE_NAME'
$env:ConnectionStrings__RatingDatabase     = New-PostgresConnectionString 'RATING_DATABASE_NAME'
$env:ConnectionStrings__TournamentDatabase = New-PostgresConnectionString 'TOURNAMENT_DATABASE_NAME'

# Secrets shared by the services
$env:Jwt__SecretKey                  = $env:JWT_SECRET_KEY
$env:InternalServices__TournamentKey = $env:INTERNAL_TOURNAMENT_KEY
$env:InternalServices__ServiceKey = $env:INTERNAL_TOURNAMENT_KEY
if ($env:IDENTITY_API_BASE_URL) { $env:IdentityApi__BaseUrl = $env:IDENTITY_API_BASE_URL }
if ($env:STOCKFISH_PATH) { $env:Engine__Path = $env:STOCKFISH_PATH }

# Optional: SignalR Redis backplane (required for multi-instance Realtime deployments)
if ($env:SIGNALR_REDIS_CONNECTION_STRING) { $env:SignalR__Redis__ConnectionString = $env:SIGNALR_REDIS_CONNECTION_STRING }
if ($env:SIGNALR_REDIS_CHANNEL_PREFIX) { $env:SignalR__Redis__ChannelPrefix = $env:SIGNALR_REDIS_CHANNEL_PREFIX }

# Optional: real password-reset emails
if ($env:BREVO_API_KEY)      { $env:Brevo__ApiKey      = $env:BREVO_API_KEY }
if ($env:BREVO_SENDER_EMAIL) { $env:Brevo__SenderEmail = $env:BREVO_SENDER_EMAIL }

Write-Host 'Loaded local ChessPlatform environment variables for this PowerShell session.' -ForegroundColor Green
Write-Host 'Secrets are not printed. Keep this session open for dotnet run / dotnet ef commands.'
