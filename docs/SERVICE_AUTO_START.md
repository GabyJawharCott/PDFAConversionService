# Service Auto-Start and Auto-Restart Configuration

## Overview

The PDFA Conversion Service is configured to:
- ✅ **Start automatically** when Windows starts
- ✅ **Restart automatically** if the service crashes or fails
- ✅ **Keep running** until explicitly stopped by the user

## Configuration Details

### Automatic Startup

The service is configured with `start= auto`, which means:
- The service will start automatically when Windows boots
- No manual intervention required after system restart
- The service will be available immediately after system startup

### Automatic Restart on Failure

The service is configured with failure recovery actions:
- **First failure**: Service restarts after 60 seconds
- **Second failure**: Service restarts after 60 seconds
- **Subsequent failures**: Service restarts after 60 seconds
- **Failure count reset**: After 1 day (86400 seconds) without failures

This ensures that if the service crashes due to:
- Unexpected exceptions
- Memory issues
- Process termination
- System resource issues

The service will automatically recover and continue running.

## Configuration Command

The failure recovery is configured using:

```powershell
sc.exe failure PDFAConversionService reset= 86400 actions= restart/60000/restart/60000/restart/60000
```

**Parameters:**
- `reset= 86400`: Reset failure count after 1 day (86400 seconds)
- `actions= restart/60000/restart/60000/restart/60000`: 
  - First failure: restart after 60 seconds (60000 milliseconds)
  - Second failure: restart after 60 seconds
  - Third and subsequent failures: restart after 60 seconds

## Verification

To verify the service configuration:

```powershell
# Check service start type
Get-Service PDFAConversionService | Select-Object Name, Status, StartType

# Check failure recovery configuration
sc.exe qfailure PDFAConversionService
```

Expected output:
- **StartType**: `Automatic`
- **Failure Actions**: Restart after 60 seconds on all failures

## Manual Service Control

Even though the service starts automatically, you can still control it manually:

```powershell
# Start the service
Start-Service PDFAConversionService

# Stop the service
Stop-Service PDFAConversionService

# Restart the service
Restart-Service PDFAConversionService

# Check service status
Get-Service PDFAConversionService
```

**Note**: If you stop the service manually, it will **NOT** automatically restart until:
- The system is rebooted (service will start automatically)
- You manually start it again
- The service crashes (then auto-restart will trigger)

## Troubleshooting

### Service Not Starting on Boot

If the service doesn't start automatically:

1. **Check service configuration:**
   ```powershell
   sc.exe qc PDFAConversionService
   ```
   Verify `START_TYPE` is `AUTO_START`

2. **Check event logs:**
   ```powershell
   Get-EventLog -LogName Application -Source "PDFAConversionService" -Newest 20
   ```

3. **Reinstall service:**
   ```powershell
   .\deployment-scripts\Install-Service.ps1 -ServicePath "C:\Services\PDFAConversionService"
   ```

### Service Not Auto-Restarting on Failure

If the service doesn't restart after a failure:

1. **Check failure recovery configuration:**
   ```powershell
   sc.exe qfailure PDFAConversionService
   ```

2. **Reconfigure failure recovery:**
   ```powershell
   sc.exe failure PDFAConversionService reset= 86400 actions= restart/60000/restart/60000/restart/60000
   ```

3. **Check if service account has permissions:**
   - Ensure the service account has "Log on as a service" permission
   - Check Windows Event Log for permission errors

## Best Practices

1. **Monitor Service Health**: Set up monitoring/alerts for service failures
2. **Review Logs**: Regularly check event logs for recurring failures
3. **Test Auto-Restart**: Periodically test that auto-restart works by stopping the service process
4. **Update Configuration**: If you need different restart delays, update the `sc.exe failure` command

## Related Documentation

- [Installation Guide](../deployment-scripts/Install-Service.ps1)
- [Deployment Guide](./TFS_DEVOPS_DEPLOYMENT.md)
- [Troubleshooting](../README.md#troubleshooting)

