# Install Windows Service Script
# Run this script to install the PDFA Conversion Service as a Windows Service

param(
    [Parameter(Mandatory=$true)]
    [string]$ServicePath,
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "PDFAConversionService",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceAccount = "NT AUTHORITY\NETWORK SERVICE",
    
    [Parameter(Mandatory=$false)]
    [string]$DisplayName = "PDFA Conversion Service",
    
    [Parameter(Mandatory=$false)]
    [string]$Description = "Converts PDF files to PDF/A-1b format using Ghostscript"
)

$ErrorActionPreference = "Stop"

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This script must be run as Administrator to install Windows Service"
}

$exePath = Join-Path $ServicePath "PDFAConversionService.exe"

if (-not (Test-Path $exePath)) {
    throw "Service executable not found at: $exePath"
}

Write-Host "========================================="
Write-Host "Installing Windows Service"
Write-Host "========================================="
Write-Host "Service Name: $ServiceName"
Write-Host "Service Path: $ServicePath"
Write-Host "Executable: $exePath"
Write-Host "Service Account: $ServiceAccount"
Write-Host ""

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Service '$ServiceName' already exists." -ForegroundColor Yellow
    $response = Read-Host "Do you want to update it? (Y/N)"
    if ($response -ne 'Y' -and $response -ne 'y') {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    # Stop service if running
    if ($existingService.Status -eq 'Running') {
        Write-Host "Stopping service..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 3
    }
    
    # Update service configuration
    Write-Host "Updating service configuration..." -ForegroundColor Cyan
    sc.exe config $ServiceName binPath= "`"$exePath`""
    sc.exe config $ServiceName obj= $ServiceAccount
    sc.exe config $ServiceName start= auto
    sc.exe config $ServiceName DisplayName= $DisplayName
    sc.exe description $ServiceName $Description
    
    # Configure failure recovery: auto-restart on failure
    Write-Host "Configuring automatic restart on failure..." -ForegroundColor Cyan
    # Reset failure count after 1 day (86400 seconds)
    # Actions: restart after 60 seconds on first failure, restart after 60 seconds on second failure, restart after 60 seconds on subsequent failures
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    
    Write-Host "Service updated successfully." -ForegroundColor Green
} else {
    # Create new service
    Write-Host "Creating new service..." -ForegroundColor Cyan
    $result = sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create service. Error: $result"
    }
    
    # Configure service
    sc.exe config $ServiceName obj= $ServiceAccount
    sc.exe config $ServiceName DisplayName= $DisplayName
    sc.exe description $ServiceName $Description
    
    # Configure failure recovery: auto-restart on failure
    Write-Host "Configuring automatic restart on failure..." -ForegroundColor Cyan
    # Reset failure count after 1 day (86400 seconds)
    # Actions: restart after 60 seconds on first failure, restart after 60 seconds on second failure, restart after 60 seconds on subsequent failures
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    
    Write-Host "Service created successfully." -ForegroundColor Green
}

# Start service
Write-Host "`nStarting service..." -ForegroundColor Cyan
Start-Service -Name $ServiceName
Start-Sleep -Seconds 5

$service = Get-Service -Name $ServiceName
if ($service.Status -eq 'Running') {
    Write-Host "Service started successfully!" -ForegroundColor Green
    Write-Host "`nService Status: $($service.Status)" -ForegroundColor Green
    Write-Host "Start Type: $($service.StartType)" -ForegroundColor Green
    Write-Host "`nService Configuration:" -ForegroundColor Cyan
    Write-Host "  - Automatic startup: Enabled (starts with Windows)" -ForegroundColor Gray
    Write-Host "  - Auto-restart on failure: Enabled (restarts after 60 seconds)" -ForegroundColor Gray
    Write-Host "`nService can be managed using:" -ForegroundColor Cyan
    Write-Host "  Get-Service $ServiceName" -ForegroundColor Gray
    Write-Host "  Start-Service $ServiceName" -ForegroundColor Gray
    Write-Host "  Stop-Service $ServiceName" -ForegroundColor Gray
} else {
    Write-Warning "Service failed to start. Status: $($service.Status)"
    Write-Host "Check event logs for details:" -ForegroundColor Yellow
    Write-Host "  Get-EventLog -LogName Application -Source '$ServiceName' -Newest 10" -ForegroundColor Gray
    exit 1
}

Write-Host "`n========================================="
Write-Host "Installation completed!" -ForegroundColor Green
Write-Host "========================================="

