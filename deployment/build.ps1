$ErrorActionPreference = "Stop"

function GetMSBuildPath()
{
	# default to latest version isntalled with VS2019
	$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"

	# try look for MSBuild 15.x in other locations
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
	param([string]$filePath, [string]$version)

	if (!(Test-Path -Path $filePath)) {
		throw "$filePath does not exist - unable to update project file"
	}

	$doc = New-Object System.Xml.XmlDocument
	$doc.Load($filePath)
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/Version" -newValue $version
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/AssemblyVersion" -newValue $version
	UpdateXmlNodeIfExists -xmlDoc $doc -xpath "//PropertyGroup/FileVersion" -newValue $version
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
$source = $root.Replace("deployment", "") + "\source"
$version = Read-Host -Prompt "What version are we building? [e.g. 2.3.0]"

# make sure the files reflect the correct assembly version
UpdateAssemblyVersion -assemblyFilePath "$source\SharedAssemblyInfo.cs" -version $version
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine\Stateless.WorkflowEngine.csproj" -version $version
UpdateProjectVersion -filePath "$source\Stateless.WorkflowEngine.MongoDb\Stateless.WorkflowEngine.MongoDb.csproj" -version $version

# run msbuild on the solution
Write-Host "Building solution $version"
Set-Location "$source"
$msbuild = GetMSBuildPath
& $msbuild Stateless.WorkflowEngine.sln /t:Clean,Build /p:Configuration=Release
Set-Location $root

# package mongodb
$mongodb = "$source\Stateless.WorkflowEngine.MongoDb\bin\Release\netstandard2.0\publish"
$zip = "$root\Stateless.WorkflowEngine.MongoDb_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$mongodb\*.*" -zipfile $zip 

# package raven db 
Write-Host "Building Stateless.Workflow.RavenDB version $version"
$ravendb = "$source\Stateless.WorkflowEngine.RavenDb\bin\Release"
$zip = "$root\Stateless.WorkflowEngine.RavenDb_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$ravendb\*.*" -zipfile $zip 

# package the workflow console with production settings
Write-Host "Building Stateless.Workflow.WebConsole version $version"
$webconsole = "$source\Stateless.WorkflowEngine.WebConsole\bin\Release"
UpdateAppConfigSetting -filePath "$webConsole\Stateless.WorkflowEngine.WebConsole.exe.config" -key "LatestVersionUrl" -value "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"
UpdateAppConfigSetting -filePath "$webConsole\AutoUpdater\Stateless.WorkflowEngine.WebConsole.AutoUpdater.exe.config" -key "LatestVersionUrl" -value "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"
$zip = "$root\Stateless.WorkflowEngine.WebConsole_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$webconsole\*.*" -zipfile $zip 

Write-Host "Done" -BackgroundColor Green -ForegroundColor White

