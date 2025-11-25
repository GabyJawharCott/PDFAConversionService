using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using PDFAConversionService.Services;

namespace PDFAConversionService.Tests.Helpers
{
    /// <summary>
    /// Helper class to discover Ghostscript executable path using the same logic as the main application
    /// </summary>
    public static class GhostscriptPathHelper
    {
        /// <summary>
        /// Discovers the Ghostscript executable path using the same logic as Program.cs
        /// </summary>
        public static string GetGhostscriptPath(IConfiguration? configuration = null)
        {
            // Load configuration from appsettings.json if not provided
            if (configuration == null)
            {
                var basePath = Directory.GetCurrentDirectory();
                var builder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile(Path.Combine(basePath, "..", "..", "..", "..", "PDFAConversionService", "appsettings.json"), optional: true)
                    .AddJsonFile(Path.Combine(basePath, "..", "..", "PDFAConversionService", "appsettings.json"), optional: true)
                    .AddEnvironmentVariables();

                configuration = builder.Build();
            }

            // Get configured path or use default
            var configuredPath = configuration["Ghostscript:ExecutablePath"];
            string ghostscriptPath = configuredPath ?? @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";

            // If configured path exists, use it
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(ghostscriptPath))
            {
                return ghostscriptPath;
            }

            // Try version-based path
            var configuredVersion = configuration["Ghostscript:Version"];
            ghostscriptPath = configuredVersion != null
                ? $@"C:\Program Files\gs\gs{configuredVersion}\bin\gswin64c.exe"
                : @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";

            if (File.Exists(ghostscriptPath))
            {
                return ghostscriptPath;
            }

            // Try discovery via 'where' command (same as Program.cs)
            var executor = new CommandExecutorService();
            var (exitCode, output) = executor.RunCommand("where", "gswin64c.exe");
            
            if (exitCode == 0)
            {
                var first = output
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(p => File.Exists(p.Trim()));
                
                if (first != null)
                {
                    return first.Trim();
                }
            }

            // Fall back to configured path or default
            return ghostscriptPath;
        }

        /// <summary>
        /// Checks if Ghostscript is available
        /// </summary>
        public static bool IsGhostscriptAvailable(IConfiguration? configuration = null)
        {
            var path = GetGhostscriptPath(configuration);
            return File.Exists(path);
        }
    }
}

