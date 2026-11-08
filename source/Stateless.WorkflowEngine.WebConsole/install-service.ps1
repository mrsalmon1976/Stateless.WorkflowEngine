<#
.SYNOPSIS
    Installs (or uninstalls) the Stateless Workflow Engine web console as a Windows service.

.DESCRIPTION
    Run from the console's install folder in an elevated PowerShell prompt.  The console requires the
    ASP.NET Core 10 runtime (https://dotnet.microsoft.com/download/dotnet/10.0).

.EXAMPLE
    .\install-service.ps1
    .\install-service.ps1 -Uninstall
#>
param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$serviceName = "Stateless.WorkflowEngine.Console"
$displayName = "Stateless Workflow Engine Console"
$exePath = Join-Path $PSScriptRoot "Stateless.WorkflowEngine.WebConsole.exe"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This script must be run as an Administrator"
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($Uninstall) {
    if (!$service) {
        Write-Host "Service '$serviceName' is not installed"
        exit 0
    }
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
    }
    sc.exe delete $serviceName | Out-Null
    Write-Host "Service '$serviceName' uninstalled"
    exit 0
}

if (!(Test-Path -Path $exePath)) {
    throw "Unable to find '$exePath'"
}

if ($service) {
    Write-Host "Service '$serviceName' is already installed"
}
else {
    New-Service -Name $serviceName -BinaryPathName "`"$exePath`"" -DisplayName $displayName -Description "Web console for monitoring Stateless.WorkflowEngine workflow stores" -StartupType Automatic | Out-Null
    Write-Host "Service '$serviceName' installed"
}

Start-Service -Name $serviceName
Write-Host "Service '$serviceName' started - browse to the url configured in appsettings.json (default port 8811, e.g. http://localhost:8811) - open the port in Windows Firewall to allow access from other machines"
