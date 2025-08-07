Clear-Host
$ErrorActionPreference = "Stop"
$global:scriptRoot = $PSScriptRoot

# variables
$global:latestVersionUrl = "https://api.github.com/repos/mrsalmon1976/Stateless.WorkflowEngine/releases/latest"
$global:serviceExecutableName = "Stateless.WorkflowEngine.WebConsole.exe"
$global:serviceName = "Stateless.WorkflowEngine.Console"
$global:tempFolder = "$global:scriptRoot\TempUpdate"
$global:backupFolder = "$global:scriptRoot\Backup"
$global:tempExtractionFolder = "$global:tempFolder\TempUpdate\Content"

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

function BackupCurrent {
    param ([System.String]$currentVersion) 

    $source = $global:scriptRoot
    $destination = "$global:backupFolder\$currentVersion"

    LogMessage -msg "Backing up current version to '$destination' (this may take a while)"

    # Patterns to exclude
    $excludeFiles = @()
    $excludeFolders = @("Backup", "TempUpdate")

    # Create destination folder if it doesn't exist
    if (-not (Test-Path $destination)) {
        New-Item -ItemType Directory -Path $destination | Out-Null
    }

    # Copy items recursively, excluding certain files and folders
    Get-ChildItem -Path $source -Recurse -Force | Where-Object {
        # Exclude files by extension
        foreach ($pattern in $excludeFiles) {
            if ($_.PSIsContainer -eq $false -and $_.Name -like $pattern) {
                return $false
            }
        }

        # Exclude folders by name
        foreach ($folder in $excludeFolders) {
            if ($_.PSIsContainer -and $_.Name -eq $folder) {
                return $false
            }

            if ($_.FullName -like "*\$folder\*") {
                return $false
            }
        }

        return $true
    } | ForEach-Object {
        $targetPath = $_.FullName.Replace($source, $destination)
        
        if ($_.PSIsContainer) {
            if (-not (Test-Path $targetPath)) {
                New-Item -ItemType Directory -Path $targetPath | Out-Null
            }
        } else {
            Copy-Item -Path $_.FullName -Destination $targetPath
        }
    }

    LogMessage -msg "Current version backed up to '$destination'"
    return $destination
}

function CopyNewVersion {

    $source = $global:tempExtractionFolder
    $destination = "$global:scriptRoot"

    # Copy items recursively, excluding certain files and folders
    Get-ChildItem -Path $source -Recurse -Force | ForEach-Object {
        $targetPath = $_.FullName.Replace($source, $destination)
        
        if ($_.PSIsContainer) {
            if (-not (Test-Path $targetPath)) {
                New-Item -ItemType Directory -Path $targetPath | Out-Null
            }
        } else {
            Copy-Item -Path $_.FullName -Destination $targetPath
        }
    }

    LogMessage -msg "Latest version copied into service folder '$destination'"
}

function DeleteCurrent {
    param ([System.String]$currentVersion) 

    $source = $global:scriptRoot

    # Patterns to exclude
    $excludeFiles = @("updates.log", "Update.ps1")
    $excludeFolders = @("Backup", "Data", "TempUpdate")

    # delete items recursively, excluding certain files and folders
    Get-ChildItem -Path $source -Recurse -Force | Where-Object {
        # Exclude files by extension
        foreach ($pattern in $excludeFiles) {
            if ($_.PSIsContainer -eq $false -and $_.Name -like $pattern) {
                return $false
            }
        }

        # Exclude folders by name
        foreach ($folder in $excludeFolders) {
            if ($_.PSIsContainer -and $_.Name -eq $folder) {
                return $false
            }

            if ($_.FullName -like "*\$folder\*") {
                return $false
            }
        }

        return $true
    } | ForEach-Object {
        $targetPath = $_.FullName.Replace($source, $destination)
        
        if ($_.PSIsContainer) {
            if (-not (Test-Path $targetPath)) {
                New-Item -ItemType Directory -Path $targetPath | Out-Null
            }
        } 
        else {
            Remove-Item -Path $_.FullName -Force
        }
    }

    LogMessage -msg "Deleted current version"
}

function ExitWithError {
    param ([System.String]$msg) 
    LogMessage -msg $msg -level "ERROR"
    Exit 1
}
function GetCurrentVersion {
    $pathToExe = "$global:scriptRoot\$global:serviceExecutableName"
    if (!(Test-Path -Path $pathToExe)) {
        LogMessage -msg "File '$pathToExe' does not exist" -level "WARN"
        return "0.0.0"
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pathToExe)
    $versionInstalled = $versionInfo.FileVersion
    LogMessage -msg "Version installed: $versionInstalled"
    return [System.Version]::Parse($versionInstalled).ToString(3)
}

function CreateTempFolder {
    $path = $global:tempFolder
    if (-Not (Test-Path -Path $path)) {
        New-Item -Path $path -ItemType Directory | Out-Null
        LogMessage -msg "Created temporary folder '$global:tempFolder'"
    }
}

function DownloadRelease {
    param ([System.String]$url, [System.String]$fileName) 

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

    # remove the folder if it exists
    if (Test-Path -Path $path) {
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }

    Expand-Archive -Path $zipPath -DestinationPath $path
    LogMessage -msg "Release unzipped to '$global:tempExtractionFolder'"
}

function GetLatestVersion {
    
    $data = Invoke-RestMethod -Uri $global:latestVersionUrl

    $asset = $data.assets | Where-Object { $_.name -like "*webconsole*" }

    $result = [PSCustomObject]@{
        version = $data.tag_name.Trim().TrimStart('v')
        fileName = $asset.name
        downloadUrl = $asset.browser_download_url
    }    
    LogMessage -msg "Latest version available: $($result.version)"
    return $result
}

function IsLatestVersionInstalled {
    param ([System.String]$installedVersion, [System.String]$latestVersion) 

    $vInstalled = [System.Version]::Parse($installedVersion);
    $vLatest = [System.Version]::Parse($latestVersion);
    $result = ($vInstalled -eq $vLatest)

    if ($result) {
        LogMessage -msg "The latest version is already installed" -level "WARN"
    }
    return $result
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
    Add-Content -Path "$scriptRoot\updates.log" -Value "$(Get-Date)|$level|$msg"
}

function RemoveTempFolder {
    $path = $global:tempFolder
    if (Test-Path -Path $path) {
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        LogMessage -msg "Temp folder '$path' deleted."

    }
}

function RemoveLegacyFolder {
    param ([System.String]$folder) 

    $path = "$global:scriptRoot\$folder"
    if (Test-Path -Path $path) {
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        LogMessage -msg "Legacy folder '$path' deleted."
    }
}

function StartService {

    $service = Get-Service -Name $global:serviceName -ErrorAction SilentlyContinue
    $exePath = "$global:scriptRoot\$global:serviceExecutableName"

    if (!$service) {
        # sc.exe create $global:serviceName binPath= "`"$exePath`"" DisplayName= "`"$global:serviceName`"" start= auto
        # & $global:serviceExecutableName install
        Start-Process -FilePath $exePath -ArgumentList "install" -Wait
        LogMessage -msg "Service '$global:serviceName' installed."
    } 

    # Start-Service -Name $serviceName
    #& $global:serviceExecutableName start
    Start-Process -FilePath $exePath -ArgumentList "start" -Wait
    LogMessage -msg "Service '$global:serviceName' started."
}

function StopService {
    $service = Get-Service -Name $global:serviceName -ErrorAction SilentlyContinue

    if ($service) {
        if ($service.Status -ne 'Stopped') {
            LogMessage -msg "Stopping service '$global:serviceName'"
            try {
                # Stop-Service -Name $global:serviceName -Force
                & $global:serviceExecutableName stop
                LogMessage -msg "Stopped service '$global:serviceName'"
            }
            catch 
            {
                $ex = $_.Exception
                throw "Failed to stop service: $($ex.Message) - make sure you are running in Administrator mode"
            }
        } 
        else 
        {
            LogMessage -msg "Service '$global:serviceName' is already stopped."
        }
    } 
    else {
        LogMessage -msg "Service '$global:serviceName' is not installed."
    }    
}

function Run {
    try {
        LogMessage -msg "----------------------------------------------------------------------"
        LogMessage -msg "Starting update initiated by user '$Env:UserName'"
        LogMessage -msg "----------------------------------------------------------------------"

        # get installed version
        $versionInstalled = GetCurrentVersion

        # get latest version - includes "version,fileName,downloadUrl"
        $latestVersionInfo = GetLatestVersion
        $versionLatest = $latestVersionInfo.version

        # if versions match - print and exit
        if (IsLatestVersionInstalled -installedVersion $versionInstalled -latestVersion $versionLatest) {
            Exit
        }

        # create temp folder for update
        CreateTempFolder

        # download latest version if it has not been downloaded already
        $zipPath = DownloadRelease -url $latestVersionInfo.downloadUrl -fileName $latestVersionInfo.fileName

        # unzip latest version
        ExtractRelease -zipPath $zipPath

        # back up current service files into zip
        BackupCurrent -currentVersion $versionInstalled
        
        # top the service - don't uninstall it as we don't want to lose credentials
        StopService

        # delete current service files
        DeleteCurrent

        # copy new service files into folder
        CopyNewVersion

        # install the service if it is not installed
        StartService

        # clean up temporary folder - delete everything in it and remove the folder
        RemoveTempFolder

        # remove legacy folders if they are still hanging around
        RemoveLegacyFolder -folder "Updater"
    }
    catch {
        $ex = $_.Exception

        ExitWithError("An error occurred: $($ex.Message)")
    }

}

Run