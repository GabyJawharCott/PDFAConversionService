# Pre-Deployment Server Setup Script
# Run this script ONCE on the Dev server before first deployment

param(
    [Parameter(Mandatory=$false)]
    [string]$ServicePath = "C:\Services\PDFAConversionService",
    
    [Parameter(Mandatory=$false)]
    [string]$TempDirectory = "C:\Temp\PdfaConversion",
    
    [Parameter(Mandatory=$false)]
    [string]$ServiceAccount = "NT AUTHORITY\NETWORK SERVICE",
    
    [Parameter(Mandatory=$false)]
    [string]$GhostscriptPath = "C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================="
Write-Host "PDFA Conversion Service - Pre-Deployment Setup"
Write-Host "========================================="

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Some operations require Administrator privileges. Run as Administrator for full setup."
}

# 1. Check .NET Runtime
Write-Host "`n[1/6] Checking .NET Runtime..." -ForegroundColor Cyan
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET Runtime: $dotnetVersion" -ForegroundColor Green
    
    # Check if it's .NET 10.0
    if ($dotnetVersion -notmatch "^10\.") {
        Write-Warning ".NET 10.0 is recommended. Current version: $dotnetVersion"
    }
} catch {
    Write-Error ".NET Runtime not found. Please install .NET 10.0 Runtime from https://dotnet.microsoft.com/download"
    exit 1
}

# 2. Check Ghostscript
Write-Host "`n[2/6] Checking Ghostscript..." -ForegroundColor Cyan
if (Test-Path $GhostscriptPath) {
    Write-Host "✓ Ghostscript found: $GhostscriptPath" -ForegroundColor Green
    
    # Try to get version
    try {
        $gsVersion = & $GhostscriptPath --version 2>&1 | Select-Object -First 1
        Write-Host "  Version: $gsVersion" -ForegroundColor Gray
    } catch {
        Write-Host "  (Could not determine version)" -ForegroundColor Gray
    }
} else {
    Write-Warning "Ghostscript not found at: $GhostscriptPath"
    Write-Host "Please install Ghostscript from: https://www.ghostscript.com/download/gsdnld.html" -ForegroundColor Yellow
    Write-Host "Or update the GhostscriptPath parameter if installed elsewhere." -ForegroundColor Yellow
}

# 3. Create Service Directory
Write-Host "`n[3/6] Creating service directory..." -ForegroundColor Cyan
if (-not (Test-Path $ServicePath)) {
    New-Item -ItemType Directory -Path $ServicePath -Force | Out-Null
    Write-Host "✓ Service directory created: $ServicePath" -ForegroundColor Green
} else {
    Write-Host "✓ Service directory exists: $ServicePath" -ForegroundColor Green
}

# 4. Create Temp Directory
Write-Host "`n[4/6] Creating temp directory..." -ForegroundColor Cyan
if (-not (Test-Path $TempDirectory)) {
    New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
    Write-Host "✓ Temp directory created: $TempDirectory" -ForegroundColor Green
} else {
    Write-Host "✓ Temp directory exists: $TempDirectory" -ForegroundColor Green
}

# 5. Set Permissions
Write-Host "`n[5/6] Setting permissions..." -ForegroundColor Cyan
if ($isAdmin) {
    # Temp directory permissions
    try {
        icacls $TempDirectory /grant "${ServiceAccount}:(OI)(CI)M" /inheritance:r | Out-Null
        Write-Host "✓ Temp directory permissions set for $ServiceAccount" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to set temp directory permissions: $_"
    }
    
    # Service directory permissions
    try {
        icacls $ServicePath /grant "${ServiceAccount}:(OI)(CI)RX" /inheritance:r | Out-Null
        Write-Host "✓ Service directory permissions set for $ServiceAccount" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to set service directory permissions: $_"
    }
} else {
    Write-Warning "Skipping permission setup (requires Administrator privileges)"
    Write-Host "Run this command manually:" -ForegroundColor Yellow
    Write-Host "  icacls `"$TempDirectory`" /grant `"${ServiceAccount}:(OI)(CI)M`" /inheritance:r" -ForegroundColor Gray
    Write-Host "  icacls `"$ServicePath`" /grant `"${ServiceAccount}:(OI)(CI)RX`" /inheritance:r" -ForegroundColor Gray
}

# 6. Verify Setup
Write-Host "`n[6/6] Verifying setup..." -ForegroundColor Cyan
$allGood = $true

if (-not (Test-Path $ServicePath)) {
    Write-Error "Service directory not found: $ServicePath"
    $allGood = $false
}

if (-not (Test-Path $TempDirectory)) {
    Write-Error "Temp directory not found: $TempDirectory"
    $allGood = $false
}

if (-not (Test-Path $GhostscriptPath)) {
    Write-Warning "Ghostscript not found: $GhostscriptPath"
    $allGood = $false
}

if ($allGood) {
    Write-Host "`n========================================="
    Write-Host "Pre-deployment setup completed!" -ForegroundColor Green
    Write-Host "========================================="
    Write-Host "Service Path: $ServicePath"
    Write-Host "Temp Directory: $TempDirectory"
    Write-Host "Ghostscript: $GhostscriptPath"
    Write-Host "`nYou can now proceed with deployment." -ForegroundColor Green
} else {
    Write-Host "`n========================================="
    Write-Host "Setup completed with warnings." -ForegroundColor Yellow
    Write-Host "Please resolve the issues above before deployment." -ForegroundColor Yellow
    Write-Host "========================================="
    exit 1
}

