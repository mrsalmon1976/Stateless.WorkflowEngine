$ErrorActionPreference = "Stop"
Clear-Host

function GetMSBuildPath()
{
	# default to VS2022 Pro
	$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"

	# VS2022 Community
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
	}

	# VS2019
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
	}
	
	# VS2017
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
	}
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		$msbuild = "D:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
	}
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
	}
	
	if ([System.IO.File]::Exists($msbuild) -eq $false)
	{
		Write-Output "Unable to find valid MSBuild.exe" 
		Throw "Unable to find valid MSBuild.exe" 
		exit 1
	}	
	return $msbuild
}

function UpdateAppConfigSetting
{
	param([string]$filePath, [string]$key, [string]$value)

	if (!(Test-Path -Path $filePath)) {
		throw "$filePath does not exist - unable to update setting"
	}

	$doc = New-Object System.Xml.XmlDocument
	$doc.Load($filePath)

	$setting = $doc.SelectSingleNode("//appSettings/add[@key = '$key']")
	$setting.value = "$value"

	$doc.Save($filePath)
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

function UpdateAssemblyVersion
{
  param ([string]$assemblyFilePath, [string]$version)
  $newVersion = 'AssemblyVersion("' + $version + '")';
  $newFileVersion = 'AssemblyFileVersion("' + $version + '")';

  if (!(Test-Path -Path $assemblyFilePath)) { throw "Assembly version file '$assemblyFilePath' does not exist" }

	$tmpFile = $assemblyFilePath + ".tmp"
	if (Test-Path -Path $tmpFile) { Remove-Item -Path $tmpFile -Force }

 	Get-Content -Encoding UTF8 $assemblyFilePath | 
    	%{$_ -replace 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newVersion } |
    	%{$_ -replace 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newFileVersion }  > $tmpFile

 	Move-Item $tmpFile $assemblyFilePath -force
}

function UpdateNuspecVersion
{
	param ([string]$filePath, [string]$version)
	[xml]$xmlDoc = Get-Content $filePath
	$xmlDoc.package.metadata.version = $version

	foreach ($dep in $xmlDoc.package.metadata.dependencies.group.dependency) {
		if ($dep.id -eq "Stateless.WorkflowEngine") {
			$dep.version = $version
		}
	}

	$xmlDoc.Save($filePath)
	
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
	sz a -xr!'Data\users.json' -tzip -r $zipFile $sourceFile | Out-Null
}

$root = $PSScriptRoot
$publish = "$root\publish"
if (Test-Path -Path $publish) { Remove-Item $publish -Recurse -Force }
$source = [System.IO.Path]::Combine($root.Replace("deployment", ""), "source") 
$version = Read-Host -Prompt "What version are we building? [e.g. 2.3.0]"
$preReleaseSuffix = Read-Host -Prompt "Would you like to add a pre-release suffix? [e.g. alpha1]"
$versionWithSuffix = "$version-$preReleaseSuffix".TrimEnd("-")


# make sure the files reflect the correct assembly version
UpdateAssemblyVersion -assemblyFilePath "$source\SharedAssemblyInfo.cs" -version $version
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine\Stateless.WorkflowEngine.csproj" -version $version -versionWithSuffix $versionWithSuffix
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.MongoDb\Stateless.WorkflowEngine.MongoDb.csproj" -version $version -versionWithSuffix $versionWithSuffix
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.RavenDb\Stateless.WorkflowEngine.RavenDb.csproj" -version $version -versionWithSuffix $versionWithSuffix

# run msbuild on the solution
Write-Host "Building solution $version"
# Set-Location "$source"
$msbuild = GetMSBuildPath
# & $msbuild Stateless.WorkflowEngine.sln /t:Clean,Build /p:Configuration=Release /p:OutDir=$publish
# Set-Location $root

# package mongodb
Write-Host "Building Stateless.Workflow.MongoDb version $versionWithSuffix"
$mongodbPublishDir = "$publish\Stateless.WorkflowEngine.MongoDb"
& $msbuild "$source\Stateless.WorkflowEngine.MongoDb\Stateless.WorkflowEngine.MongoDb.csproj" /t:Clean,Build /p:Configuration=Release /p:OutDir="$mongodbPublishDir"
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
& $msbuild "$source\Stateless.WorkflowEngine.RavenDb\Stateless.WorkflowEngine.RavenDb.csproj" /t:Clean,Build /p:Configuration=Release /p:OutDir="$ravendbPublishDir"
if (!(Test-Path -Path $ravendbPublishDir) -or !(Test-Path -Path "$ravendbPublishDir\Stateless.WorkflowEngine.RavenDb.dll")) {
	Write-Host "RavenDb publish location/files not found at $ravendbPublishDir" -ForegroundColor White -BackgroundColor Red
	Exit
}
$zip = "$root\Stateless.WorkflowEngine.RavenDb_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$ravendbPublishDir\*.*" -zipfile $zip 

# package the workflow console with production settings
Write-Host "Building Stateless.Workflow.WebConsole version $versionWithSuffix"
$webconsolePublishDir = "$publish\Stateless.WorkflowEngine.WebConsole"
& $msbuild "$source\Stateless.WorkflowEngine.WebConsole\Stateless.WorkflowEngine.WebConsole.csproj" /t:Clean,Build /p:Configuration=Release /p:OutDir="$webconsolePublishDir"
if (!(Test-Path -Path "$webconsolePublishDir\Stateless.WorkflowEngine.WebConsole.exe")) {
	Write-Host "WebConsole publish location/files not found at $webconsolePublishDir" -ForegroundColor White -BackgroundColor Red
	Exit
}
UpdateAppConfigSetting -filePath "$webconsolePublishDir\Stateless.WorkflowEngine.WebConsole.exe.config" -key "LatestVersionUrl" -value "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"

$zip = "$root\Stateless.WorkflowEngine.WebConsole_v$versionWithSuffix.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$webconsolePublishDir\*.*" -zipfile $zip 

Write-Host "Done" -BackgroundColor Green -ForegroundColor White

