<#
.SYNOPSIS
    Updates the Stateless Workflow Engine web console to the latest GitHub release.

.DESCRIPTION
    Run from the console's install folder in an elevated PowerShell prompt.  The script downloads the latest
    release, backs up the current install, stops the service, replaces the application files (keeping Data,
    logs, backups and any appsettings.Production.json) and starts the service again.  Installs of the
    previous Topshelf-based console are converted to a standard Windows service.
#>
Clear-Host
$ErrorActionPreference = "Stop"
$global:scriptRoot = $PSScriptRoot

# variables
$global:latestVersionUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"
$global:serviceExecutableName = "Stateless.WorkflowEngine.WebConsole.exe"
$global:serviceName = "Stateless.WorkflowEngine.Console"
$global:serviceDisplayName = "Stateless Workflow Engine Console"
$global:tempFolder = "$global:scriptRoot\TempUpdate"
$global:backupFolder = "$global:scriptRoot\Backup"
$global:tempExtractionFolder = "$global:tempFolder\Content"

# files and folders that are never backed up, deleted or overwritten
$global:preservedFolders = @("Backup", "Data", "logs", "TempUpdate")
$global:preservedFiles = @("updates.log", "Update.ps1", "appsettings.Production.json")

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

function AssertAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run as an Administrator"
    }
}

function AssertRuntimeInstalled {
    $runtimes = & dotnet --list-runtimes 2>$null
    if (-not ($runtimes | Where-Object { $_ -like "Microsoft.AspNetCore.App 10.*" })) {
        throw "The ASP.NET Core 10 runtime is not installed - download it from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again"
    }
}

function IsPreserved {
    param ($item, [System.String]$root)

    $relativePath = $item.FullName.Substring($root.Length).TrimStart('\')
    $topLevel = $relativePath.Split('\')[0]
    if ($global:preservedFolders -contains $topLevel) {
        return $true
    }
    return (-not $item.PSIsContainer) -and ($relativePath -notlike "*\*") -and ($global:preservedFiles -contains $item.Name)
}

function BackupCurrent {
    param ([System.String]$currentVersion)

    $source = $global:scriptRoot
    $destination = "$global:backupFolder\$currentVersion"
    LogMessage -msg "Backing up current version to '$destination'"

    Get-ChildItem -Path $source -Recurse -Force | Where-Object { -not (IsPreserved -item $_ -root $source) } | ForEach-Object {
        $targetPath = $destination + $_.FullName.Substring($source.Length)
        if ($_.PSIsContainer) {
            New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        }
        else {
            New-Item -ItemType Directory -Path (Split-Path $targetPath) -Force | Out-Null
            Copy-Item -Path $_.FullName -Destination $targetPath -Force
        }
    }

    # the user store is small and important - always include it in the backup
    if (Test-Path -Path "$source\Data") {
        Copy-Item -Path "$source\Data" -Destination "$destination\Data" -Recurse -Force
    }

    LogMessage -msg "Current version backed up to '$destination'"
}

function DeleteCurrent {
    $source = $global:scriptRoot

    # delete files first, then empty folders (deepest first)
    Get-ChildItem -Path $source -Recurse -Force -File | Where-Object { -not (IsPreserved -item $_ -root $source) } | Remove-Item -Force
    Get-ChildItem -Path $source -Recurse -Force -Directory | Where-Object { -not (IsPreserved -item $_ -root $source) } |
        Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { -not (Get-ChildItem -Path $_.FullName -Force) } |
        Remove-Item -Force

    LogMessage -msg "Deleted current version"
}

function CopyNewVersion {
    $source = $global:tempExtractionFolder
    $destination = $global:scriptRoot

    Get-ChildItem -Path $source -Recurse -Force -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($source.Length).TrimStart('\')
        if ($global:preservedFiles -contains $relativePath) {
            return
        }
        $targetPath = Join-Path $destination $relativePath
        New-Item -ItemType Directory -Path (Split-Path $targetPath) -Force | Out-Null
        Copy-Item -Path $_.FullName -Destination $targetPath -Force
    }

    LogMessage -msg "Latest version copied into service folder '$destination'"
}

function GetCurrentVersion {
    $pathToExe = "$global:scriptRoot\$global:serviceExecutableName"
    if (!(Test-Path -Path $pathToExe)) {
        LogMessage -msg "File '$pathToExe' does not exist" -level "WARN"
        return "0.0.0"
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pathToExe)
    LogMessage -msg "Version installed: $($versionInfo.FileVersion)"
    return [System.Version]::Parse($versionInfo.FileVersion).ToString(3)
}

function GetLatestVersion {
    $data = Invoke-RestMethod -Uri $global:latestVersionUrl
    $asset = $data.assets | Where-Object { $_.name -like "*webconsole*" } | Select-Object -First 1
    if (!$asset) {
        throw "The latest release does not contain a web console package"
    }

    $result = [PSCustomObject]@{
        version = ($data.tag_name.Trim().TrimStart('v') -split '-')[0]
        fileName = $asset.name
        downloadUrl = $asset.browser_download_url
    }
    LogMessage -msg "Latest version available: $($result.version)"
    return $result
}

function DownloadRelease {
    param ([System.String]$url, [System.String]$fileName)

    New-Item -Path $global:tempFolder -ItemType Directory -Force | Out-Null
    $zipPath = "$global:tempFolder\$fileName"
    if (Test-Path -Path $zipPath) {
        LogMessage -msg "Latest release already downloaded to '$zipPath'"
    }
    else {
        Invoke-WebRequest -Uri $url -OutFile $zipPath
        LogMessage -msg "Downloaded latest release to '$zipPath'"
    }
    return $zipPath
}

function ExtractRelease {
    param ([System.String]$zipPath)

    $path = $global:tempExtractionFolder
    if (Test-Path -Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
    Expand-Archive -Path $zipPath -DestinationPath $path

    if (!(Test-Path -Path "$path\$global:serviceExecutableName")) {
        throw "The downloaded release does not contain '$global:serviceExecutableName'"
    }
    LogMessage -msg "Release unzipped to '$path'"
}

function LogMessage {
    param ([System.String]$msg, [System.String]$level = "INFO")

    $consoleText = "$(Get-Date) $msg"
    if ($level -eq "ERROR") {
        Write-Host $consoleText -BackgroundColor Red
    }
    elseif ($level -eq "WARN") {
        Write-Host $consoleText -ForegroundColor Yellow
    }
    else {
        Write-Host $consoleText
    }
    Add-Content -Path "$global:scriptRoot\updates.log" -Value "$(Get-Date)|$level|$msg"
}

function StopService {
    $service = Get-Service -Name $global:serviceName -ErrorAction SilentlyContinue
    if (!$service) {
        LogMessage -msg "Service '$global:serviceName' is not installed."
        return
    }
    if ($service.Status -ne 'Stopped') {
        LogMessage -msg "Stopping service '$global:serviceName'"
        Stop-Service -Name $global:serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        # give the process time to release file locks
        Start-Sleep -Seconds 2
        LogMessage -msg "Stopped service '$global:serviceName'"
    }
}

function InstallAndStartService {
    $exePath = "$global:scriptRoot\$global:serviceExecutableName"
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$global:serviceName'"

    # services installed by the previous (Topshelf) console pass arguments the new console doesn't understand
    if ($service -and $service.PathName -like "*-servicename*") {
        LogMessage -msg "Converting legacy Topshelf service registration"
        sc.exe delete $global:serviceName | Out-Null
        Start-Sleep -Seconds 2
        $service = $null
    }

    if (!$service) {
        New-Service -Name $global:serviceName -BinaryPathName "`"$exePath`"" -DisplayName $global:serviceDisplayName -StartupType Automatic | Out-Null
        LogMessage -msg "Service '$global:serviceName' installed."
    }

    Start-Service -Name $global:serviceName
    LogMessage -msg "Service '$global:serviceName' started."
}

function RemoveTempFolder {
    if (Test-Path -Path $global:tempFolder) {
        Remove-Item -Path $global:tempFolder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        LogMessage -msg "Temp folder '$global:tempFolder' deleted."
    }
}

function Run {
    try {
        LogMessage -msg "----------------------------------------------------------------------"
        LogMessage -msg "Starting update initiated by user '$Env:UserName'"
        LogMessage -msg "----------------------------------------------------------------------"

        AssertAdministrator
        AssertRuntimeInstalled

        $versionInstalled = GetCurrentVersion
        $latestVersionInfo = GetLatestVersion

        if ([System.Version]::Parse($versionInstalled) -ge [System.Version]::Parse($latestVersionInfo.version)) {
            LogMessage -msg "The latest version is already installed" -level "WARN"
            Exit
        }

        $zipPath = DownloadRelease -url $latestVersionInfo.downloadUrl -fileName $latestVersionInfo.fileName
        ExtractRelease -zipPath $zipPath
        BackupCurrent -currentVersion $versionInstalled
        StopService
        DeleteCurrent
        CopyNewVersion
        InstallAndStartService
        RemoveTempFolder

        LogMessage -msg "Update to version $($latestVersionInfo.version) complete"
    }
    catch {
        LogMessage -msg "An error occurred: $($_.Exception.Message)" -level "ERROR"
        Exit 1
    }
}

Run
