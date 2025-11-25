# Uninstall Windows Service Script
# Run this script to remove the PDFA Conversion Service

param(
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "PDFAConversionService"
)

$ErrorActionPreference = "Stop"

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This script must be run as Administrator to uninstall Windows Service"
}

Write-Host "========================================="
Write-Host "Uninstalling Windows Service"
Write-Host "========================================="
Write-Host "Service Name: $ServiceName"
Write-Host ""

# Check if service exists
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if (-not $service) {
    Write-Host "Service '$ServiceName' not found." -ForegroundColor Yellow
    exit 0
}

Write-Host "Service Status: $($service.Status)" -ForegroundColor Cyan

# Stop service if running
if ($service.Status -eq 'Running') {
    Write-Host "Stopping service..." -ForegroundColor Cyan
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 5
    Write-Host "Service stopped." -ForegroundColor Green
}

# Delete service
Write-Host "Removing service..." -ForegroundColor Cyan
$result = sc.exe delete $ServiceName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service removed successfully!" -ForegroundColor Green
} else {
    Write-Error "Failed to remove service. Error: $result"
    exit 1
}

Write-Host "`n========================================="
Write-Host "Uninstallation completed!" -ForegroundColor Green
Write-Host "========================================="

