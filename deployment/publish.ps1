$root = $PSScriptRoot
cd $root
cd ..
cd source

# read the api key from a file that is not published with the repository
$apiKey = [System.IO.File]::ReadAllText("$root\_apikey.txt").Trim()

Write-Host "NOTE: Ensure that you have updated the nuspec files to include dependencies!" -BackgroundColor Red -ForegroundColor White
$version = Read-Host -Prompt "What version are we publishing? [e.g. 2.3.0]"

# Stateless.WorkflowEngine
cd Stateless.WorkflowEngine
nuget pack -Prop Platform=AnyCPU
Invoke-Expression "& nuget push Stateless.WorkflowEngine.$version.nupkg $apiKey -Source https://www.nuget.org/api/v2/package"
cd ..

# Stateless.WorkflowEngine.MongoDb
cd Stateless.WorkflowEngine.MongoDb
nuget pack -IncludeReferencedProjects -Prop Platform=AnyCPU 
Invoke-Expression "& nuget push Stateless.WorkflowEngine.MongoDb.$version.nupkg $apiKey -Source https://www.nuget.org/api/v2/package"
cd ..

Write-Host "Done" -BackgroundColor Green -ForegroundColor White

