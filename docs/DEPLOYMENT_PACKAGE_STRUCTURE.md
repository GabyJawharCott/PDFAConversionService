# Deployment Package Structure

This document describes the structure of the deployment package and what files are included.

## 📦 Deployment Package Contents

After building and publishing, the deployment package should contain:

```
PDFAConversionService/
├── PDFAConversionService.exe          # Main executable
├── PDFAConversionService.dll          # Main assembly
├── PDFAConversionService.pdb          # Debug symbols (optional)
├── PDFAConversionService.runtimeconfig.json
├── PDFAConversionService.deps.json
├── appsettings.json                   # Base configuration
├── appsettings.Development.json       # Development config
├── appsettings.Production.json        # Production config
├── appsettings.Staging.json           # Staging config
│
├── Dependencies/                      # NuGet packages
│   ├── FluentValidation.dll
│   ├── FluentValidation.AspNetCore.dll
│   ├── Microsoft.Extensions.*.dll
│   ├── Swashbuckle.AspNetCore.*.dll
│   └── ...
│
└── runtimes/                          # .NET runtime files
    └── win/
        └── lib/
            └── net8.0/
                └── System.ServiceProcess.ServiceController.dll
```

## 📋 Required Files

### Essential Files (Must Deploy)
- ✅ `PDFAConversionService.exe` - Main executable
- ✅ `PDFAConversionService.dll` - Main assembly
- ✅ `PDFAConversionService.runtimeconfig.json` - Runtime configuration
- ✅ `PDFAConversionService.deps.json` - Dependency manifest
- ✅ `appsettings.json` - Base configuration
- ✅ All DLL dependencies

### Configuration Files (Environment-Specific)
- ✅ `appsettings.Development.json` - For Development
- ✅ `appsettings.Staging.json` - For Staging
- ✅ `appsettings.Production.json` - For Production

### Optional Files
- ⚠️ `PDFAConversionService.pdb` - Debug symbols (for troubleshooting)
- ⚠️ `runtimes/` - Platform-specific runtime files

## 🔍 Verification Checklist

Before deployment, verify:

- [ ] All DLL dependencies are included
- [ ] Configuration files are present
- [ ] Executable is not corrupted
- [ ] File sizes are reasonable (not 0 bytes)
- [ ] No missing dependencies

## 📊 Expected Package Size

Approximate sizes:
- **Executable + DLLs**: ~500 KB - 1 MB
- **Dependencies**: ~5-10 MB
- **Total Package**: ~10-15 MB

## 🚀 Deployment Package Creation

### Using dotnet publish
```bash
dotnet publish PDFAConversionService/PDFAConversionService.csproj \
  -c Release \
  -o publish \
  --self-contained false
```

### Using Azure DevOps
The build pipeline automatically creates the package in the artifact staging directory.

## 📁 Target Server Structure

After deployment, the server should have:

```
C:\Services\PDFAConversionService\
├── PDFAConversionService.exe
├── PDFAConversionService.dll
├── appsettings.json
├── appsettings.Production.json
└── [all dependencies]
```

## 🔧 Post-Deployment Verification

1. **File Count Check**
   ```powershell
   (Get-ChildItem "C:\Services\PDFAConversionService" -File).Count
   # Should be 20-30 files
   ```

2. **Dependency Check**
   ```powershell
   # Check for critical DLLs
   Test-Path "C:\Services\PDFAConversionService\FluentValidation.dll"
   Test-Path "C:\Services\PDFAConversionService\Microsoft.Extensions.Hosting.dll"
   ```

3. **Configuration Check**
   ```powershell
   # Verify config files
   Test-Path "C:\Services\PDFAConversionService\appsettings.json"
   Test-Path "C:\Services\PDFAConversionService\appsettings.Production.json"
   ```

## 📝 Notes

- Configuration files are environment-aware
- The service will automatically load the correct `appsettings.{Environment}.json` file
- Missing dependencies will cause runtime errors
- Always verify package integrity before deployment

