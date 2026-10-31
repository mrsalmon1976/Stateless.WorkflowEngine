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

	$doc = New-Object System.Xml.XmlDocument
	$doc.Load($filePath)
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/Version" -newValue $versionWithSuffix
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/AssemblyVersion" -newValue $version
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/FileVersion" -newValue $version
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//package/metadata/version" -newValue $version
	$doc.Save($filePath)
}

function UpdateXmlNodeIfExists
{
	param($xmlDoc, $xpath, $newValue)
	$node = $xmlDoc.SelectSingleNode($xpath)
	if ($null -ne $node)
	{
		$node.InnerText = $newValue
	}
}

function ZipFile
{
	param(
		[String]$sourceFile,
		[String]$zipFile
	)

	$exeloc = ""
	if (Test-Path -Path "C:\Program Files\7-Zip\7z.exe") {
		$exeloc = "C:\Program Files\7-Zip\7z.exe"
	}
	elseif (Test-Path -Path "C:\Program Files (x86)\7-Zip\7z.exe") {
		$exeloc = "C:\Program Files (x86)\7-Zip\7z.exe"
	}
	else {
		Write-Host "Unable to find 7-zip executable" -BackgroundColor Red -ForegroundColor White
		Exit 1
	}

	set-alias sz $exeloc  
	sz a -xr!'Data' -tzip -r $zipFile $sourceFile | Out-Null
}

$root = $PSScriptRoot
$publish = "$root\publish"
if (Test-Path -Path $publish) { Remove-Item $publish -Recurse -Force }
$source = [System.IO.Path]::Combine($root.Replace("deployment", ""), "source") 
$version = Read-Host -Prompt "What version are we building? [e.g. 2.3.0]"
$preReleaseSuffix = Read-Host -Prompt "Enter a pre-release suffix (e.g. alpha1) if you would like one, or just hit ENTER for no release suffix"
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
if (!(Test-Path -Path "$mongodbPublishDir\Stateless.WorkflowEngine.MongoDb.dll")) {
	Write-Host "MongoDb publish location/files not found at $mongodbPublishDir" -ForegroundColor White -BackgroundColor Red
	Exit
}
$zip = "$root\Stateless.WorkflowEngine.MongoDb_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$mongodbPublishDir\*.*" -zipfile $zip 

# package raven db 
Write-Host "Building Stateless.Workflow.RavenDB version $versionWithSuffix"
$ravendbPublishDir = "$publish\Stateless.WorkflowEngine.RavenDb"
& dotnet publish "$source\Stateless.WorkflowEngine.RavenDb\Stateless.WorkflowEngine.RavenDb.csproj" -c Release -o "$ravendbPublishDir"
if (!(Test-Path -Path $ravendbPublishDir) -or !(Test-Path -Path "$ravendbPublishDir\Stateless.WorkflowEngine.RavenDb.dll")) {
	Write-Host "RavenDb publish location/files not found at $ravendbPublishDir" -ForegroundColor White -BackgroundColor Red
	Exit
}
$zip = "$root\Stateless.WorkflowEngine.RavenDb_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$ravendbPublishDir\*.*" -zipfile $zip 

# package the web console (the client is built into wwwroot by the BuildClient target - requires node/npm)
Write-Host "Building Stateless.Workflow.WebConsole version $versionWithSuffix"
$webconsolePublishDir = "$publish\Stateless.WorkflowEngine.WebConsole"
& dotnet publish "$source\Stateless.WorkflowEngine.WebConsole\Stateless.WorkflowEngine.WebConsole.csproj" -c Release -r win-x64 --self-contained false -o "$webconsolePublishDir"
if (!(Test-Path -Path "$webconsolePublishDir\Stateless.WorkflowEngine.WebConsole.exe") -or !(Test-Path -Path "$webconsolePublishDir\wwwroot\index.html")) {
	Write-Host "WebConsole publish location/files not found at $webconsolePublishDir" -ForegroundColor White -BackgroundColor Red
	Exit
}
UpdateJsonSetting -filePath "$webconsolePublishDir\appsettings.json" -key "LatestVersionUrl" -value "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"

$zip = "$root\Stateless.WorkflowEngine.WebConsole_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$webconsolePublishDir\*" -zipfile $zip 

$buildDocker = Read-Host -Prompt "Build the web console Docker image? [y/N]"
if ($buildDocker -eq "y") {
	& docker build -f "$source\Stateless.WorkflowEngine.WebConsole\Dockerfile" -t "stateless-workflowengine-webconsole:$versionWithSuffix" -t "stateless-workflowengine-webconsole:latest" "$source"
}

Write-Host "Done" -BackgroundColor Green -ForegroundColor White
