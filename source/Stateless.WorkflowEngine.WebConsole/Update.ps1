<#
.SYNOPSIS
    Updates the Stateless Workflow Engine web console to the latest GitHub release.

.DESCRIPTION
    Run from the console's install folder in an elevated PowerShell prompt.  The script downloads the latest
    release, checks the runtime it needs is installed, backs up the current install, stops the service, replaces
    the application files (keeping Data, logs, backups and any appsettings.Production.json) and starts the service
    again.  If the update fails once the service has been stopped, the backup is restored and the service is
    restarted.  This script is itself replaced by the release's copy, so the next update runs the latest script.
    Installs of the previous Topshelf-based console are converted to a standard Windows service, keeping the
    service's account and startup settings.
#>
Clear-Host
$ErrorActionPreference = "Stop"
$global:scriptRoot = $PSScriptRoot

# variables
$global:latestVersionUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"
$global:serviceExecutableName = "Stateless.WorkflowEngine.WebConsole.exe"
$global:serviceName = "Stateless.WorkflowEngine.Console"
$global:serviceDisplayName = "Stateless Workflow Engine Console"
$global:serviceDescription = "Web console for monitoring Stateless.WorkflowEngine workflow stores"
$global:exePath = "$global:scriptRoot\$global:serviceExecutableName"
$global:tempFolder = "$global:scriptRoot\TempUpdate"
$global:backupFolder = "$global:scriptRoot\Backup"
$global:tempExtractionFolder = "$global:tempFolder\Content"

# files and folders that are never backed up, deleted or overwritten - Update.ps1 is not one of them, so each
# update brings the release's copy of this script
$global:preservedFolders = @("Backup", "Data", "logs", "TempUpdate")
$global:preservedFiles = @("updates.log", "appsettings.Production.json")

# add TLS 1.2 to the protocols the OS allows (older .NET Framework versions don't enable it by default)
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12

function AssertAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run as an Administrator"
    }
}

function AssertServiceInstalledHere {
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$global:serviceName'"
    if (!$service) {
        return
    }

    # the path may be quoted and may be followed by arguments (Topshelf services)
    if ($service.PathName -match '^\s*"?([^"]+?\.exe)') {
        $serviceExePath = [System.IO.Path]::GetFullPath($Matches[1])
        if ($serviceExePath -ne [System.IO.Path]::GetFullPath($global:exePath)) {
            throw "Service '$global:serviceName' runs '$serviceExePath' - run the copy of this script in that folder"
        }
    }
}

function AssertRuntimeInstalled {
    # the release's runtimeconfig lists the shared frameworks it needs, so a release targeting a newer runtime is
    # caught before anything is changed
    $runtimeConfigPath = "$global:tempExtractionFolder\" + [System.IO.Path]::ChangeExtension($global:serviceExecutableName, ".runtimeconfig.json")
    $runtimeOptions = (Get-Content -Path $runtimeConfigPath -Raw | ConvertFrom-Json).runtimeOptions
    $frameworks = @($runtimeOptions.frameworks) + @($runtimeOptions.framework) | Where-Object { $_ }

    # dotnet may not be on the path of an elevated prompt
    $dotnet = "$Env:ProgramFiles\dotnet\dotnet.exe"
    if (!(Test-Path -Path $dotnet)) {
        $dotnet = "dotnet"
    }
    try {
        $runtimes = & $dotnet --list-runtimes 2>$null
    }
    catch {
        $runtimes = @()
    }

    foreach ($framework in $frameworks) {
        # by default a framework rolls forward to any later version with the same major version
        $required = [System.Version]::Parse($framework.version)
        $installed = $false
        foreach ($runtime in $runtimes) {
            if ($runtime -match "^$([regex]::Escape($framework.name)) (\d+\.\d+\.\d+)") {
                $version = [System.Version]::Parse($Matches[1])
                $installed = $installed -or ($version.Major -eq $required.Major -and $version -ge $required)
            }
        }
        if (!$installed) {
            throw "The release needs $($framework.name) $required or a later $($required.Major).x version, which is not installed - download the ASP.NET Core Runtime $($required.Major) (Hosting Bundle or x64 installer) from https://dotnet.microsoft.com/download/dotnet/$($required.Major).0 and run this script again"
        }
    }
    LogMessage -msg "Required runtimes are installed"
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

function CopyFiles {
    param ([System.String]$source, [System.String]$destination)

    Get-ChildItem -Path $source -Recurse -Force -File | Where-Object { -not (IsPreserved -item $_ -root $source) } | ForEach-Object {
        $targetPath = $destination + $_.FullName.Substring($source.Length)
        New-Item -ItemType Directory -Path (Split-Path $targetPath) -Force | Out-Null
        Copy-Item -Path $_.FullName -Destination $targetPath -Force
    }
}

function BackupCurrent {
    param ([System.String]$currentVersion)

    $source = $global:scriptRoot
    $destination = "$global:backupFolder\$currentVersion"

    # a backup from an earlier attempt would be merged with this one, and restoring it would bring back stale files
    if (Test-Path -Path $destination) {
        $destination += "-" + (Get-Date -Format "yyyyMMddHHmmss")
    }
    LogMessage -msg "Backing up current version to '$destination'"

    CopyFiles -source $source -destination $destination

    # the user store is small and important - always include it in the backup
    if (Test-Path -Path "$source\Data") {
        Copy-Item -Path "$source\Data" -Destination "$destination\Data" -Recurse -Force
    }

    LogMessage -msg "Current version backed up to '$destination'"
    return $destination
}

function RestoreBackup {
    param ([System.String]$backupPath)

    LogMessage -msg "Restoring the previous version from '$backupPath'" -level "WARN"
    DeleteCurrent
    # Data is never changed by the update, so the live copy is kept rather than restoring the backup's
    CopyFiles -source $backupPath -destination $global:scriptRoot
    LogMessage -msg "Previous version restored" -level "WARN"
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
    CopyFiles -source $global:tempExtractionFolder -destination $global:scriptRoot
    LogMessage -msg "Latest version copied into service folder '$global:scriptRoot'"
}

function GetCurrentVersion {
    if (!(Test-Path -Path $global:exePath)) {
        LogMessage -msg "File '$global:exePath' does not exist" -level "WARN"
        return "0.0.0"
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($global:exePath)
    $version = $null
    if (![System.Version]::TryParse("$($versionInfo.FileVersion)", [ref]$version)) {
        LogMessage -msg "Unable to read the version of '$global:exePath'" -level "WARN"
        return "0.0.0"
    }
    LogMessage -msg "Version installed: $($versionInfo.FileVersion)"
    return $version.ToString(3)
}

function GetLatestVersion {
    $data = Invoke-RestMethod -Uri $global:latestVersionUrl -UseBasicParsing
    $asset = $data.assets | Where-Object { $_.name -like "*webconsole*.zip" } | Select-Object -First 1
    if (!$asset) {
        throw "The latest release ($($data.tag_name)) does not contain a web console package"
    }

    $result = [PSCustomObject]@{
        version = ($data.tag_name.Trim().TrimStart('v') -split '-')[0]
        fileName = $asset.name
        downloadUrl = $asset.browser_download_url
        size = $asset.size
        # e.g. "sha256:abc..." - only present for releases published since GitHub started recording digests
        digest = $asset.digest
    }
    LogMessage -msg "Latest version available: $($result.version)"
    return $result
}

function DownloadRelease {
    param ($release)

    New-Item -Path $global:tempFolder -ItemType Directory -Force | Out-Null
    $zipPath = "$global:tempFolder\$($release.fileName)"

    # always download - a zip left by an interrupted run may be incomplete
    Invoke-WebRequest -Uri $release.downloadUrl -OutFile $zipPath -UseBasicParsing

    if ($release.size -and (Get-Item -Path $zipPath).Length -ne $release.size) {
        throw "The downloaded release is $((Get-Item -Path $zipPath).Length) bytes, expected $($release.size)"
    }
    if ($release.digest -like "sha256:*") {
        $expectedHash = $release.digest.Substring(7)
        $actualHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) {
            throw "The downloaded release's SHA-256 hash '$actualHash' does not match the published hash '$expectedHash'"
        }
        LogMessage -msg "Downloaded release hash verified"
    }

    LogMessage -msg "Downloaded latest release to '$zipPath'"
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
    # don't let a log file that can't be written (e.g. without elevation) hide the original error
    Add-Content -Path "$global:scriptRoot\updates.log" -Value "$(Get-Date)|$level|$msg" -ErrorAction SilentlyContinue
}

function StopService {
    $service = Get-Service -Name $global:serviceName -ErrorAction SilentlyContinue
    if (!$service) {
        LogMessage -msg "Service '$global:serviceName' is not installed."
    }
    elseif ($service.Status -ne 'Stopped') {
        LogMessage -msg "Stopping service '$global:serviceName'"
        Stop-Service -Name $global:serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
        LogMessage -msg "Stopped service '$global:serviceName'"
    }

    # the service process can take a moment to exit after the service stops, and the console may also be running
    # outside the service - either would lock the files
    $deadline = (Get-Date).AddSeconds(30)
    while ($true) {
        $processes = Get-Process -Name ([System.IO.Path]::GetFileNameWithoutExtension($global:serviceExecutableName)) -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq $global:exePath }
        if (!$processes) {
            return
        }
        if ((Get-Date) -gt $deadline) {
            throw "'$global:exePath' is still running (process id $(($processes | ForEach-Object { $_.Id }) -join ', ')) - close it and run this script again"
        }
        Start-Sleep -Seconds 1
    }
}

function InstallService {
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$global:serviceName'"

    if (!$service) {
        New-Service -Name $global:serviceName -BinaryPathName "`"$global:exePath`"" -DisplayName $global:serviceDisplayName -Description $global:serviceDescription -StartupType Automatic | Out-Null
        LogMessage -msg "Service '$global:serviceName' installed."
    }
    elseif ($service.PathName -like "*-servicename*") {
        # services installed by the previous (Topshelf) console pass arguments the new console doesn't understand -
        # change the path rather than recreating the service, which would lose its account and recovery settings
        LogMessage -msg "Converting legacy Topshelf service registration"
        $global:legacyServicePath = $service.PathName
        SetServicePath -path "`"$global:exePath`""
    }
}

function SetServicePath {
    param ([System.String]$path)

    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$global:serviceName'"
    $result = Invoke-CimMethod -InputObject $service -MethodName Change -Arguments @{ PathName = $path }
    if ($result.ReturnValue -ne 0) {
        throw "Unable to set the path of service '$global:serviceName' (Win32_Service.Change returned $($result.ReturnValue))"
    }
}

function StartService {
    Start-Service -Name $global:serviceName

    # a console that fails on startup (e.g. the port is in use) stops shortly after starting
    Start-Sleep -Seconds 5
    $service = Get-Service -Name $global:serviceName
    if ($service.Status -ne 'Running') {
        throw "Service '$global:serviceName' stopped after starting (status $($service.Status)) - see the logs folder and the Windows event log"
    }
    LogMessage -msg "Service '$global:serviceName' started."
}

function RemoveTempFolder {
    if (Test-Path -Path $global:tempFolder) {
        Remove-Item -Path $global:tempFolder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        LogMessage -msg "Temp folder '$global:tempFolder' deleted."
    }
}

function Run {
    $backupPath = $null
    $serviceWasRunning = $false
    $filesChanged = $false
    $global:legacyServicePath = $null
    try {
        AssertAdministrator

        LogMessage -msg "----------------------------------------------------------------------"
        LogMessage -msg "Starting update initiated by user '$Env:UserName'"
        LogMessage -msg "----------------------------------------------------------------------"

        AssertServiceInstalledHere

        $versionInstalled = GetCurrentVersion
        $latestVersionInfo = GetLatestVersion

        if ([System.Version]::Parse($versionInstalled) -ge [System.Version]::Parse($latestVersionInfo.version)) {
            LogMessage -msg "The latest version is already installed" -level "WARN"
            # an update run by the 5.x script copies the new files but leaves the Topshelf service registration (and
            # may leave the service stopped) - finish converting it
            InstallService
            if ($global:legacyServicePath -and (Get-Service -Name $global:serviceName).Status -ne 'Running') {
                StopService
                StartService
            }
            Exit
        }

        $zipPath = DownloadRelease -release $latestVersionInfo
        ExtractRelease -zipPath $zipPath
        AssertRuntimeInstalled
        $serviceWasRunning = (Get-Service -Name $global:serviceName -ErrorAction SilentlyContinue).Status -eq "Running"
        $backupPath = BackupCurrent -currentVersion $versionInstalled

        # from here on a failure restarts the service, restoring the backup first once files have changed
        StopService
        $filesChanged = $true
        DeleteCurrent
        CopyNewVersion
        InstallService
        StartService
        RemoveTempFolder

        LogMessage -msg "Update to version $($latestVersionInfo.version) complete"
    }
    catch {
        LogMessage -msg "An error occurred: $($_.Exception.Message)" -level "ERROR"

        if ($backupPath) {
            try {
                if ($filesChanged) {
                    StopService
                    RestoreBackup -backupPath $backupPath
                    if ($global:legacyServicePath) {
                        SetServicePath -path $global:legacyServicePath
                    }
                }
                if ($serviceWasRunning) {
                    StartService
                }
            }
            catch {
                LogMessage -msg "Unable to restore the previous version: $($_.Exception.Message) - restore it manually from '$backupPath'" -level "ERROR"
            }
        }
        Exit 1
    }
}

Run
