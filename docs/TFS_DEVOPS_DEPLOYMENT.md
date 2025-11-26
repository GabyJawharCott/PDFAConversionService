# TFS/Azure DevOps Deployment Guide - PDFA Conversion Service

This guide provides step-by-step instructions for deploying the PDFA Conversion Service to a Dev environment using TFS/Azure DevOps.

## 📋 Prerequisites

### 1. TFS/Azure DevOps Setup
- [ ] Access to TFS/Azure DevOps project
- [ ] Build Agent with Windows OS
- [ ] Release Agent with access to Dev server
- [ ] Service account with deployment permissions

### 2. Dev Server Requirements
- [ ] Windows Server (2016 or later)
- [ ] .NET 10.0 Runtime installed
- [ ] Ghostscript installed (version 10.06.0 or later)
- [ ] Service account configured (NETWORK SERVICE or custom)
- [ ] Network access to Dev server from build agent

### 3. Repository Structure
```
PDFAConversionService/
├── PDFAConversionService/
│   ├── PDFAConversionService.csproj
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── ...
├── PDFAConversionService.Tests/
└── azure-pipelines.yml (or use Classic pipelines)
```

## 🔧 Step 1: Create Build Pipeline

### Option A: YAML Pipeline (Recommended)

Create `azure-pipelines.yml` in the root of your repository:

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
    - main
    - develop
    - release/*

pool:
  vmImage: 'windows-latest'

variables:
  buildConfiguration: 'Release'
  solution: 'PDFAConversionService.sln'
  projectPath: 'PDFAConversionService/PDFAConversionService.csproj'
  testProjectPath: 'PDFAConversionService.Tests/PDFAConversionService.Tests.csproj'

stages:
- stage: Build
  displayName: 'Build and Test'
  jobs:
  - job: Build
    displayName: 'Build Application'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 10.0 SDK'
      inputs:
        packageType: 'sdk'
        version: '10.0.x'
        installationPath: $(Agent.ToolsDirectory)/dotnet

    - task: DotNetCoreCLI@2
      displayName: 'Restore NuGet packages'
      inputs:
        command: 'restore'
        projects: '$(solution)'

    - task: DotNetCoreCLI@2
      displayName: 'Build solution'
      inputs:
        command: 'build'
        projects: '$(solution)'
        arguments: '--configuration $(buildConfiguration) --no-restore'

    - task: DotNetCoreCLI@2
      displayName: 'Run unit tests'
      inputs:
        command: 'test'
        projects: '$(testProjectPath)'
        arguments: '--configuration $(buildConfiguration) --no-build --verbosity normal'
        publishTestResults: true

    - task: DotNetCoreCLI@2
      displayName: 'Publish application'
      inputs:
        command: 'publish'
        projects: '$(projectPath)'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/publish --no-build'
        zipAfterPublish: false

    - task: CopyFiles@2
      displayName: 'Copy configuration files'
      inputs:
        SourceFolder: 'PDFAConversionService'
        Contents: |
          appsettings.json
          appsettings.Development.json
        TargetFolder: '$(Build.ArtifactStagingDirectory)/publish'

    - task: PublishBuildArtifacts@1
      displayName: 'Publish build artifacts'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)/publish'
        ArtifactName: 'PDFAConversionService'
        publishLocation: 'Container'
```

### Option B: Classic Build Pipeline

1. **Create New Build Pipeline**
   - Go to Pipelines → Builds → New Pipeline
   - Select your repository
   - Choose "ASP.NET Core" template

2. **Configure Build Tasks**

   **Task 1: Use .NET Core SDK**
   - Task: `Use .NET Core SDK`
   - Version: `10.0.x`

   **Task 2: Restore**
   - Task: `NuGet restore` or `dotnet restore`
   - Path to solution: `PDFAConversionService.sln`

   **Task 3: Build**
   - Task: `Build solution` or `dotnet build`
   - Path to solution: `PDFAConversionService.sln`
   - Configuration: `Release`
   - Arguments: `--no-restore`

   **Task 4: Test**
   - Task: `Test` or `dotnet test`
   - Path to test project: `PDFAConversionService.Tests/PDFAConversionService.Tests.csproj`
   - Arguments: `--configuration Release --no-build --verbosity normal`

   **Task 5: Publish**
   - Task: `Publish` or `dotnet publish`
   - Path to project: `PDFAConversionService/PDFAConversionService.csproj`
   - Arguments: `--configuration Release --output $(Build.ArtifactStagingDirectory)/publish --no-build`
   - Zip Published Projects: `false`

   **Task 6: Copy Files**
   - Task: `Copy Files`
   - Source Folder: `PDFAConversionService`
   - Contents: `appsettings.json`, `appsettings.Development.json`
   - Target Folder: `$(Build.ArtifactStagingDirectory)/publish`

   **Task 7: Publish Artifacts**
   - Task: `Publish Build Artifacts`
   - Path to publish: `$(Build.ArtifactStagingDirectory)/publish`
   - Artifact name: `PDFAConversionService`

## 🚀 Step 2: Create Release Pipeline

### Classic Release Pipeline Setup

1. **Create New Release Pipeline**
   - Go to Pipelines → Releases → New Pipeline
   - Select "Empty job" template

2. **Add Artifact**
   - Source: Build artifact from your build pipeline
   - Source alias: `_PDFAConversionService`

3. **Configure Dev Environment**

   **Stage 1: Dev Environment**

   **Agent Job:**
   - Agent pool: Select your Windows agent pool
   - Agent: Windows agent with access to Dev server

   **Tasks:**

   **Task 1: Extract Files** (if artifact is zipped)
   - Task: `Extract Files`
   - Archive file patterns: `$(System.DefaultWorkingDirectory)/_PDFAConversionService/PDFAConversionService/*.zip`
   - Destination folder: `$(System.DefaultWorkingDirectory)/_PDFAConversionService/PDFAConversionService/extracted`

   **Task 2: PowerShell - Pre-Deployment Checks**
   ```powershell
   # Pre-Deployment Checks
   $devServer = "$(DevServerName)"
   $servicePath = "$(DevServicePath)" # e.g., C:\Services\PDFAConversionService
   
   Write-Host "Checking prerequisites on $devServer..."
   
   # Check .NET Runtime
   $dotnetVersion = Invoke-Command -ComputerName $devServer -ScriptBlock {
       dotnet --version
   }
   Write-Host ".NET Version: $dotnetVersion"
   
   # Check Ghostscript
   $gsPath = "C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe"
   $gsExists = Invoke-Command -ComputerName $devServer -ScriptBlock {
       Test-Path $using:gsPath
   }
   if (-not $gsExists) {
       throw "Ghostscript not found at $gsPath"
   }
   Write-Host "Ghostscript found: $gsExists"
   
   # Check temp directory
   $tempDir = "C:\Temp\PdfaConversion"
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       if (-not (Test-Path $using:tempDir)) {
           New-Item -ItemType Directory -Path $using:tempDir -Force
       }
   }
   Write-Host "Temp directory ready: $tempDir"
   ```

   **Task 3: Stop Windows Service**
   ```powershell
   # Stop Windows Service
   $serviceName = "PDFAConversionService"
   $devServer = "$(DevServerName)"
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($name)
       $service = Get-Service -Name $name -ErrorAction SilentlyContinue
       if ($service -and $service.Status -eq 'Running') {
           Stop-Service -Name $name -Force
           Write-Host "Service $name stopped"
           Start-Sleep -Seconds 5
       } else {
           Write-Host "Service $name not running or not found"
       }
   } -ArgumentList $serviceName
   ```

   **Task 4: Copy Files to Dev Server**
   - Task: `Windows Machine File Copy`
   - Source: `$(System.DefaultWorkingDirectory)/_PDFAConversionService/PDFAConversionService/publish`
   - Destination: `\\$(DevServerName)\C$\Services\PDFAConversionService`
   - Clean Target Folder: `true` (optional, for clean deployment)

   **Task 5: PowerShell - Configure AppSettings**
   ```powershell
   # Configure appsettings.Development.json
   $devServer = "$(DevServerName)"
   $servicePath = "C:\Services\PDFAConversionService"
   $appSettingsPath = "$servicePath\appsettings.Development.json"
   
   $appSettings = @{
       Ghostscript = @{
           Version = "$(GhostscriptVersion)"
           ExecutablePath = "$(GhostscriptExecutablePath)"
           TempDirectory = "$(TempDirectory)"
           BaseParameters = "$(GhostscriptBaseParameters)"
           TimeoutInSeconds = $(GhostscriptTimeoutSeconds)
       }
       ServiceHost = @{
           KestrelListenerPort = $(KestrelPort)
       }
       Logging = @{
           LogLevel = @{
               Default = "$(LogLevel)"
               Microsoft.AspNetCore = "Warning"
           }
       }
   }
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($path, $settings)
       $json = $settings | ConvertTo-Json -Depth 10
       Set-Content -Path $path -Value $json -Encoding UTF8
       Write-Host "Configuration updated: $path"
   } -ArgumentList $appSettingsPath, $appSettings
   ```

   **Task 6: PowerShell - Set Permissions**
   ```powershell
   # Set permissions for temp directory
   $devServer = "$(DevServerName)"
   $tempDir = "$(TempDirectory)"
   $serviceAccount = "$(ServiceAccount)" # e.g., "NT AUTHORITY\NETWORK SERVICE"
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($dir, $account)
       if (Test-Path $dir) {
           icacls $dir /grant "${account}:(OI)(CI)M" /inheritance:r
           Write-Host "Permissions set for $dir"
       }
   } -ArgumentList $tempDir, $serviceAccount
   ```

   **Task 7: Install/Update Windows Service**
   ```powershell
   # Install or Update Windows Service
   $devServer = "$(DevServerName)"
   $serviceName = "PDFAConversionService"
   $servicePath = "C:\Services\PDFAConversionService"
   $exePath = "$servicePath\PDFAConversionService.exe"
   $serviceAccount = "$(ServiceAccount)"
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($name, $path, $account)
       
       $service = Get-Service -Name $name -ErrorAction SilentlyContinue
       
       if ($service) {
           # Service exists - update it
           Write-Host "Updating existing service: $name"
           sc.exe config $name binPath= "`"$path`""
           sc.exe config $name obj= $account
           sc.exe config $name start= auto
       } else {
           # Service doesn't exist - create it
           Write-Host "Creating new service: $name"
           sc.exe create $name binPath= "`"$path`"" start= auto
           sc.exe config $name obj= $account
           sc.exe config $name DisplayName= "PDFA Conversion Service"
           sc.exe description $name "Converts PDF files to PDF/A-1b format using Ghostscript"
       }
       
       Write-Host "Service configured successfully"
   } -ArgumentList $serviceName, $exePath, $serviceAccount
   ```

   **Task 8: Start Windows Service**
   ```powershell
   # Start Windows Service
   $devServer = "$(DevServerName)"
   $serviceName = "PDFAConversionService"
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($name)
       Start-Service -Name $name
       Start-Sleep -Seconds 5
       
       $service = Get-Service -Name $name
       if ($service.Status -eq 'Running') {
           Write-Host "Service $name started successfully"
       } else {
           throw "Service $name failed to start. Status: $($service.Status)"
       }
   } -ArgumentList $serviceName
   ```

   **Task 9: Verify Deployment**
   ```powershell
   # Verify Deployment
   $devServer = "$(DevServerName)"
   $serviceName = "PDFAConversionService"
   $port = "$(KestrelPort)"
   $healthUrl = "http://localhost:$port/api/PdfaConversion/health"
   
   Invoke-Command -ComputerName $devServer -ScriptBlock {
       param($name, $url)
       
       # Check service status
       $service = Get-Service -Name $name
       if ($service.Status -ne 'Running') {
           throw "Service $name is not running. Status: $($service.Status)"
       }
       Write-Host "Service status: $($service.Status)"
       
       # Check health endpoint
       Start-Sleep -Seconds 10 # Wait for service to fully start
       try {
           $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30
           if ($response.StatusCode -eq 200) {
               Write-Host "Health check passed: $url"
               Write-Host "Response: $($response.Content)"
           } else {
               throw "Health check failed with status: $($response.StatusCode)"
           }
       } catch {
           Write-Warning "Health check failed: $_"
           # Get recent event log entries
           $events = Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 5 -ErrorAction SilentlyContinue
           foreach ($event in $events) {
               Write-Host "Event: $($event.Message)"
           }
           throw
       }
   } -ArgumentList $serviceName, $healthUrl
   ```

## 🔐 Step 3: Configure Variables

### Release Pipeline Variables

Go to Variables tab in your Release Pipeline and add:

| Variable Name | Value | Scope | Secret |
|--------------|-------|-------|--------|
| `DevServerName` | `dev-server-name` | Dev | No |
| `DevServicePath` | `C:\Services\PDFAConversionService` | Dev | No |
| `ServiceAccount` | `NT AUTHORITY\NETWORK SERVICE` | Dev | No |
| `GhostscriptVersion` | `10.06.0` | Dev | No |
| `GhostscriptExecutablePath` | `C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe` | Dev | No |
| `TempDirectory` | `C:\Temp\PdfaConversion` | Dev | No |
| `KestrelPort` | `7015` | Dev | No |
| `LogLevel` | `Information` | Dev | No |
| `GhostscriptTimeoutSeconds` | `300` | Dev | No |
| `GhostscriptBaseParameters` | `-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4 -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ` | Dev | No |

### Variable Groups (Optional but Recommended)

Create a Variable Group for shared configuration:

1. Go to Pipelines → Library → Variable Groups
2. Create new variable group: `PDFAConversionService-Dev`
3. Add all variables above
4. Link to Release Pipeline

## 📝 Step 4: Create appsettings.Development.json

Create this file in your repository:

```json
{
  "Ghostscript": {
    "Version": "10.06.0",
    "ExecutablePath": "C:\\Program Files\\gs\\gs10.06.0\\bin\\gswin64c.exe",
    "TempDirectory": "C:\\Temp\\PdfaConversion",
    "BaseParameters": "-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite  -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4  -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ",
    "TimeoutInSeconds": 300
  },
  "ServiceHost": {
    "KestrelListenerPort": 7015
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "PDFAConversionService": "Information"
    }
  }
}
```

## 🛠️ Step 5: Pre-Deployment Server Setup

Run these PowerShell commands on the Dev server (one-time setup):

```powershell
# 1. Install .NET 10.0 Runtime (if not already installed)
# Download from: https://dotnet.microsoft.com/download/dotnet/10.0
# Run installer

# 2. Install Ghostscript (if not already installed)
# Download from: https://www.ghostscript.com/download/gsdnld.html
# Install to default location: C:\Program Files\gs\gs10.06.0\

# 3. Create service directory
New-Item -ItemType Directory -Path "C:\Services\PDFAConversionService" -Force

# 4. Create temp directory
New-Item -ItemType Directory -Path "C:\Temp\PdfaConversion" -Force

# 5. Set temp directory permissions
$tempDir = "C:\Temp\PdfaConversion"
icacls $tempDir /grant "NETWORK SERVICE:(OI)(CI)M" /inheritance:r

# 6. Verify Ghostscript
$gsPath = "C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe"
if (Test-Path $gsPath) {
    Write-Host "Ghostscript found: $gsPath"
} else {
    Write-Error "Ghostscript not found at $gsPath"
}
```

## ✅ Step 6: Test Deployment

### Manual Test Steps

1. **Trigger Release Pipeline**
   - Go to Releases → Create Release
   - Select your build artifact
   - Deploy to Dev environment

2. **Monitor Deployment**
   - Watch the release pipeline logs
   - Check each task for errors

3. **Verify Service**
   ```powershell
   # On Dev server
   Get-Service PDFAConversionService
   
   # Test health endpoint
   Invoke-WebRequest -Uri "http://localhost:7015/api/PdfaConversion/health"
   
   # Check event logs
   Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 10
   ```

4. **Test Conversion**
   ```powershell
   # Test PDF conversion
   $base64Pdf = [Convert]::ToBase64String([IO.File]::ReadAllBytes("test.pdf"))
   $body = @{ base64Pdf = $base64Pdf } | ConvertTo-Json
   Invoke-RestMethod -Uri "http://localhost:7015/api/PdfaConversion/convert" `
       -Method POST -ContentType "application/json" -Body $body
   ```

## 🔄 Step 7: Continuous Deployment (Optional)

### Enable Continuous Deployment Trigger

1. Go to Release Pipeline → Edit
2. Click on Artifact → Continuous deployment trigger
3. Enable trigger
4. Add branch filters (e.g., `develop` for Dev environment)

## 📊 Step 8: Monitoring and Alerts

### Add Post-Deployment Tasks

**Task: Send Deployment Notification**
```powershell
# Send notification (email, Teams, etc.)
$deploymentStatus = "Success"
$devServer = "$(DevServerName)"
$serviceName = "PDFAConversionService"

Write-Host "##vso[task.logissue type=warning]Deployment completed to $devServer"
Write-Host "Service: $serviceName"
Write-Host "Status: $deploymentStatus"
```

## 🐛 Troubleshooting

### Common Issues

1. **Service Won't Start**
   - Check event logs: `Get-EventLog -LogName Application -Source "PDFAConversionService"`
   - Verify Ghostscript path
   - Check temp directory permissions
   - Verify .NET runtime is installed

2. **Health Check Fails**
   - Wait longer for service to start (increase sleep time)
   - Check if port is already in use
   - Verify service is running: `Get-Service PDFAConversionService`

3. **Permission Errors**
   - Verify service account has permissions
   - Check temp directory permissions
   - Verify service account can access Ghostscript

4. **Build Fails**
   - Check .NET SDK version on build agent
   - Verify all NuGet packages restore correctly
   - Check test failures

## 📋 Deployment Checklist

Before deploying:
- [ ] Build pipeline is configured and working
- [ ] Release pipeline is configured
- [ ] All variables are set correctly
- [ ] Dev server has .NET 10.0 Runtime installed
- [ ] Dev server has Ghostscript installed
- [ ] Temp directory exists and has permissions
- [ ] Service account is configured
- [ ] Network access to Dev server is available

After deployment:
- [ ] Service is running
- [ ] Health check passes
- [ ] Test conversion works
- [ ] Event logs show no errors
- [ ] Monitor for 24 hours

## 🔗 Additional Resources

- [Azure DevOps Pipelines Documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/)
- [.NET Core Deployment Guide](https://docs.microsoft.com/en-us/dotnet/core/deploying/)
- [Windows Service Deployment](https://docs.microsoft.com/en-us/dotnet/core/extensions/windows-service)

---

**Last Updated**: 2024-11-25
**Version**: 1.0

