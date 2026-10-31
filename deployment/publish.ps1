Clear-Host
$root = $PSScriptRoot
$sourceFolder = [System.IO.Path]::Combine($root.Replace("deployment", ""), "source") 
$statelessWorkflowFolder = "$sourceFolder\Stateless.WorkflowEngine"
$statelessWorkflowMongoDbFolder = "$sourceFolder\Stateless.WorkflowEngine.MongoDb"

# read the api key from a file that is not published with the repository
$apiKey = [System.IO.File]::ReadAllText("$root\_apikey.txt").Trim()

$version = Read-Host -Prompt "What version are we publishing (include prerelease suffix) [e.g. 2.3.0 or 0.0.2-alpha]"
$isProduction = ((Read-Host -Prompt "Is this a production push?  [Y/y]").ToLower() -eq "y")

# Stateless.WorkflowEngine
Set-Location $statelessWorkflowFolder
dotnet pack --configuration Release

# Stateless.WorkflowEngine.MongoDb
Set-Location $statelessWorkflowMongoDbFolder
dotnet pack --configuration Release

$statelessWorkflowNupkg = "$statelessWorkflowFolder\bin\Release\Stateless.WorkflowEngine.$version.nupkg"
$statelessWorkflowMongoDbNupkg = "$statelessWorkflowMongoDbFolder\bin\Release\Stateless.WorkflowEngine.MongoDb.$version.nupkg"

# make sure the packages have been built
if (!(Test-Path -Path $statelessWorkflowNupkg)) {
	Write-Host "Stateless.WorkflowEngine nuget package not found at $statelessWorkflowNupkg" -ForegroundColor White -BackgroundColor Red
	Exit
}
if (!(Test-Path -Path $statelessWorkflowMongoDbNupkg)) {
    Write-Host "Stateless.WorkflowEngine.MongoDb nuget package not found at $statelessWorkflowMongoDbNupkg" -ForegroundColor White -BackgroundColor Red
 	Exit
}

# now we push!
if ($isProduction) {
    & dotnet nuget push "$statelessWorkflowNupkg" --source https://api.nuget.org/v3/index.json --api-key $apiKey
    & dotnet nuget push "$statelessWorkflowMongoDbNupkg" --source https://api.nuget.org/v3/index.json --api-key $apiKey
}
else {
    # pushing to a folder creates a local feed for the NugetTest app
    New-Item -ItemType Directory -Force -Path "$root\NugetTest\NugetPackageTest" | Out-Null
    & dotnet nuget push "$statelessWorkflowNupkg" --source "$root\NugetTest\NugetPackageTest"
    & dotnet nuget push "$statelessWorkflowMongoDbNupkg" --source "$root\NugetTest\NugetPackageTest"
}

cd $root
Write-Host "Done" -BackgroundColor Green -ForegroundColor White

