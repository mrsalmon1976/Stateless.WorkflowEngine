$ErrorActionPreference = "Stop"
Clear-Host

function UpdateJsonSetting
{
	param([string]$filePath, [string]$key, [string]$value)

	if (!(Test-Path -Path $filePath)) {
		throw "$filePath does not exist - unable to update setting"
	}

	$json = Get-Content -Path $filePath -Raw | ConvertFrom-Json
	$json.$key = $value
	$json | ConvertTo-Json -Depth 10 | Set-Content -Path $filePath -Encoding UTF8
}

function UpdateProjectVersion
{
	param([string]$filePath, [string]$version, [string]$versionWithSuffix)

	if (!(Test-Path -Path $filePath)) {
		throw "$filePath does not exist - unable to update project file"
	}

	# replace the element values as text - saving through XmlDocument reformats the file (blank lines, line endings
	# and escaping), which makes every release's diff noisy
	$content = [System.IO.File]::ReadAllText($filePath)
	$updated = $content -replace '<Version>[^<]*</Version>', "<Version>$versionWithSuffix</Version>"
	$updated = $updated -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$version</AssemblyVersion>"
	$updated = $updated -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$version</FileVersion>"
	if ($updated -notmatch "<Version>$([regex]::Escape($versionWithSuffix))</Version>") {
		throw "$filePath has no <Version> element - unable to set the version"
	}
	[System.IO.File]::WriteAllText($filePath, $updated)
}

function AssertSuccess
{
	param([string]$step)
	if ($LASTEXITCODE -ne 0) {
		Write-Host "$step failed (exit code $LASTEXITCODE)" -ForegroundColor White -BackgroundColor Red
		Exit 1
	}
}

function Get7ZipPath
{
	if (Test-Path -Path "C:\Program Files\7-Zip\7z.exe") {
		return "C:\Program Files\7-Zip\7z.exe"
	}
	if (Test-Path -Path "C:\Program Files (x86)\7-Zip\7z.exe") {
		return "C:\Program Files (x86)\7-Zip\7z.exe"
	}
	Write-Host "Unable to find 7-zip executable" -BackgroundColor Red -ForegroundColor White
	Exit 1
}

function ZipFile
{
	param(
		[String]$sourceFile,
		[String]$zipFile
	)

	& (Get7ZipPath) a -xr!'Data' -tzip -r $zipFile $sourceFile | Out-Null
	AssertSuccess -step "Zipping $zipFile"
}

# the Docker image is built last - check the daemon is running now rather than after every other step has run
$dockerRunning = $false
if (Get-Command docker -ErrorAction SilentlyContinue) {
	# stderr is discarded via cmd so the "Stop" preference doesn't turn docker's error output into an exception
	cmd /c "docker info --format {{.ServerVersion}} >nul 2>&1"
	$dockerRunning = ($LASTEXITCODE -eq 0)
}
if (!$dockerRunning) {
	Write-Host "Docker is not running (or not installed) - start Docker Desktop and run the build again" -ForegroundColor White -BackgroundColor Red
	Exit 1
}

$root = $PSScriptRoot
$publish = "$root\publish"
if (Test-Path -Path $publish) { Remove-Item $publish -Recurse -Force }
$source = [System.IO.Path]::Combine($root.Replace("deployment", ""), "source") 
# the version must be numeric (it becomes the file version, which Update.ps1 compares with the release tag), and
# the suffix must be valid in a NuGet version
do {
	$version = (Read-Host -Prompt "What version are we building? [e.g. 2.3.0]").Trim()
} while ($version -notmatch '^\d+\.\d+\.\d+$')
do {
	$preReleaseSuffix = (Read-Host -Prompt "Enter a pre-release suffix (e.g. alpha1) if you would like one, or just hit ENTER for no release suffix").Trim()
} while ($preReleaseSuffix -notmatch '^[0-9A-Za-z.-]*$')
$versionWithSuffix = "$version-$preReleaseSuffix".TrimEnd("-")


# make sure the files reflect the correct assembly version
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine\Stateless.WorkflowEngine.csproj" -version $version -versionWithSuffix $versionWithSuffix
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.MongoDb\Stateless.WorkflowEngine.MongoDb.csproj" -version $version -versionWithSuffix $versionWithSuffix
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.RavenDb\Stateless.WorkflowEngine.RavenDb.csproj" -version $version -versionWithSuffix $versionWithSuffix
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.WebConsole\Stateless.WorkflowEngine.WebConsole.csproj" -version $version -versionWithSuffix $versionWithSuffix

# package mongodb
Write-Host "Building Stateless.Workflow.MongoDb version $versionWithSuffix"
$mongodbPublishDir = "$publish\Stateless.WorkflowEngine.MongoDb"
& dotnet publish "$source\Stateless.WorkflowEngine.MongoDb\Stateless.WorkflowEngine.MongoDb.csproj" -c Release -o "$mongodbPublishDir"
AssertSuccess -step "Publishing MongoDb"
if (!(Test-Path -Path "$mongodbPublishDir\Stateless.WorkflowEngine.MongoDb.dll")) {
	Write-Host "MongoDb publish location/files not found at $mongodbPublishDir" -ForegroundColor White -BackgroundColor Red
	Exit 1
}
$zip = "$root\Stateless.WorkflowEngine.MongoDb_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$mongodbPublishDir\*.*" -zipfile $zip 

# package raven db 
Write-Host "Building Stateless.Workflow.RavenDB version $versionWithSuffix"
$ravendbPublishDir = "$publish\Stateless.WorkflowEngine.RavenDb"
& dotnet publish "$source\Stateless.WorkflowEngine.RavenDb\Stateless.WorkflowEngine.RavenDb.csproj" -c Release -o "$ravendbPublishDir"
AssertSuccess -step "Publishing RavenDb"
if (!(Test-Path -Path $ravendbPublishDir) -or !(Test-Path -Path "$ravendbPublishDir\Stateless.WorkflowEngine.RavenDb.dll")) {
	Write-Host "RavenDb publish location/files not found at $ravendbPublishDir" -ForegroundColor White -BackgroundColor Red
	Exit 1
}
$zip = "$root\Stateless.WorkflowEngine.RavenDb_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$ravendbPublishDir\*.*" -zipfile $zip 

# package the web console (the client is built into wwwroot by the BuildClient target - requires node/npm)
Write-Host "Building Stateless.Workflow.WebConsole version $versionWithSuffix"
$webconsolePublishDir = "$publish\Stateless.WorkflowEngine.WebConsole"
& dotnet publish "$source\Stateless.WorkflowEngine.WebConsole\Stateless.WorkflowEngine.WebConsole.csproj" -c Release -r win-x64 --self-contained false -o "$webconsolePublishDir"
AssertSuccess -step "Publishing the web console"
if (!(Test-Path -Path "$webconsolePublishDir\Stateless.WorkflowEngine.WebConsole.exe") -or !(Test-Path -Path "$webconsolePublishDir\wwwroot\index.html")) {
	Write-Host "WebConsole publish location/files not found at $webconsolePublishDir" -ForegroundColor White -BackgroundColor Red
	Exit 1
}
UpdateJsonSetting -filePath "$webconsolePublishDir\appsettings.json" -key "LatestVersionUrl" -value "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"

$zip = "$root\Stateless.WorkflowEngine.WebConsole_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$webconsolePublishDir\*" -zipfile $zip 

# build the web console Docker image
Write-Host "Building the web console Docker image stateless-workflowengine-webconsole:$versionWithSuffix"
& docker build -f "$source\Stateless.WorkflowEngine.WebConsole\Dockerfile" -t "stateless-workflowengine-webconsole:$versionWithSuffix" -t "stateless-workflowengine-webconsole:latest" "$source"
AssertSuccess -step "Building the web console Docker image"

# export the image as a release file (load it with "docker load -i <file>") - named after the image rather than
# "Stateless.WorkflowEngine.WebConsole..." so the console's update check and Update.ps1 never mistake it for the zip
$imageTar = "$publish\stateless-workflowengine-webconsole_v$versionWithSuffix.tar"
$imageFile = "$root\stateless-workflowengine-webconsole_v$versionWithSuffix.tar.gz"
Write-Host "Exporting the web console Docker image to $imageFile"
& docker save -o "$imageTar" "stateless-workflowengine-webconsole:$versionWithSuffix"
AssertSuccess -step "Exporting the web console Docker image"
[system.io.file]::Delete($imageFile)
& (Get7ZipPath) a -tgzip $imageFile $imageTar | Out-Null
AssertSuccess -step "Compressing the web console Docker image"
Remove-Item $imageTar

Write-Host "Done" -BackgroundColor Green -ForegroundColor White
