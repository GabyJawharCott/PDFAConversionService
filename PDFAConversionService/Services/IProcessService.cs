namespace PDFAConversionService.Services
{
    /// <summary>
    /// Service for executing external processes with timeout and proper resource management
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Executes a process asynchronously with timeout support
        /// </summary>
        /// <param name="fileName">Path to the executable</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="timeoutInSeconds">Timeout in seconds (default: 300)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Process execution result</returns>
        Task<ProcessExecutionResult> ExecuteAsync(
            string fileName,
            string arguments,
            int timeoutInSeconds = 300,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of process execution
    /// </summary>
    public class ProcessExecutionResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public bool TimedOut { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}

