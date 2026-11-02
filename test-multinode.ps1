<#
.SYNOPSIS
    Runs the multi-node stress test against MongoDB and/or RavenDB.

.DESCRIPTION
    For each store, the script:
      1. resets the store, its execution ledger and work folder, then seeds the workflows
      2. starts several normal nodes (alternating sync and async) and one node that is killed part way through, each
         in its own window
      3. restarts the killed node once it has died, and waits for all nodes to finish (nodes exit once the store is empty)
      4. runs the report, which checks the execution ledger to prove that no workflow step was executed by two nodes
         at the same time, that every workflow completed, and that nothing was left locked

    MongoDB and RavenDB must be running - start them with .\start.ps1.  The execution ledger is always stored in
    MongoDB.  Node logs and the report summaries are written to the logs folder.

.EXAMPLE
    .\test-multinode.ps1                          # both stores, 2000 workflows, 3 nodes plus a node that is killed
    .\test-multinode.ps1 -Store mongo -Count 500  # MongoDB only
    .\test-multinode.ps1 -Nodes 5 -Hidden         # 5 nodes, without opening a window per node
#>
param(
    [ValidateSet("all", "mongo", "raven")]
    [string]$Store = "all",
    [int]$Count = 2000,
    [int]$Nodes = 3,
    [int]$Concurrency = 10,
    [int]$CrashAfter = 20,
    [int]$NodeTimeout = 20,
    [double]$ErrorRate = 0.02,
    [int]$TimeoutMinutes = 30,
    [switch]$Hidden
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = "$root\source\Example\Example.csproj"
$dll = "$root\source\Example\bin\Release\net10.0\Example.dll"
$invariant = [Globalization.CultureInfo]::InvariantCulture

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

function Invoke-Example {
    param([string[]]$arguments)
    & dotnet $dll multinode @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Example multinode $($arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Start-Node {
    param([string]$storeName, [string]$name, [string[]]$extra)
    $nodeArgs = @($dll, "multinode", "node", "--store", $storeName, "--name", $name,
        "--concurrency", $Concurrency, "--node-timeout", $NodeTimeout,
        "--error-rate", $ErrorRate.ToString($invariant), "--max-minutes", $TimeoutMinutes)
    if ($extra) {
        $nodeArgs += $extra
    }
    $windowStyle = if ($Hidden) { "Hidden" } else { "Normal" }
    return Start-Process dotnet -ArgumentList $nodeArgs -WorkingDirectory $root -WindowStyle $windowStyle -PassThru
}

$stores = if ($Store -eq "all") { @("mongo", "raven") } else { @($Store) }

if (-not (IsListening 27017)) {
    throw "MongoDB is not running on localhost:27017 (it holds the execution ledger) - run .\start.ps1 first"
}
if ($stores -contains "raven" -and -not (IsListening 8080)) {
    throw "RavenDB is not running on localhost:8080 - run .\start.ps1 first"
}

Write-Host "Building the Example project..." -ForegroundColor Cyan
dotnet build $project -c Release -v q -nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed"
}

$results = @()
foreach ($s in $stores) {
    Write-Host ""
    Write-Host "=== $s : $Count workflows, $Nodes nodes + 1 node killed after $($CrashAfter)s ===" -ForegroundColor Cyan
    $started = Get-Date

    Invoke-Example @("reset", "--store", $s)
    Invoke-Example @("seed", "--store", $s, "--count", $Count)

    $processes = @()
    for ($i = 1; $i -le $Nodes; $i++) {
        $extra = if ($i % 2 -eq 0) { @("--async") } else { @() }
        $processes += Start-Node $s "node$i" $extra
    }
    $crashNode = Start-Node $s "crash" @("--crash-after", $CrashAfter)

    $crashNode.WaitForExit()
    Write-Host "The crash node was killed - its workflows are released after the node timeout ($($NodeTimeout)s).  Restarting it..." -ForegroundColor Yellow
    $processes += Start-Node $s "crash-restarted" @("--async")

    $deadline = $started.AddMinutes($TimeoutMinutes + 1)
    $timedOut = $false
    foreach ($process in $processes) {
        $remaining = [Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds)
        if (-not $process.WaitForExit([int]$remaining)) {
            $timedOut = $true
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    if ($timedOut) {
        Write-Host "Nodes did not finish within $TimeoutMinutes minutes and were stopped" -ForegroundColor Red
    }
    Write-Host "All nodes finished in $([int]((Get-Date) - $started).TotalSeconds)s" -ForegroundColor Cyan

    & dotnet $dll multinode report --store $s
    $summary = Get-Content "$root\logs\multinode-report-$s.json" -Raw | ConvertFrom-Json
    if ($timedOut) {
        $summary.Result = "FAIL"
    }
    $results += $summary
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
$results | Format-Table Store, Result, Seeded, Completed, Active, Suspended, LockedByNodes, StepExecutions, Overlaps, DuplicateSuccesses, Unfinished, ExpectedReexecutions -AutoSize | Out-String | Write-Host

if ($results | Where-Object { $_.Result -ne "PASS" }) {
    Write-Host "FAIL" -ForegroundColor Red
    exit 1
}
Write-Host "PASS" -ForegroundColor Green
exit 0
