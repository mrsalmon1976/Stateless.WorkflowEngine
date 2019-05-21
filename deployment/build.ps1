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
$version = Read-Host -Prompt "What version are we building? [e.g. 1.5.0]"

# build for mongo db 
Write-Host "Building Stateless.Workflow.MongoDB version $version"
$mongodb = "$source\Stateless.WorkflowEngine.MongoDb\bin\Release"
$zip = "$root\Stateless.WorkflowEngine.MongoDb_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$mongodb\*.*" -zipfile $zip 

# build for raven db 
Write-Host "Building Stateless.Workflow.RavenDB version $version"
$ravendb = "$source\Stateless.WorkflowEngine.RavenDb\bin\Release"
$zip = "$root\Stateless.WorkflowEngine.RavenDb_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$ravendb\*.*" -zipfile $zip 

# build the workflow console
Write-Host "Building Stateless.Workflow.WebConsole version $version"
$webconsole = "$source\Stateless.WorkflowEngine.WebConsole\bin\Release"
$zip = "$root\Stateless.WorkflowEngine.WebConsole_v$version.zip"
[system.io.file]::Delete($zip)
ZipFile -sourcefile "$webconsole\*.*" -zipfile $zip 

Write-Host "Done" -BackgroundColor Green -ForegroundColor White

