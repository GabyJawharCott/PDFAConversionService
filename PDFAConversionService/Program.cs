using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using PDFAConversionService;
using PDFAConversionService.Middleware;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using PDFAConversionService.Validators;

//TODO: Refactor this file, Move some code especially the ones related to the Builder to functions if possible

string serviceName = "Cott.PDFAConversion.Service";

AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    Console.Error.WriteLine("UNHANDLED: " + e.ExceptionObject);
};


// Handle command-line arguments for service management
if (args.Length > 0)
{
    var command = args[0].ToLowerInvariant();

    switch (command)
    {
#if WINDOWS
        case "install":
            var account = ExtractOption(args, "--account") ?? "NT AUTHORITY\\NETWORK SERVICE";
            exitCode = ServiceInstaller.Install(account);
            Environment.Exit(exitCode);
            return;

        case "uninstall":
            exitCode = ServiceInstaller.Uninstall();
            Environment.Exit(exitCode);
            return;

        case "start":
            exitCode = ServiceInstaller.Start();
            Environment.Exit(exitCode);
            return;

        case "stop":
            exitCode = ServiceInstaller.Stop();
            Environment.Exit(exitCode);
            return;

        case "status":
            exitCode = ServiceInstaller.Status();
            Environment.Exit(exitCode);
            return;
#endif
        case "help":
        case "--help":
        case "-h":
        case "/?":
#if WINDOWS
            ServiceInstaller.ShowHelp();
#else
            Console.WriteLine("Service management commands are only available on Windows.");
#endif
            Environment.Exit(0);
            return;

        default:
            await Console.Error.WriteLineAsync($"Unknown command: {command}");
            await Console.Error.WriteLineAsync("Use 'help' to see available commands.");
            Environment.Exit(1);
            return;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Lifetime
if (Environment.UserInteractive)
    builder.Host.UseConsoleLifetime();
else
    builder.Host.UseWindowsService(o => o.ServiceName = serviceName);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new EventLogSettings { SourceName = serviceName });
}

// Register services including command executor
builder.Services.AddSingleton<ICommandExecutorService, CommandExecutorService>();

// Validate configuration and get startup config
// Note: We need to build a temporary provider to resolve ICommandExecutorService for Ghostscript discovery
// This is a one-time operation during startup, so the overhead is acceptable
StartupConfig cfg;
using (var tempProvider = builder.Services.BuildServiceProvider())
{
    var executor = tempProvider.GetRequiredService<ICommandExecutorService>();
    cfg = ValidateAndGetStartupConfig(builder.Configuration, executor);
}

// Register options using validated values
builder.Services.AddOptions<GhostscriptOptions>()
    .Configure(o =>
    {
        o.ExecutablePath = cfg.GhostscriptPath;
        o.BaseParameters = cfg.BaseParameters;
        // Use configured temp directory or fallback to system temp
        o.TempDirectory = builder.Configuration["Ghostscript:TempDirectory"] 
            ?? Path.Combine(Path.GetTempPath(), "PdfaConversion");
        o.TimeoutInSeconds = cfg.TimeoutInSeconds;
    })
    .Validate(o => File.Exists(o.ExecutablePath!), "Ghostscript executable not found.")
    .ValidateOnStart();

builder.Services.AddOptions<ServiceHostOptions>()
    .Configure(o => o.KestrelListenerPort = cfg.KestrelPort)
    .Validate(o => o.KestrelListenerPort > 0 && o.KestrelListenerPort <= 65535, "KestrelListenerPort must be between 1 and 65535.")
    .ValidateOnStart();

// Core services
builder.Services.AddControllers();

// Register new service abstractions
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddScoped<IProcessService, ProcessService>();
builder.Services.AddScoped<IPdfaConversionService, PdfaConversionService>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<PdfaConversionRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Kestrel with configurable HTTPS support
builder.WebHost.UseKestrel(options =>
{
    // Check if HTTPS is enabled via configuration
    var httpsEnabled = builder.Configuration.GetValue<bool>("Kestrel:Https:Enabled", false);
    var isDevelopment = builder.Environment.IsDevelopment();
    
    options.ListenLocalhost(cfg.KestrelPort, listenOptions =>
    {
        if (httpsEnabled)
        {
            // HTTPS with HTTP/2 support
            var httpsConfig = builder.Configuration.GetSection("Kestrel:Https");
            ConfigureHttps(listenOptions, httpsConfig, isDevelopment);
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        }
        else
        {
            // HTTP only (no HTTPS, no HTTP/2) - removes the warning
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
        }
    });
    
    // Set request size limits (100 MB for base64 PDFs, with some overhead)
    options.Limits.MaxRequestBodySize = 120 * 1024 * 1024; // 120 MB
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32 KB
    options.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB
});

// Helper method to configure HTTPS
#region Configuration Helpers
static void ConfigureHttps(
    Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions,
    Microsoft.Extensions.Configuration.IConfigurationSection httpsConfig,
    bool isDevelopment)
{
    var certificateSource = httpsConfig["CertificateSource"] ?? "Development";

    switch (certificateSource.ToLowerInvariant())
    {
        case "development":
            ConfigureDevelopmentCertificate(listenOptions, isDevelopment);
            break;
        case "file":
            ConfigureFileCertificate(listenOptions, httpsConfig);
            break;
        case "store":
            ConfigureStoreCertificate(listenOptions, httpsConfig);
            break;
        default:
            throw new InvalidOperationException(
                $"Invalid CertificateSource '{certificateSource}'. Must be 'Development', 'File', or 'Store'");
    }
}

static void ConfigureDevelopmentCertificate(
    Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions,
    bool isDevelopment)
{
    if (!isDevelopment)
        throw new InvalidOperationException("Development certificate can only be used in development environment.");
    listenOptions.UseHttps();
}

static void ConfigureFileCertificate(
    Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions,
    Microsoft.Extensions.Configuration.IConfigurationSection httpsConfig)
{
    var certPath = httpsConfig["CertificatePath"];
    var certPassword = httpsConfig["CertificatePassword"];

    if (string.IsNullOrWhiteSpace(certPath))
        throw new InvalidOperationException("Kestrel:Https:CertificatePath is required when CertificateSource is 'File'");

    if (!System.IO.File.Exists(certPath))
        throw new InvalidOperationException($"Certificate file not found: {certPath}");

    if (string.IsNullOrWhiteSpace(certPassword))
        listenOptions.UseHttps(certPath);
    else
        listenOptions.UseHttps(certPath, certPassword);
}

static void ConfigureStoreCertificate(
    Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions,
    Microsoft.Extensions.Configuration.IConfigurationSection httpsConfig)
{
    var storeName = httpsConfig["StoreName"] ?? "My";
    var storeLocation = httpsConfig["StoreLocation"] ?? "LocalMachine";
    var thumbprint = httpsConfig["CertificateThumbprint"];
    var subject = httpsConfig["CertificateSubject"];

    if (string.IsNullOrWhiteSpace(thumbprint) && string.IsNullOrWhiteSpace(subject))
        throw new InvalidOperationException(
            "When CertificateSource is 'Store', you must provide either CertificateThumbprint or CertificateSubject");

    var location = storeLocation.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase)
        ? System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser
        : System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine;

    var store = new System.Security.Cryptography.X509Certificates.X509Store(storeName, location);

    try
    {
        store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);

        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = FindCertificate(store, thumbprint, subject);

        if (certificate == null)
        {
            throw new InvalidOperationException(
                $"Certificate not found in store. Store: {storeName}, Location: {storeLocation}, " +
                $"Thumbprint: {thumbprint ?? "N/A"}, Subject: {subject ?? "N/A"}");
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"Certificate '{certificate.Subject}' (Thumbprint: {certificate.Thumbprint}) " +
                "does not have a private key. The certificate cannot be used for SSL.");
        }

        listenOptions.UseHttps(certificate);
    }
    finally
    {
        store.Close();
    }
}

#endregion

static System.Security.Cryptography.X509Certificates.X509Certificate2? FindCertificate(
    System.Security.Cryptography.X509Certificates.X509Store store,
    string? thumbprint,
    string? subject)
{
    if (!string.IsNullOrWhiteSpace(thumbprint))
    {
        var certs = store.Certificates.Find(
            System.Security.Cryptography.X509Certificates.X509FindType.FindByThumbprint,
            thumbprint.Replace(" ", "").Replace("-", ""),
            validOnly: false);
        if (certs.Count > 0)
            return certs[0];
    }
    if (!string.IsNullOrWhiteSpace(subject))
    {
        var certs = store.Certificates.Find(
            System.Security.Cryptography.X509Certificates.X509FindType.FindBySubjectName,
            subject,
            validOnly: false);
        if (certs.Count > 0)
            return certs[0];
    }
    return null;
}

try
{
    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Add correlation ID middleware for structured logging
    app.UseCorrelationId();

    app.UseRouting();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"FATAL STARTUP ERROR: {ex.GetType().Name}: {ex.Message}");
    await Console.Error.WriteLineAsync(ex.ToString());
    Environment.ExitCode = -1;
}

// Centralized validation (now includes command execution fallback)
static StartupConfig ValidateAndGetStartupConfig(IConfiguration configuration, ICommandExecutorService executor)
{
    // Config or default path
    var configuredPath = configuration["Ghostscript:ExecutablePath"];

    string ghostscriptPath = configuredPath ?? @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";

    string baseParameters = configuration["Ghostscript:BaseParameters"] ?? "-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite  -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4  -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ";

    // If configured path missing or not found, try discovery via 'where'
    if (string.IsNullOrWhiteSpace(configuredPath) || !File.Exists(ghostscriptPath))
    {
        var configuredVersion = configuration["Ghostscript:Version"];

        ghostscriptPath = configuredVersion != null
            ? $@"C:\Program Files\gs\gs{configuredVersion}\bin\gswin64c.exe"
            : @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";

        if(!File.Exists(ghostscriptPath))
        {
            var (exitCode, output) = ((ICommandExecutorService)executor).RunCommand((string)"where", (string)"gswin64c.exe");
            if (exitCode == 0)
            {
                var first = output
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(p => File.Exists(p));
                if (first != null)
                {
                    ghostscriptPath = first;
                }
            }
        }
    }

    if (!File.Exists(ghostscriptPath))
        throw new InvalidOperationException($"Ghostscript executable not found. Checked '{ghostscriptPath}'. Set Ghostscript:ExecutablePath or install Ghostscript.");

    // Port
    var portRaw = configuration["ServiceHost:KestrelListenerPort"];
    int port;
    if (string.IsNullOrWhiteSpace(portRaw))
        port = 7015;
    else if (!int.TryParse(portRaw, out port) || port < 1 || port > 65535)
        throw new InvalidOperationException($"Invalid ServiceHost:KestrelListenerPort '{portRaw}'. Must be 1-65535.");

    // Timeout
    var timeoutRaw = configuration["Ghostscript:TimeoutInSeconds"] ?? configuration["Ghostscript:GhostscriptTimeoutSeconds"];
    int? timeout = null;
    if (!string.IsNullOrWhiteSpace(timeoutRaw))
    {
        if (int.TryParse(timeoutRaw, out var timeoutValue) && timeoutValue > 0)
        {
            timeout = timeoutValue;
        }
        else
        {
            throw new InvalidOperationException($"Invalid Ghostscript:TimeoutInSeconds '{timeoutRaw}'. Must be a positive integer.");
        }
    }

    return new StartupConfig(ghostscriptPath, baseParameters, port, timeout);
}

namespace PDFAConversionService
{
    internal record StartupConfig(string GhostscriptPath, string BaseParameters, int KestrelPort, int? TimeoutInSeconds = null);
}