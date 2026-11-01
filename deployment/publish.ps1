$ErrorActionPreference = "Stop"
Clear-Host
$root = $PSScriptRoot
$sourceFolder = [System.IO.Path]::Combine($root.Replace("deployment", ""), "source")
$packageFolder = "$root\publish\nuget"
$packages = @("Stateless.WorkflowEngine", "Stateless.WorkflowEngine.MongoDb", "Stateless.WorkflowEngine.RavenDb")

# read the api key from a file that is not published with the repository
$apiKey = [System.IO.File]::ReadAllText("$root\_apikey.txt").Trim()

# the version is set in the project files by build.ps1 - all packages must be on the same version, as each store
# package depends on the matching version of Stateless.WorkflowEngine
$version = $null
foreach ($package in $packages) {
	$projectVersion = ([xml](Get-Content "$sourceFolder\$package\$package.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
	if ($null -eq $version) { $version = $projectVersion }
	if ($projectVersion -ne $version) {
		Write-Host "$package is version $projectVersion but expected $version - run build.ps1 first" -ForegroundColor White -BackgroundColor Red
		Exit 1
	}
}
# the packages must be built from a commit that is on GitHub - source link in the symbols points at that commit
$repoRoot = $root.Replace("\deployment", "")
$uncommitted = & git -C $repoRoot status --porcelain
if ($uncommitted) {
	Write-Host "There are uncommitted changes - commit and push them before publishing:" -ForegroundColor White -BackgroundColor Red
	$uncommitted | ForEach-Object { Write-Host "  $_" }
	Exit 1
}
& git -C $repoRoot fetch --quiet origin
if ($LASTEXITCODE -ne 0) {
	Write-Host "Unable to fetch from origin to check the commit has been pushed" -ForegroundColor White -BackgroundColor Red
	Exit 1
}
$pushedTo = & git -C $repoRoot branch --remotes --contains HEAD
if (!$pushedTo) {
	Write-Host "Commit $(& git -C $repoRoot rev-parse --short HEAD) has not been pushed to origin - push before publishing" -ForegroundColor White -BackgroundColor Red
	Exit 1
}

if ((Read-Host -Prompt "Publishing version $version - continue? [Y/y]").ToLower() -ne "y") {
	Exit
}
$isProduction = ((Read-Host -Prompt "Is this a production push?  [Y/y]").ToLower() -eq "y")

# pack into a clean folder so a stale package can never be pushed - ContinuousIntegrationBuild makes the build
# deterministic (no local paths in the symbols, so source link maps to GitHub)
if (Test-Path -Path $packageFolder) { Remove-Item $packageFolder -Recurse -Force }
foreach ($package in $packages) {
	& dotnet pack "$sourceFolder\$package\$package.csproj" --configuration Release --output $packageFolder -p:ContinuousIntegrationBuild=true
	$nupkg = "$packageFolder\$package.$version.nupkg"
	if ($LASTEXITCODE -ne 0 -or !(Test-Path -Path $nupkg)) {
		Write-Host "$package nuget package not found at $nupkg" -ForegroundColor White -BackgroundColor Red
		Exit 1
	}
}

# now we push! (dotnet nuget push also pushes the matching .snupkg symbols package)
foreach ($package in $packages) {
	$nupkg = "$packageFolder\$package.$version.nupkg"
	if ($isProduction) {
		& dotnet nuget push $nupkg --source https://api.nuget.org/v3/index.json --api-key $apiKey
	}
	else {
		# pushing to a folder creates a local feed for the NugetTest app
		New-Item -ItemType Directory -Force -Path "$root\NugetTest\NugetPackageTest" | Out-Null
		& dotnet nuget push $nupkg --source "$root\NugetTest\NugetPackageTest"
	}
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Failed to push $nupkg" -ForegroundColor White -BackgroundColor Red
		Exit 1
	}
}

Write-Host "Done" -BackgroundColor Green -ForegroundColor White
