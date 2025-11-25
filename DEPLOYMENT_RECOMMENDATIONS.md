# Deployment Recommendations for PDFA Conversion Service

## 🚨 Critical Issues to Address Before Production

### 1. **HTTPS/SSL Configuration** ⚠️
**Current State**: Service only listens on `localhost` without HTTPS
**Action Required**:
```csharp
// Update Program.cs to support HTTPS
builder.WebHost.UseKestrel(options =>
{
    options.Listen(IPAddress.Any, cfg.KestrelPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps(); // Add SSL certificate configuration
    });
});
```

**Recommendations**:
- Use a valid SSL certificate (from CA or internal PKI)
- Configure certificate path in `appsettings.Production.json`
- Consider using certificate store instead of file-based certificates
- Enable HTTPS redirect middleware

### 2. **Authentication** ⚠️
**Current State**: No authentication implemented
**Action Required**:
- Implement API Key authentication or OAuth2/JWT
- Add `[Authorize]` attribute to controller endpoints
- Store API keys securely (Azure Key Vault, AWS Secrets Manager)

**Example Implementation**:
```csharp
// Add to Program.cs
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", options => { });

// Add to Controller
[Authorize]
[HttpPost("convert")]
public async Task<ActionResult<PdfaConversionResponse>> ConvertToPdfA(...)
```

### 3. **Rate Limiting** ⚠️
**Current State**: No rate limiting
**Action Required**:
```csharp
// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

### 4. **Swagger in Production** ⚠️
**Current State**: Swagger enabled in Development only (good!)
**Action Required**: Ensure Swagger is disabled in production
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// ✅ Already correct - no action needed
```

### 5. **Error Message Information Disclosure**
**Current State**: Error messages may expose system details
**Action Required**: Review error messages in `PdfaConversionController.cs`
- Ensure production errors don't expose file paths
- Use generic error messages for production
- Log detailed errors server-side only

### 6. **Configuration Management**
**Action Required**: Create `appsettings.Production.json`
```json
{
  "Ghostscript": {
    "ExecutablePath": "${GHOSTSCRIPT_PATH}",
    "TimeoutInSeconds": 300,
    "TempDirectory": "${TEMP_DIRECTORY}"
  },
  "ServiceHost": {
    "KestrelListenerPort": 7015
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## 📋 Pre-Deployment Tasks

### Immediate Actions (Before First Deployment)

1. **Create Production Configuration**
   ```bash
   # Create appsettings.Production.json
   # Move sensitive values to environment variables or secret store
   ```

2. **Set Up Monitoring**
   - Add Application Insights or similar
   - Configure health check endpoints
   - Set up alerting

3. **Security Hardening**
   - Enable HTTPS
   - Add authentication
   - Configure firewall rules
   - Review and restrict network access

4. **Performance Testing**
   - Load test with expected production load
   - Test concurrent conversions
   - Verify timeout handling
   - Check memory usage

5. **Documentation**
   - Update README with production deployment steps
   - Document configuration options
   - Create runbook for operations team

### Before Each Deployment

1. **Run Full Test Suite**
   ```bash
   dotnet test
   ```

2. **Verify Configuration**
   - Check all environment variables
   - Verify Ghostscript path
   - Confirm temp directory exists and has space

3. **Check Dependencies**
   - Verify .NET runtime version
   - Confirm Ghostscript version
   - Check Windows updates

4. **Backup Current Version**
   - Backup configuration files
   - Document current version
   - Prepare rollback plan

## 🔧 Production Configuration Example

### appsettings.Production.json
```json
{
  "Ghostscript": {
    "Version": "10.06.0",
    "ExecutablePath": "C:\\Program Files\\gs\\gs10.06.0\\bin\\gswin64c.exe",
    "TempDirectory": "D:\\Temp\\PdfaConversion",
    "BaseParameters": "-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4 -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ",
    "TimeoutInSeconds": 300
  },
  "ServiceHost": {
    "KestrelListenerPort": 7015
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "PDFAConversionService": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### Environment Variables (Recommended)
```bash
# Set these in production environment
GHOSTSCRIPT_EXECUTABLE_PATH=C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe
TEMP_DIRECTORY=D:\Temp\PdfaConversion
KESTREL_PORT=7015
LOG_LEVEL=Warning
```

## 🚀 Deployment Steps

### 1. Prepare Server
```powershell
# Install .NET 10.0 Runtime
# Install Ghostscript
# Create temp directory
New-Item -ItemType Directory -Path "D:\Temp\PdfaConversion" -Force
```

### 2. Deploy Application
```powershell
# Copy application files
# Copy appsettings.Production.json
# Set environment variables
```

### 3. Install as Windows Service
```powershell
# Install service
sc create PDFAConversionService binPath="C:\Services\PDFAConversionService\PDFAConversionService.exe" start=auto

# Configure service account
sc config PDFAConversionService obj="NT AUTHORITY\NETWORK SERVICE"

# Start service
sc start PDFAConversionService
```

### 4. Verify Deployment
```powershell
# Check service status
sc query PDFAConversionService

# Test health endpoint
Invoke-WebRequest -Uri "http://localhost:7015/api/PdfaConversion/health"

# Check logs
Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 10
```

## 📊 Monitoring Checklist

### Key Metrics to Monitor
- [ ] Request count per minute/hour
- [ ] Average response time
- [ ] Error rate (4xx, 5xx)
- [ ] Conversion success rate
- [ ] Memory usage
- [ ] CPU usage
- [ ] Disk space (temp directory)
- [ ] Active Ghostscript processes

### Alerts to Configure
- [ ] Service down (health check fails)
- [ ] High error rate (>5%)
- [ ] Slow response time (>30 seconds)
- [ ] High memory usage (>80%)
- [ ] Low disk space (<10% free)
- [ ] Conversion timeout rate

## 🔍 Post-Deployment Validation

### Immediate Checks (First 24 Hours)
1. Monitor error logs
2. Check performance metrics
3. Verify health checks
4. Test conversion functionality
5. Monitor resource usage

### Ongoing Monitoring
1. Review logs daily
2. Monitor performance weekly
3. Review error trends monthly
4. Update dependencies quarterly

## 🛡️ Security Best Practices

1. **Principle of Least Privilege**
   - Service account should have minimal permissions
   - Temp directory should be isolated
   - Ghostscript should run with limited privileges

2. **Defense in Depth**
   - Network-level security (firewall)
   - Application-level security (authentication)
   - Process-level security (isolation)

3. **Regular Updates**
   - Keep .NET runtime updated
   - Keep Ghostscript updated
   - Keep Windows updated
   - Monitor security advisories

4. **Audit & Compliance**
   - Enable audit logging
   - Review access logs regularly
   - Document security incidents

## 📞 Support & Escalation

### Support Contacts
- **Development Team**: [Contact Info]
- **Operations Team**: [Contact Info]
- **Security Team**: [Contact Info]

### Escalation Path
1. Level 1: Operations Team (Service restart, basic troubleshooting)
2. Level 2: Development Team (Code issues, configuration problems)
3. Level 3: Architecture Team (Design issues, scalability problems)

---

**Note**: This is a living document. Update as the service evolves and new requirements emerge.

