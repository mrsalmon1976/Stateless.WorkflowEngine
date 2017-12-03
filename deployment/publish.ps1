$root = $PSScriptRoot
cd $root
cd ..
cd source

# Stateless.WorkflowEngine
cd Stateless.WorkflowEngine
nuget pack -Prop Platform=AnyCPU
nuget push Stateless.WorkflowEngine.1.1.1.nupkg f384a881-0a0f-4149-862d-e00d03b2e13c -Source https://www.nuget.org/api/v2/package
cd ..

# Stateless.WorkflowEngine.MongoDb
cd Stateless.WorkflowEngine.MongoDb
nuget pack -IncludeReferencedProjects -Prop Platform=AnyCPU 
nuget push Stateless.WorkflowEngine.MongoDb.1.1.1.nupkg f384a881-0a0f-4149-862d-e00d03b2e13c -Source https://www.nuget.org/api/v2/package
cd ..

Write-Host "Done" -BackgroundColor Green -ForegroundColor White

