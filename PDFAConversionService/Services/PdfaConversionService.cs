using Microsoft.Extensions.Options;
using PDFAConversionService.Models;
using PDFAConversionService.Services;

namespace PDFAConversionService.Services
{
    public class PdfaConversionService : IPdfaConversionService
    {
        private readonly ILogger<PdfaConversionService> _logger;
        private readonly IFileService _fileService;
        private readonly IProcessService _processService;
        private readonly string _ghostscriptPath;
        private readonly string _baseParameters;
        private readonly int _timeoutInSeconds;

        public PdfaConversionService(
            ILogger<PdfaConversionService> logger,
            IFileService fileService,
            IProcessService processService,
            IOptions<GhostscriptOptions> ghostscriptOptions)
        {
            _logger = logger;
            _fileService = fileService;
            _processService = processService;

            var opts = ghostscriptOptions.Value;
            _ghostscriptPath = opts.ExecutablePath!;
            _baseParameters = opts.BaseParameters!;
            _timeoutInSeconds = opts.TimeoutInSeconds ?? 300; // Default 5 minutes
        }

        public async Task<string> ConvertToPdfAAsync(string base64Pdf)
        {
            if (string.IsNullOrEmpty(base64Pdf))
                throw new ArgumentException("Base64 PDF string cannot be null or empty", nameof(base64Pdf));

            // Note: Input validation is now handled by FluentValidation in the controller
            // Decode base64
            byte[] pdfBytes;
            try
            {
                pdfBytes = Convert.FromBase64String(base64Pdf);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 format", nameof(base64Pdf), ex);
            }

            var inputPath = _fileService.CreateTempFile(".pdf");
            var outputPath = _fileService.CreateTempFile(".pdf");

            try
            {
                _logger.LogInformation("Starting PDF conversion process. Input size: {Size} KB", pdfBytes.Length / 1024);

                // Save input PDF to temp file
                await _fileService.WriteBytesAsync(inputPath, pdfBytes);

                // Convert to PDF/A-1b using Ghostscript
                await ConvertWithGhostscriptAsync(inputPath, outputPath);

                // Read converted file and encode to base64
                var convertedBytes = await _fileService.ReadBytesAsync(outputPath);
                var base64Result = Convert.ToBase64String(convertedBytes);

                _logger.LogInformation("PDF conversion completed successfully. Output size: {Size} KB", convertedBytes.Length / 1024);
                return base64Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during PDF conversion");
                throw;
            }
            finally
            {
                // Clean up temporary files
                await _fileService.DeleteFileAsync(inputPath);
                await _fileService.DeleteFileAsync(outputPath);
            }
        }

        private async Task ConvertWithGhostscriptAsync(string inputPath, string outputPath)
        {
            // Get the base parameters or set default ones
            string GSArgs = _baseParameters;

            if (string.IsNullOrWhiteSpace(GSArgs))
            {
                GSArgs = "-dNOPAUSE -dBATCH -dSAFER " +
                         "-sDEVICE=pdfwrite -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4 " +
                         "-dEmbedAllFonts=true -dSubsetFonts=true " +
                         "-sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB " +
                         "-dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false " +
                         "-dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ";
            }

            // Adding input and output paths (ensure space before output file parameter)
            GSArgs = GSArgs.TrimEnd() + $" -sOutputFile=\"{outputPath}\" \"{inputPath}\"";

            _logger.LogInformation("Executing Ghostscript with timeout: {TimeoutSeconds} seconds", _timeoutInSeconds);

            var result = await _processService.ExecuteAsync(
                _ghostscriptPath,
                GSArgs,
                _timeoutInSeconds);

            _logger.LogInformation("Ghostscript completed in {ExecutionTime}ms. Exit code: {ExitCode}",
                result.ExecutionTime.TotalMilliseconds, result.ExitCode);

            if (result.TimedOut)
            {
                throw new TimeoutException($"Ghostscript conversion exceeded timeout of {_timeoutInSeconds} seconds");
            }

            if (result.ExitCode != 0)
            {
                _logger.LogError("Ghostscript failed with exit code {ExitCode}. Error: {Error}",
                    result.ExitCode, result.StandardError);
                throw new InvalidOperationException(
                    $"Ghostscript conversion failed with exit code {result.ExitCode}: {result.StandardError}");
            }

            if (!_fileService.FileExists(outputPath))
            {
                throw new InvalidOperationException("Conversion completed but output file was not created");
            }

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                _logger.LogDebug("Ghostscript output: {Output}", result.StandardOutput);
            }
        }
    }
}