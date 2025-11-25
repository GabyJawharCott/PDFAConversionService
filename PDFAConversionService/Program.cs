using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.EventLog;
using PDFAConversionService.Middleware;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using PDFAConversionService.Validators;

AppDomain.CurrentDomain.UnhandledException += (s, e) =>
{
    Console.Error.WriteLine("UNHANDLED: " + e.ExceptionObject);
};

var builder = WebApplication.CreateBuilder(args);

// Lifetime
if (Environment.UserInteractive)
    builder.Host.UseConsoleLifetime();
else
    builder.Host.UseWindowsService(o => o.ServiceName = "PDFAConversionService");

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
    builder.Logging.AddEventLog(new EventLogSettings { SourceName = "PDFAConversionService" });
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

// Configure Kestrel with request size limits
builder.WebHost.UseKestrel(options =>
{
    options.ListenLocalhost(cfg.KestrelPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    
    // Set request size limits (100 MB for base64 PDFs, with some overhead)
    options.Limits.MaxRequestBodySize = 120 * 1024 * 1024; // 120 MB
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32 KB
    options.Limits.MaxRequestLineSize = 8 * 1024; // 8 KB
});

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

    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL STARTUP ERROR: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex);
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

record StartupConfig(string GhostscriptPath, string BaseParameters, int KestrelPort, int? TimeoutInSeconds = null);