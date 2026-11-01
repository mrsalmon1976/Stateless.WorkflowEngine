<#
.SYNOPSIS
    Starts the Stateless.WorkflowEngine development environment in Docker.

.DESCRIPTION
    Starts MongoDB (localhost:27017), RavenDB (http://localhost:8080) and the web console
    (http://localhost:8811) using docker-compose.yml.  The Example app and the MongoDb/RavenDb test projects
    point at these containers by default (override with the STATELESS_MONGODB_CONNECTIONSTRING and
    STATELESS_RAVENDB_URL environment variables).

    On first run, a connection to the MongoDB container's example database is added to the console.

    With -Dev, the console container is stopped and the console runs from source instead, with changes
    picked up live: the API under 'dotnet watch' (http://localhost:8811) and the client under the Vite dev
    server (http://localhost:5173, which proxies /api to the API).  Each runs in its own window - close the
    windows to stop them.  The local console keeps its own users/connections in the WebConsole project's
    Data folder, separate from the container's.

.EXAMPLE
    .\start.ps1             # start everything, rebuilding the console image if the source has changed
    .\start.ps1 -NoBuild    # start without rebuilding the console image
    .\start.ps1 -Dev        # start the databases in Docker and run the console from source with live reload
    .\start.ps1 -Stop       # stop the containers (data volumes are kept)
#>
param(
    [switch]$NoBuild,
    [switch]$Dev,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$composeFile = "$root\docker-compose.yml"
$consoleUrl = "http://localhost:8811"
$devClientUrl = "http://localhost:5173"
$ravenUrl = "http://localhost:8080"
$consoleProject = "$root\source\Stateless.WorkflowEngine.WebConsole"
$clientProject = "$root\source\Stateless.WorkflowEngine.WebConsole.Client"

function IsListening {
    param([int]$port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect("localhost", $port)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function StartInWindow {
    param([string]$title, [string]$workingDirectory, [string]$command)
    $script = "`$Host.UI.RawUI.WindowTitle = '$title'; $command"
    Start-Process powershell -WorkingDirectory $workingDirectory -ArgumentList "-NoExit", "-Command", $script | Out-Null
}

function IsDockerRunning {
    # in Windows PowerShell, redirected stderr from a native command becomes an error record, which would
    # terminate the script under ErrorActionPreference=Stop when the daemon isn't running
    $ErrorActionPreference = "Continue"
    docker info >$null 2>&1
    return ($LASTEXITCODE -eq 0)
}

function WaitFor {
    param([string]$name, [scriptblock]$test, [int]$timeoutSeconds = 120)

    # see IsDockerRunning - failures while a service is starting are expected, so don't let them terminate
    $ErrorActionPreference = "Continue"
    Write-Host "Waiting for $name..." -ForegroundColor Gray
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            if (& $test) {
                Write-Host "$name is ready." -ForegroundColor Green
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    Write-Host "$name did not start within $timeoutSeconds seconds - check 'docker compose logs'." -ForegroundColor Red
    exit 1
}

function AddExampleConnection {
    param([string]$mongoHost)

    # signs in with the default admin account - if the password has been changed, add connections manually
    try {
        $login = @{ userName = "admin"; password = "admin" } | ConvertTo-Json
        Invoke-RestMethod -Uri "$consoleUrl/api/auth/login" -Method Post -Body $login -ContentType "application/json" -SessionVariable session | Out-Null
    }
    catch {
        Write-Host "Skipping example connection - unable to sign in to the console as admin/admin." -ForegroundColor Yellow
        return
    }

    $connections = Invoke-RestMethod -Uri "$consoleUrl/api/connections" -WebSession $session
    if ($connections | Where-Object { $_.host -eq $mongoHost -and $_.database -eq "StatelessWorkflowExample" }) {
        return
    }

    # inside the docker network the console reaches MongoDB by its service name, from source it is localhost
    $connection = @{
        workflowStoreType   = "MongoDb"
        host                = $mongoHost
        port                = 27017
        database            = "StatelessWorkflowExample"
        activeCollection    = "Workflows"
        completedCollection = "CompletedWorkflows"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$consoleUrl/api/connections" -Method Post -Body $connection -ContentType "application/json" -WebSession $session | Out-Null
    Write-Host "Added console connection to ${mongoHost}:27017/StatelessWorkflowExample." -ForegroundColor Green
}

# 1. Ensure Docker is running
Write-Host "Checking if Docker is running..." -ForegroundColor Cyan
if (-not (IsDockerRunning)) {
    Write-Host "Docker is not running. Starting Docker Desktop..." -ForegroundColor Yellow
    & 'C:\Program Files\Docker\Docker\Docker Desktop.exe'

    # Wait for Docker to start (up to 2 minutes)
    $maxRetries = 24
    $retryCount = 0
    while ($retryCount -lt $maxRetries) {
        if (IsDockerRunning) {
            Write-Host "Docker is now running." -ForegroundColor Green
            break
        }
        Write-Host "Waiting for Docker daemon... ($($retryCount + 1)/$maxRetries)" -ForegroundColor Gray
        Start-Sleep -Seconds 5
        $retryCount++
    }

    if ($retryCount -eq $maxRetries) {
        Write-Host "Docker failed to start in a timely manner. Exiting." -ForegroundColor Red
        exit 1
    }
}

if ($Stop) {
    Write-Host "Stopping Docker services..." -ForegroundColor Cyan
    docker compose -f $composeFile stop
    exit $LASTEXITCODE
}

# 2. Start Docker services
Write-Host "Starting Docker services..." -ForegroundColor Cyan
if ($Dev) {
    # the console runs from source on the same port, so the container must not be running
    docker compose -f $composeFile stop webconsole
    $composeArgs = @("compose", "-f", $composeFile, "up", "-d", "mongodb", "ravendb")
}
else {
    $composeArgs = @("compose", "-f", $composeFile, "up", "-d")
    if (-not $NoBuild) {
        $composeArgs += "--build"
    }
}
& docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker compose failed. Exiting." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Wait for the services to accept requests
WaitFor -name "MongoDB" -test { (docker exec stateless_mongodb mongosh --quiet --eval "db.runCommand({ ping: 1 }).ok" 2>$null) -eq "1" }
WaitFor -name "RavenDB" -test { (Invoke-WebRequest -Uri "$ravenUrl/build/version" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop).StatusCode -eq 200 }

if ($Dev) {
    # skip anything already running from a previous -Dev run
    if (IsListening 8811) {
        Write-Host "Something is already listening on port 8811 - assuming it is the console API." -ForegroundColor Yellow
    }
    else {
        # --non-interactive restarts the app automatically when a change can't be hot reloaded
        StartInWindow -title "Web console API (dotnet watch)" -workingDirectory $consoleProject -command "dotnet watch run --non-interactive"
    }
    if (IsListening 5173) {
        Write-Host "Something is already listening on port 5173 - assuming it is the Vite dev server." -ForegroundColor Yellow
    }
    else {
        $npmCommand = if (Test-Path "$clientProject\node_modules") { "npm run dev" } else { "npm install; npm run dev" }
        StartInWindow -title "Web console client (vite)" -workingDirectory $clientProject -command $npmCommand
    }

    # the first build can take a while
    WaitFor -name "Web console API" -timeoutSeconds 300 -test { IsListening 8811 }
    WaitFor -name "Vite dev server" -timeoutSeconds 300 -test { IsListening 5173 }
    AddExampleConnection -mongoHost "localhost"
}
else {
    WaitFor -name "Web console" -test { (Invoke-WebRequest -Uri $consoleUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop).StatusCode -eq 200 }

    # 4. Point the console at the example database
    AddExampleConnection -mongoHost "mongodb"
}

Write-Host ""
Write-Host "Environment ready:" -ForegroundColor Cyan
if ($Dev) {
    Write-Host "  Web console : $devClientUrl (live reload - admin/admin on first run)"
    Write-Host "  Console API : $consoleUrl (dotnet watch)"
}
else {
    Write-Host "  Web console : $consoleUrl (admin/admin on first run)"
}
Write-Host "  MongoDB     : mongodb://localhost:27017"
Write-Host "  RavenDB     : $ravenUrl (studio)"
Write-Host ""
Write-Host "Seed workflows with the Example app:  dotnet run --project source\Example" -ForegroundColor Gray
Write-Host "Run the store tests (they use these containers) from Visual Studio or 'dotnet test'." -ForegroundColor Gray
if ($Dev) {
    Write-Host "Stop the console by closing its two windows, and the containers with:  .\start.ps1 -Stop" -ForegroundColor Gray
    Start-Process $devClientUrl
}
else {
    Write-Host "Stop everything with:  .\start.ps1 -Stop" -ForegroundColor Gray
    Start-Process $consoleUrl
}
