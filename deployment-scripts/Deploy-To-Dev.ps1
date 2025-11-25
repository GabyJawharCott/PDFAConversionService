# Deployment Script for PDFA Conversion Service - Dev Environment
# This script can be used as a standalone deployment or integrated into TFS/Azure DevOps

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$ServicePath = "C:\Services\PDFAConversionService",
    
    [Parameter(Mandatory=$false)]
    [string]$SourcePath = ".\publish",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceAccount = "NT AUTHORITY\NETWORK SERVICE",
    
    [Parameter(Mandatory=$false)]
    [string]$TempDirectory = "C:\Temp\PdfaConversion",
    
    [Parameter(Mandatory=$false)]
    [int]$Port = 7015
)

$ErrorActionPreference = "Stop"
$serviceName = "PDFAConversionService"

Write-Host "========================================="
Write-Host "PDFA Conversion Service Deployment"
Write-Host "Server: $ServerName"
Write-Host "========================================="

# Step 1: Pre-Deployment Checks
Write-Host "`n[1/8] Running pre-deployment checks..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($tempDir, $gsPath)
    
    # Check .NET Runtime
    try {
        $dotnetVersion = dotnet --version
        Write-Host "✓ .NET Runtime: $dotnetVersion" -ForegroundColor Green
    } catch {
        throw "✗ .NET Runtime not found. Please install .NET 10.0 Runtime."
    }
    
    # Check Ghostscript
    if (Test-Path $gsPath) {
        Write-Host "✓ Ghostscript found: $gsPath" -ForegroundColor Green
    } else {
        throw "✗ Ghostscript not found at $gsPath"
    }
    
    # Create temp directory
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        Write-Host "✓ Temp directory created: $tempDir" -ForegroundColor Green
    } else {
        Write-Host "✓ Temp directory exists: $tempDir" -ForegroundColor Green
    }
    
} -ArgumentList $TempDirectory, "C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe"

# Step 2: Stop Service
Write-Host "`n[2/8] Stopping service..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($name)
    $service = Get-Service -Name $name -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq 'Running') {
        Stop-Service -Name $name -Force
        Start-Sleep -Seconds 5
        Write-Host "✓ Service stopped" -ForegroundColor Green
    } else {
        Write-Host "ℹ Service not running" -ForegroundColor Yellow
    }
} -ArgumentList $serviceName

# Step 3: Backup Current Version
Write-Host "`n[3/8] Backing up current version..." -ForegroundColor Cyan
$backupPath = "$ServicePath.backup.$(Get-Date -Format 'yyyyMMddHHmmss')"
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($source, $backup)
    if (Test-Path $source) {
        Copy-Item -Path $source -Destination $backup -Recurse -Force
        Write-Host "✓ Backup created: $backup" -ForegroundColor Green
    }
} -ArgumentList $ServicePath, $backupPath

# Step 4: Copy Files
Write-Host "`n[4/8] Copying files to server..." -ForegroundColor Cyan
if (-not (Test-Path $SourcePath)) {
    throw "Source path not found: $SourcePath"
}

# Create destination directory
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($path)
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
} -ArgumentList $ServicePath

# Copy files
Copy-Item -Path "$SourcePath\*" -Destination "\\$ServerName\C$\Services\PDFAConversionService\" -Recurse -Force
Write-Host "✓ Files copied successfully" -ForegroundColor Green

# Step 5: Set Permissions
Write-Host "`n[5/8] Setting permissions..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($tempDir, $account, $servicePath)
    
    # Temp directory permissions
    if (Test-Path $tempDir) {
        icacls $tempDir /grant "${account}:(OI)(CI)M" /inheritance:r | Out-Null
        Write-Host "✓ Temp directory permissions set" -ForegroundColor Green
    }
    
    # Service directory permissions
    if (Test-Path $servicePath) {
        icacls $servicePath /grant "${account}:(OI)(CI)RX" /inheritance:r | Out-Null
        Write-Host "✓ Service directory permissions set" -ForegroundColor Green
    }
} -ArgumentList $TempDirectory, $ServiceAccount, $ServicePath

# Step 6: Install/Update Service
Write-Host "`n[6/8] Installing/updating service..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($name, $exePath, $account)
    
    $service = Get-Service -Name $name -ErrorAction SilentlyContinue
    
    if ($service) {
        # Update existing service
        sc.exe config $name binPath= "`"$exePath`"" | Out-Null
        sc.exe config $name obj= $account | Out-Null
        sc.exe config $name start= auto | Out-Null
        # Configure failure recovery: auto-restart on failure
        sc.exe failure $name reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
        Write-Host "✓ Service updated" -ForegroundColor Green
    } else {
        # Create new service
        sc.exe create $name binPath= "`"$exePath`"" start= auto | Out-Null
        sc.exe config $name obj= $account | Out-Null
        sc.exe config $name DisplayName= "PDFA Conversion Service" | Out-Null
        sc.exe description $name "Converts PDF files to PDF/A-1b format using Ghostscript" | Out-Null
        # Configure failure recovery: auto-restart on failure
        sc.exe failure $name reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
        Write-Host "✓ Service created" -ForegroundColor Green
    }
} -ArgumentList $serviceName, "$ServicePath\PDFAConversionService.exe", $ServiceAccount

# Step 7: Start Service
Write-Host "`n[7/8] Starting service..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($name)
    Start-Service -Name $name
    Start-Sleep -Seconds 10
    
    $service = Get-Service -Name $name
    if ($service.Status -eq 'Running') {
        Write-Host "✓ Service started successfully" -ForegroundColor Green
    } else {
        throw "✗ Service failed to start. Status: $($service.Status)"
    }
} -ArgumentList $serviceName

# Step 8: Verify Deployment
Write-Host "`n[8/8] Verifying deployment..." -ForegroundColor Cyan
Invoke-Command -ComputerName $ServerName -ScriptBlock {
    param($name, $port)
    
    # Check service status
    $service = Get-Service -Name $name
    if ($service.Status -ne 'Running') {
        throw "Service is not running. Status: $($service.Status)"
    }
    Write-Host "✓ Service status: $($service.Status)" -ForegroundColor Green
    
    # Check health endpoint
    $healthUrl = "http://localhost:$port/api/PdfaConversion/health"
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 30
        if ($response.StatusCode -eq 200) {
            Write-Host "✓ Health check passed" -ForegroundColor Green
            Write-Host "  Response: $($response.Content)" -ForegroundColor Gray
        } else {
            throw "Health check returned status: $($response.StatusCode)"
        }
    } catch {
        Write-Warning "Health check failed: $_"
        # Show recent event log entries
        $events = Get-EventLog -LogName Application -Source $name -Newest 5 -ErrorAction SilentlyContinue
        if ($events) {
            Write-Host "Recent event log entries:" -ForegroundColor Yellow
            foreach ($event in $events) {
                Write-Host "  [$($event.TimeGenerated)] $($event.Message)" -ForegroundColor Gray
            }
        }
        throw
    }
} -ArgumentList $serviceName, $Port

Write-Host "`n========================================="
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "========================================="
Write-Host "Service: $serviceName"
Write-Host "Server: $ServerName"
Write-Host "Path: $ServicePath"
Write-Host "Port: $Port"
Write-Host "Health: http://$ServerName`:$Port/api/PdfaConversion/health"

