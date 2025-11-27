using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace PDFAConversionService;

/// <summary>
/// Helper class for installing, uninstalling, and managing the Windows Service
/// </summary>
[SupportedOSPlatform("windows")]
public static class ServiceInstaller
{
    private const string ServiceName = "PDFAConversionService";
    private const string DisplayName = "PDFA Conversion Service";
    private const string Description = "Converts PDF files to PDF/A-1b format using Ghostscript";

    /// <summary>
    /// Installs the Windows Service
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static int Install(string? serviceAccount = null, ILogger? logger = null)
    {
        try
        {
            if (!IsAdministrator())
                return HandleAdminError(logger);

            var exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return HandleExecutableError(exePath, logger);

            logger?.LogInformation("Installing service: {ServiceName}", ServiceName);
            Console.WriteLine($"Installing Windows Service: {ServiceName}");
            Console.WriteLine($"Executable: {exePath}");

            var existingService = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (existingService != null)
            {
                HandleExistingService(existingService, logger);
            }

            var account = serviceAccount ?? "NT AUTHORITY\\NETWORK SERVICE";
            if (existingService == null)
            {
                if (RunScCommandWithError($"create {ServiceName} binPath= \"{exePath}\" start= auto", logger, "Failed to create service."))
                    return 1;
                Console.WriteLine("Service created successfully.");
            }
            else
            {
                if (RunScCommandWithError($"config {ServiceName} binPath= \"{exePath}\"", logger, "Failed to update service configuration."))
                    return 1;
            }

            if (RunScCommand($"config {ServiceName} obj= {account}") != 0)
            {
                logger?.LogWarning("Failed to set service account.");
                Console.WriteLine("WARNING: Failed to set service account. Continuing...");
            }

            RunScCommand($"config {ServiceName} DisplayName= {DisplayName}");
            RunScCommand($"description {ServiceName} {Description}");
            RunScCommand($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");

            Console.WriteLine("Service configured successfully.");
            Console.WriteLine($"  - Service Account: {account}");
            Console.WriteLine($"  - Auto-start: Enabled");
            Console.WriteLine($"  - Auto-restart on failure: Enabled");

            return StartServiceAfterInstall(logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error installing service");
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Uninstalls the Windows Service
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static int Uninstall(ILogger? logger = null)
    {
        try
        {
            if (!IsAdministrator())
            {
                logger?.LogError("Administrator privileges required to uninstall service");
                Console.Error.WriteLine("ERROR: This operation requires Administrator privileges.");
                Console.Error.WriteLine("Please run as Administrator or use PowerShell script: .\\deployment-scripts\\Uninstall-Service.ps1");
                return 1;
            }

            logger?.LogInformation("Uninstalling service: {ServiceName}", ServiceName);
            Console.WriteLine($"Uninstalling Windows Service: {ServiceName}");

            var service = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                logger?.LogInformation("Service not found");
                Console.WriteLine("Service not found. Nothing to uninstall.");
                return 0;
            }

            // Stop service if running
            if (service.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("Stopping service...");
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                Console.WriteLine("Service stopped.");
            }

            // Delete service
            Console.WriteLine("Removing service...");
            var result = RunScCommand($"delete {ServiceName}");
            
            if (result == 0)
            {
                logger?.LogInformation("Service uninstalled successfully");
                Console.WriteLine("Service removed successfully!");
                return 0;
            }
            else
            {
                logger?.LogError("Failed to remove service. Exit code: {ExitCode}", result);
                Console.Error.WriteLine("ERROR: Failed to remove service.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error uninstalling service");
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Starts the Windows Service
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static int Start(ILogger? logger = null)
    {
        try
        {
            if (!IsAdministrator())
            {
                Console.Error.WriteLine("ERROR: Administrator privileges required to start service.");
                return 1;
            }

            var service = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                Console.Error.WriteLine($"ERROR: Service '{ServiceName}' not found.");
                return 1;
            }

            if (service.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("Service is already running.");
                return 0;
            }

            Console.WriteLine("Starting service...");
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            if (service.Status == ServiceControllerStatus.Running)
            {
                Console.WriteLine("Service started successfully!");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"ERROR: Service failed to start. Status: {service.Status}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Stops the Windows Service
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static int Stop(ILogger? logger = null)
    {
        try
        {
            if (!IsAdministrator())
            {
                Console.Error.WriteLine("ERROR: Administrator privileges required to stop service.");
                return 1;
            }

            var service = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                Console.Error.WriteLine($"ERROR: Service '{ServiceName}' not found.");
                return 1;
            }

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                Console.WriteLine("Service is already stopped.");
                return 0;
            }

            Console.WriteLine("Stopping service...");
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                Console.WriteLine("Service stopped successfully!");
                return 0;
            }
            else
            {
                Console.Error.WriteLine($"ERROR: Service failed to stop. Status: {service.Status}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Shows service status
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static int Status(ILogger? logger = null)
    {
        try
        {
            var service = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service == null)
            {
                Console.WriteLine($"Service '{ServiceName}' is not installed.");
                return 1;
            }

            Console.WriteLine($"Service: {ServiceName}");
            Console.WriteLine($"Display Name: {service.DisplayName}");
            Console.WriteLine($"Status: {service.Status}");
            Console.WriteLine($"Start Type: {service.StartType}");
            Console.WriteLine($"Can Stop: {service.CanStop}");
            Console.WriteLine($"Can Pause and Continue: {service.CanPauseAndContinue}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Shows help/usage information
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine("PDFA Conversion Service - Command Line Options");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("Usage: PDFAConversionService.exe [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  install [--account <account>]  Install the Windows Service");
        Console.WriteLine("                                  Example: install --account \"NT AUTHORITY\\NETWORK SERVICE\"");
        Console.WriteLine("  uninstall                      Uninstall the Windows Service");
        Console.WriteLine("  start                          Start the Windows Service");
        Console.WriteLine("  stop                           Stop the Windows Service");
        Console.WriteLine("  status                         Show service status");
        Console.WriteLine("  help                           Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  PDFAConversionService.exe install");
        Console.WriteLine("  PDFAConversionService.exe install --account \"DOMAIN\\ServiceAccount\"");
        Console.WriteLine("  PDFAConversionService.exe uninstall");
        Console.WriteLine("  PDFAConversionService.exe start");
        Console.WriteLine("  PDFAConversionService.exe stop");
        Console.WriteLine("  PDFAConversionService.exe status");
        Console.WriteLine();
        Console.WriteLine("Note: Install and uninstall commands require Administrator privileges.");
        Console.WriteLine("      You can also use the PowerShell scripts in deployment-scripts folder.");
    }

    private static bool IsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetExecutablePath()
    {
        try
        {
            return System.Reflection.Assembly.GetExecutingAssembly().Location;
        }
        catch
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
    }

    private static int RunScCommand(string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return -1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error running sc.exe: {ex.Message}");
            return -1;
        }
    }

    private static int HandleAdminError(ILogger? logger)
    {
        logger?.LogError("Administrator privileges required to install service");
        Console.Error.WriteLine("ERROR: This operation requires Administrator privileges.");
        Console.Error.WriteLine("Please run as Administrator or use PowerShell script: .\\deployment-scripts\\Install-Service.ps1");
        return 1;
    }

    private static int HandleExecutableError(string? exePath, ILogger? logger)
    {
        logger?.LogError("Executable not found: {Path}", exePath);
        Console.Error.WriteLine($"ERROR: Executable not found: {exePath}");
        return 1;
    }

    private static void HandleExistingService(ServiceController existingService, ILogger? logger)
    {
        logger?.LogInformation("Service already exists, updating configuration");
        Console.WriteLine("Service already exists. Updating configuration...");
        if (existingService.Status == ServiceControllerStatus.Running)
        {
            Console.WriteLine("Stopping service...");
            existingService.Stop();
            existingService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
    }

    private static bool RunScCommandWithError(string arguments, ILogger? logger, string errorMessage)
    {
        var result = RunScCommand(arguments);
        if (result != 0)
        {
            // S2629, CA2254, CA1873: Use message template, avoid interpolation, and avoid expensive evaluation if logging is disabled
            logger?.LogError("{ErrorMessage} Exit code: {ExitCode}", errorMessage, result);
            Console.Error.WriteLine($"ERROR: {errorMessage}");
            return true;
        }
        return false;
    }

    private static int StartServiceAfterInstall(ILogger? logger)
    {
        Console.WriteLine("\nStarting service...");
        var startResult = RunScCommand($"start {ServiceName}");
        if (startResult == 0)
        {
            System.Threading.Thread.Sleep(2000);
            var service = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase));

            if (service != null && service.Status == ServiceControllerStatus.Running)
            {
                logger?.LogInformation("Service installed and started successfully");
                Console.WriteLine("Service started successfully!");
                Console.WriteLine($"\nService Status: {service.Status}");
                Console.WriteLine($"Start Type: {service.StartType}");
                return 0;
            }
            else
            {
                logger?.LogWarning("Service installed but failed to start. Status: {Status}", service?.Status);
                Console.WriteLine($"WARNING: Service installed but status is: {service?.Status ?? ServiceControllerStatus.Stopped}");
                Console.WriteLine("Check event logs for details.");
                return 0;
            }
        }
        else
        {
            logger?.LogWarning("Service installed but failed to start. Exit code: {ExitCode}", startResult);
            Console.WriteLine("WARNING: Service installed but failed to start.");
            Console.WriteLine("You can start it manually using: sc start " + ServiceName);
            return 0;
        }
    }
}

