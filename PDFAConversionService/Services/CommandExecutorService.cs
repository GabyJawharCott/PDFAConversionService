using System.Diagnostics;
using System.Text;

namespace PDFAConversionService.Services
{
    public class CommandExecutorService : ICommandExecutorService
    {
        private const int DefaultTimeoutSeconds = 30; // Default 30 seconds for command execution

        public (int ExitCode, string Output) RunCommand(string fileName, string arguments, int? timeoutInSeconds = null)
        {
            var timeout = timeoutInSeconds ?? DefaultTimeoutSeconds;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    errorBuilder.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process with timeout
            bool exited = process.WaitForExit(timeout * 1000); // WaitForExit takes milliseconds

            if (!exited)
            {
                // Process didn't exit within timeout
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000); // Give it 5 seconds to terminate
                    }
                }
                catch
                {
                    // Ignore errors during kill
                }
                return (-1, $"Command execution timed out after {timeout} seconds");
            }

            // Wait a bit for async output reading to complete
            process.WaitForExit(); // Ensure process is fully exited
            Thread.Sleep(100); // Brief wait for async handlers to finish

            var combinedOutput = outputBuilder.ToString() + errorBuilder.ToString();
            return (process.ExitCode, combinedOutput);
        }
    }
}