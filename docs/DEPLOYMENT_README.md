# Deployment Guide - Quick Start

This guide provides a quick reference for deploying the PDFA Conversion Service.

## 🚀 Quick Deployment Steps

### 1. Pre-Deployment (One-Time Setup)

Run on the target server:
```powershell
.\deployment-scripts\Pre-Deployment-Setup.ps1
```

Or manually:
```powershell
# Install .NET 10.0 Runtime
# Install Ghostscript
# Create directories
New-Item -ItemType Directory -Path "C:\Services\PDFAConversionService" -Force
New-Item -ItemType Directory -Path "C:\Temp\PdfaConversion" -Force

# Set permissions
icacls "C:\Temp\PdfaConversion" /grant "NETWORK SERVICE:(OI)(CI)M" /inheritance:r
```

### 2. Build and Deploy

#### Option A: Using TFS/Azure DevOps
1. Push code to repository
2. Build pipeline will automatically build and test
3. Create release and deploy to environment

#### Option B: Manual Deployment
```powershell
# Build
dotnet publish PDFAConversionService/PDFAConversionService.csproj -c Release -o publish

# Deploy
.\deployment-scripts\Deploy-To-Dev.ps1 -ServerName "dev-server" -ServicePath "C:\Services\PDFAConversionService"
```

### 3. Verify Deployment

```powershell
# Check service
Get-Service PDFAConversionService

# Test health
Invoke-WebRequest -Uri "http://localhost:7015/api/PdfaConversion/health"

# Check logs
Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 10
```

## 📁 Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development environment
- `appsettings.Production.json` - Production environment

Configuration is environment-aware and will automatically load the appropriate file based on `ASPNETCORE_ENVIRONMENT` variable.

## 🔧 Configuration

Key settings in `appsettings.json`:

```json
{
  "Ghostscript": {
    "ExecutablePath": "C:\\Program Files\\gs\\gs10.06.0\\bin\\gswin64c.exe",
    "TempDirectory": "C:\\Temp\\PdfaConversion",
    "TimeoutInSeconds": 300
  },
  "ServiceHost": {
    "KestrelListenerPort": 7015
  }
}
```

## 📚 Full Documentation

- **TFS/Azure DevOps Deployment**: See [TFS_DEVOPS_DEPLOYMENT.md](./TFS_DEVOPS_DEPLOYMENT.md)
- **Deployment Checklist**: See [DEPLOYMENT_CHECKLIST.md](./DEPLOYMENT_CHECKLIST.md)
- **Deployment Recommendations**: See [DEPLOYMENT_RECOMMENDATIONS.md](./DEPLOYMENT_RECOMMENDATIONS.md)
- **Security for Localhost**: See [SECURITY_LOCALHOST.md](./SECURITY_LOCALHOST.md)

## 🐛 Troubleshooting

### Service Won't Start
```powershell
# Check event logs
Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 20

# Verify Ghostscript
Test-Path "C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe"

# Check permissions
icacls "C:\Temp\PdfaConversion"
```

### Health Check Fails
- Wait 10-15 seconds after service start
- Check if port 7015 is available
- Verify service is running: `Get-Service PDFAConversionService`

### Build Fails
- Ensure .NET 10.0 SDK is installed on build agent
- Run `dotnet restore` manually
- Check for test failures

## 📞 Support

For deployment issues, refer to:
- Deployment scripts in `../deployment-scripts/`
- Full deployment guide: [TFS_DEVOPS_DEPLOYMENT.md](./TFS_DEVOPS_DEPLOYMENT.md)
- Troubleshooting section in [README.md](../README.md)

