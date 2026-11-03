<#
.SYNOPSIS
    Benchmarks each release in benchmarks\releases against the current branch.

.DESCRIPTION
    Builds a benchmark agent for every Stateless.WorkflowEngine.MongoDb_v<version>.zip release and for the current
    branch, runs each scenario (1000 workflows, nodes x concurrency: 1x1, 1x4, 2x2, 1x10, 2x10, 5x10) three
    times against MongoDB, and writes benchmarks\reports\benchmark-YYYYMMDD-HHMM.md (plus a .json with the raw results).

    MongoDB must be running - start it with .\start.ps1.  The StatelessBenchmark database is dropped before every run.
    Any other arguments are passed to the runner (see --help).

.EXAMPLE
    .\benchmarks\run-benchmarks.ps1
    .\benchmarks\run-benchmarks.ps1 --versions 5.1.0,current --scenarios 1,2
    .\benchmarks\run-benchmarks.ps1 --count 200 --iterations 1
#>
$ErrorActionPreference = "Stop"
$runner = "$PSScriptRoot\src\Benchmark.Runner\Benchmark.Runner.csproj"

$client = New-Object System.Net.Sockets.TcpClient
try {
    $client.Connect("localhost", 27017)
}
catch {
    if (-not $env:STATELESS_MONGODB_CONNECTIONSTRING) {
        throw "MongoDB is not running on localhost:27017 - run .\start.ps1 first"
    }
}
finally {
    $client.Dispose()
}

dotnet build $runner -c Release -v q -nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed"
}
& dotnet run --project $runner -c Release --no-build -- --root $PSScriptRoot @args
exit $LASTEXITCODE
