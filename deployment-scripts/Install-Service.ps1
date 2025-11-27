# Install Windows Service Script
# Run this script to install the PDFA Conversion Service as a Windows Service
#
# Usage:
#   .\Install-Service.ps1 -ServicePath "C:\Services\PDFAConversionService"
#   .\Install-Service.ps1 -ServicePath "C:\Services\PDFAConversionService" -ServiceAccount "DOMAIN\ServiceAccount"
#
# Alternative: You can also use the built-in command-line installer:
#   PDFAConversionService.exe install
#   PDFAConversionService.exe install --account "DOMAIN\ServiceAccount"

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, HelpMessage="Path to the service installation directory")]
    [ValidateScript({
        if (-not (Test-Path $_)) {
            throw "Service path does not exist: $_"
        }
        $true
    })]
    [string]$ServicePath,
    
    [Parameter(Mandatory=$false, HelpMessage="Windows Service name")]
    [string]$ServiceName = "PDFAConversionService",
    
    [Parameter(Mandatory=$false, HelpMessage="Service account to run the service (e.g., 'NT AUTHORITY\NETWORK SERVICE' or 'DOMAIN\ServiceAccount')")]
    [string]$ServiceAccount = "NT AUTHORITY\NETWORK SERVICE",
    
    [Parameter(Mandatory=$false, HelpMessage="Display name for the service")]
    [string]$DisplayName = "PDFA Conversion Service",
    
    [Parameter(Mandatory=$false, HelpMessage="Service description")]
    [string]$Description = "Converts PDF files to PDF/A-1b format using Ghostscript",
    
    [Parameter(Mandatory=$false, HelpMessage="Skip starting the service after installation")]
    [switch]$SkipStart
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
    Write-Error "This script must be run as Administrator to install Windows Service"
    Write-Host ""
    Write-Host "To run as Administrator:" -ForegroundColor Yellow
    Write-Host "  1. Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Gray
    Write-Host "  2. Or use: Start-Process powershell -Verb RunAs" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Alternative: Use the built-in installer:" -ForegroundColor Cyan
    Write-Host "  PDFAConversionService.exe install" -ForegroundColor Gray
    exit 1
}

# Validate service path
if (-not (Test-Path $ServicePath)) {
    Write-Error "Service path does not exist: $ServicePath"
    exit 1
}

$exePath = Join-Path $ServicePath "PDFAConversionService.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Service executable not found at: $exePath"
    Write-Host ""
    Write-Host "Please ensure:" -ForegroundColor Yellow
    Write-Host "  1. The service has been built and published" -ForegroundColor Gray
    Write-Host "  2. All files are copied to: $ServicePath" -ForegroundColor Gray
    exit 1
}

# Validate .NET runtime
Write-Step "Checking .NET runtime..."
try {
    $dotnetVersion = dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning ".NET runtime check failed. Service may not start if .NET is not installed."
    } else {
        Write-Host "  .NET Version: $dotnetVersion" -ForegroundColor Gray
    }
} catch {
    Write-Warning "Could not verify .NET installation: $_"
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Installing Windows Service" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Service Name:      $ServiceName" -ForegroundColor White
Write-Host "Service Path:      $ServicePath" -ForegroundColor White
Write-Host "Executable:        $exePath" -ForegroundColor White
Write-Host "Service Account:   $ServiceAccount" -ForegroundColor White
Write-Host ""

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Warning "Service '$ServiceName' already exists."
    
    if (-not $PSCmdlet.ShouldContinue("Do you want to update the existing service?", "Service Update")) {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
    
    # Stop service if running
    if ($existingService.Status -eq 'Running') {
        Write-Step "Stopping existing service..."
        try {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            $timeout = 30
            $elapsed = 0
            while ($existingService.Status -ne 'Stopped' -and $elapsed -lt $timeout) {
                Start-Sleep -Seconds 1
                $existingService.Refresh()
                $elapsed++
            }
            if ($existingService.Status -eq 'Stopped') {
                Write-Success "Service stopped successfully."
            } else {
                Write-Error "Service did not stop within $timeout seconds. Status: $($existingService.Status)"
                exit 1
            }
        } catch {
            Write-Error "Failed to stop service: $_"
            exit 1
        }
    }
    
    # Update service configuration
    Write-Step "Updating service configuration..."
    try {
        $result = sc.exe config $ServiceName binPath= "`"$exePath`""
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update binary path. Exit code: $LASTEXITCODE"
        }
        
        $result = sc.exe config $ServiceName obj= $ServiceAccount
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to set service account. Exit code: $LASTEXITCODE"
        }
        
        sc.exe config $ServiceName start= auto | Out-Null
        sc.exe config $ServiceName DisplayName= $DisplayName | Out-Null
        sc.exe description $ServiceName $Description | Out-Null
        
        # Configure failure recovery: auto-restart on failure
        Write-Step "Configuring automatic restart on failure..."
        sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
        
        Write-Success "Service updated successfully."
    } catch {
        Write-Error "Failed to update service: $_"
        exit 1
    }
} else {
    # Create new service
    Write-Step "Creating new service..."
    try {
        $result = sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto
        
        if ($LASTEXITCODE -ne 0) {
            $errorOutput = $result -join "`n"
            throw "Failed to create service. Exit code: $LASTEXITCODE`n$errorOutput"
        }
        
        Write-Success "Service created successfully."
        
        # Configure service
        Write-Step "Configuring service settings..."
        $result = sc.exe config $ServiceName obj= $ServiceAccount
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to set service account. Exit code: $LASTEXITCODE"
        }
        
        sc.exe config $ServiceName DisplayName= $DisplayName | Out-Null
        sc.exe description $ServiceName $Description | Out-Null
        
        # Configure failure recovery: auto-restart on failure
        Write-Step "Configuring automatic restart on failure..."
        sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
        
        Write-Success "Service configuration completed."
    } catch {
        Write-Error "Failed to create service: $_"
        exit 1
    }
}

# Start service (unless skipped)
if (-not $SkipStart) {
    Write-Host ""
    Write-Step "Starting service..."
    try {
        Start-Service -Name $ServiceName -ErrorAction Stop
        
        # Wait for service to start (with timeout)
        $timeout = 30
        $elapsed = 0
        $service = Get-Service -Name $ServiceName
        while ($service.Status -ne 'Running' -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $service.Refresh()
            $elapsed++
        }
        
        if ($service.Status -eq 'Running') {
            Write-Success "Service started successfully!"
        } else {
            Write-Warning "Service did not start within $timeout seconds. Status: $($service.Status)"
            Write-Host "You can start it manually later using:" -ForegroundColor Yellow
            Write-Host "  Start-Service $ServiceName" -ForegroundColor Gray
            Write-Host "  Or: PDFAConversionService.exe start" -ForegroundColor Gray
        }
    } catch {
        Write-Warning "Failed to start service: $_"
        Write-Host "You can start it manually later using:" -ForegroundColor Yellow
        Write-Host "  Start-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Or: PDFAConversionService.exe start" -ForegroundColor Gray
    }
} else {
    Write-Host ""
    Write-Host "Service installation completed. Service will start automatically on next reboot." -ForegroundColor Yellow
}

# Show service information
Write-Host ""
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Service Information:" -ForegroundColor Cyan
    Write-Host "  Name:        $($service.Name)" -ForegroundColor Gray
    Write-Host "  Display:     $($service.DisplayName)" -ForegroundColor Gray
    Write-Host "  Status:      $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') { 'Green' } else { 'Yellow' })
    Write-Host "  Start Type:  $($service.StartType)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Service Configuration:" -ForegroundColor Cyan
    Write-Host "  - Automatic startup: Enabled (starts with Windows)" -ForegroundColor Gray
    Write-Host "  - Auto-restart on failure: Enabled (restarts after 60 seconds)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Service Management:" -ForegroundColor Cyan
    Write-Host "  PowerShell:" -ForegroundColor Gray
    Write-Host "    Get-Service $ServiceName" -ForegroundColor DarkGray
    Write-Host "    Start-Service $ServiceName" -ForegroundColor DarkGray
    Write-Host "    Stop-Service $ServiceName" -ForegroundColor DarkGray
    Write-Host "  Command Line:" -ForegroundColor Gray
    Write-Host "    PDFAConversionService.exe start" -ForegroundColor DarkGray
    Write-Host "    PDFAConversionService.exe stop" -ForegroundColor DarkGray
    Write-Host "    PDFAConversionService.exe status" -ForegroundColor DarkGray
}

# Check for startup errors
if ($service -and $service.Status -ne 'Running') {
    Write-Host ""
    Write-Warning "Service is not running. Checking event logs..."
    try {
        $events = Get-EventLog -LogName Application -Source $ServiceName -Newest 5 -ErrorAction SilentlyContinue
        if ($events) {
            Write-Host "Recent service events:" -ForegroundColor Yellow
            $events | ForEach-Object {
                $color = if ($_.EntryType -eq 'Error') { 'Red' } elseif ($_.EntryType -eq 'Warning') { 'Yellow' } else { 'Gray' }
                Write-Host "  [$($_.TimeGenerated)] $($_.EntryType): $($_.Message)" -ForegroundColor $color
            }
        } else {
            Write-Host "  No recent events found." -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Could not read event logs: $_" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Success "Installation completed!"
Write-Host "=========================================" -ForegroundColor Cyan

