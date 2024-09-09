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
    & nuget push "$statelessWorkflowNupkg" -Source https://www.nuget.org/api/v2/package -ApiKey $apiKey
    & nuget push "$statelessWorkflowMongoDbNupkg" -Source https://www.nuget.org/api/v2/package -ApiKey $apiKey
}
else {
    & nuget add "$statelessWorkflowNupkg" -source "$root\NugetTest\NugetPackageTest"
    & nuget add "$statelessWorkflowMongoDbNupkg" -source "$root\NugetTest\NugetPackageTest"
}


Write-Host "Done" -BackgroundColor Green -ForegroundColor White

