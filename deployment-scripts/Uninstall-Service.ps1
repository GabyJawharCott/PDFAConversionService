# Uninstall Windows Service Script
# Run this script to remove the PDFA Conversion Service
#
# Usage:
#   .\Uninstall-Service.ps1
#   .\Uninstall-Service.ps1 -ServiceName "PDFAConversionService"
#   .\Uninstall-Service.ps1 -Force  # Skip confirmation
#
# Alternative: You can also use the built-in command-line uninstaller:
#   PDFAConversionService.exe uninstall

[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Mandatory=$false, HelpMessage="Windows Service name to uninstall")]
    [string]$ServiceName = "PDFAConversionService",
    
    [Parameter(Mandatory=$false, HelpMessage="Skip confirmation prompt")]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Function to write colored output
function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "WARNING: $Message" -ForegroundColor Yellow
}

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator to uninstall Windows Service"
    Write-Host ""
    Write-Host "To run as Administrator:" -ForegroundColor Yellow
    Write-Host "  1. Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Gray
    Write-Host "  2. Or use: Start-Process powershell -Verb RunAs" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Alternative: Use the built-in uninstaller:" -ForegroundColor Cyan
    Write-Host "  PDFAConversionService.exe uninstall" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Uninstalling Windows Service" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Service Name: $ServiceName" -ForegroundColor White
Write-Host ""

# Check if service exists
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if (-not $service) {
    Write-Host "Service '$ServiceName' not found." -ForegroundColor Yellow
    Write-Host "Nothing to uninstall." -ForegroundColor Gray
    exit 0
}

# Show service information
Write-Host "Service Information:" -ForegroundColor Cyan
Write-Host "  Name:        $($service.Name)" -ForegroundColor Gray
Write-Host "  Display:     $($service.DisplayName)" -ForegroundColor Gray
Write-Host "  Status:      $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') { 'Yellow' } else { 'Gray' })
Write-Host "  Start Type:  $($service.StartType)" -ForegroundColor Gray
Write-Host ""

# Confirm uninstallation
if (-not $Force) {
    if (-not $PSCmdlet.ShouldProcess("Service '$ServiceName'", "Uninstall")) {
        Write-Host "Uninstallation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Stop service if running
if ($service.Status -eq 'Running') {
    Write-Step "Stopping service..."
    try {
        Stop-Service -Name $ServiceName -Force -ErrorAction Stop
        
        # Wait for service to stop (with timeout)
        $timeout = 30
        $elapsed = 0
        while ($service.Status -ne 'Stopped' -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $service.Refresh()
            $elapsed++
        }
        
        if ($service.Status -eq 'Stopped') {
            Write-Success "Service stopped successfully."
        } else {
            Write-Warning "Service did not stop within $timeout seconds. Status: $($service.Status)"
            Write-Host "Attempting to remove service anyway..." -ForegroundColor Yellow
        }
    } catch {
        Write-Warning "Failed to stop service: $_"
        Write-Host "Attempting to remove service anyway..." -ForegroundColor Yellow
    }
}

# Delete service
Write-Step "Removing service..."
try {
    $result = sc.exe delete $ServiceName 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Service removed successfully!"
        
        # Verify removal
        Start-Sleep -Seconds 2
        $verifyService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($verifyService) {
            Write-Warning "Service still exists after removal attempt. You may need to restart the system."
        } else {
            Write-Success "Service removal verified."
        }
    } else {
        $errorOutput = $result -join "`n"
        throw "Failed to remove service. Exit code: $LASTEXITCODE`n$errorOutput"
    }
} catch {
    Write-Error "Failed to remove service: $_"
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Ensure the service is stopped" -ForegroundColor Gray
    Write-Host "  2. Check if any processes are using the service" -ForegroundColor Gray
    Write-Host "  3. Try restarting the system and run this script again" -ForegroundColor Gray
    Write-Host "  4. Use: PDFAConversionService.exe uninstall" -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Success "Uninstallation completed!"
Write-Host "=========================================" -ForegroundColor Cyan
