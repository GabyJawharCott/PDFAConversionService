using System.Diagnostics;
using System.Text;

namespace PDFAConversionService.Services
{
    public class ProcessService : IProcessService
    {
        private readonly ILogger<ProcessService> _logger;

        public ProcessService(ILogger<ProcessService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessExecutionResult> ExecuteAsync(
            string fileName,
            string arguments,
            int timeoutInSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            Process? process = null;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

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

                _logger.LogInformation("Starting process: {FileName} {Arguments}", fileName, arguments);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));

                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Process exceeded timeout of {TimeoutSeconds} seconds: {FileName}", timeoutInSeconds, fileName);
                    
                    if (process != null && !process.HasExited)
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                            await Task.Delay(1000, cancellationToken);
                        }
                        catch (Exception killEx)
                        {
                            _logger.LogWarning(killEx, "Error while killing timed-out process");
                        }
                    }

                    return new ProcessExecutionResult
                    {
                        ExitCode = -1,
                        StandardOutput = outputBuilder.ToString(),
                        StandardError = errorBuilder.ToString(),
                        TimedOut = true,
                        ExecutionTime = DateTime.UtcNow - startTime
                    };
                }

                var executionTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Process completed with exit code {ExitCode} in {ExecutionTime}ms", 
                    process.ExitCode, executionTime.TotalMilliseconds);

                return new ProcessExecutionResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = outputBuilder.ToString(),
                    StandardError = errorBuilder.ToString(),
                    TimedOut = false,
                    ExecutionTime = executionTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing process: {FileName}", fileName);
                
                // Ensure process is killed on any exception
                if (process != null && !process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogWarning(killEx, "Error while killing process after exception");
                    }
                }

                throw new InvalidOperationException($"Process execution failed: {ex.Message}", ex);
            }
            finally
            {
                // Ensure process is properly disposed
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error during process cleanup");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }
    }
}

