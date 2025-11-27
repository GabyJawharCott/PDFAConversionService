using Microsoft.Extensions.Configuration;
using PDFAConversionService.Services;
using System;
using System.IO;
using System.Linq;

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
                    .AddJsonFile(Path.Combine(basePath, "..", "..", "..", "..", "Cott.PDFAConversion.Service", "appsettings.json"), optional: true)
                    .AddJsonFile(Path.Combine(basePath, "..", "..", "Cott.PDFAConversion.Service", "appsettings.json"), optional: true)
                    .AddEnvironmentVariables();

                configuration = builder.Build();
            }

            // Try discovery via 'where' command first to prefer installed version over configuration
            var executor = new CommandExecutorService();
            var (exitCode, output) = executor.RunCommand("where", "gswin64c.exe");

            if (exitCode == 0)
            {
                var first = output
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .FirstOrDefault(File.Exists);

                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first!;
                }
            }

            // Get configured path or use default (if discovery failed)
            var configuredPath = configuration["Ghostscript:ExecutablePath"];
            var ghostscriptPath = configuredPath ?? @"C:\Program Files\gs\gs10.06.0\bin\gswin64c.exe";

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

