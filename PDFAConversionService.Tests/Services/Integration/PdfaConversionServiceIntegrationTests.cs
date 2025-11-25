using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using PDFAConversionService.Tests.Helpers;
using Xunit;

namespace PDFAConversionService.Tests.Services.Integration
{
    /// <summary>
    /// Integration tests that require actual Ghostscript installation
    /// These tests will be skipped if Ghostscript is not available
    /// </summary>
    public class PdfaConversionServiceIntegrationTests : IDisposable
    {
        private readonly ILogger<PdfaConversionService>? _logger;
        private readonly IFileService? _fileService;
        private readonly IProcessService? _processService;
        private readonly IOptions<GhostscriptOptions>? _options;
        private readonly PdfaConversionService? _service;
        private readonly string _ghostscriptPath;
        private readonly string? _tempDirectory;

        public PdfaConversionServiceIntegrationTests()
        {
            // Get Ghostscript path using the same discovery logic as the main application
            _ghostscriptPath = GhostscriptPathHelper.GetGhostscriptPath();
            
            if (!File.Exists(_ghostscriptPath))
            {
                // Skip tests if Ghostscript is not available
                return;
            }

            _tempDirectory = Path.Combine(Path.GetTempPath(), "PdfaConversionTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            _logger = Mock.Of<ILogger<PdfaConversionService>>();
            _fileService = new FileService(
                Options.Create(new GhostscriptOptions { TempDirectory = _tempDirectory }),
                Mock.Of<ILogger<FileService>>());
            _processService = new ProcessService(Mock.Of<ILogger<ProcessService>>());

            var ghostscriptOptions = new GhostscriptOptions
            {
                ExecutablePath = _ghostscriptPath,
                BaseParameters = "-dNOPAUSE -dBATCH -dSAFER -sDEVICE=pdfwrite -dPDFA=1 -dPDFACompatibilityPolicy=1 -dCompatibilityLevel=1.4 -dEmbedAllFonts=true -dSubsetFonts=true -sColorConversionStrategy=UseDeviceIndependentColor -sProcessColorModel=DeviceRGB -dDownsampleColorImages=false -dDownsampleGrayImages=false -dDownsampleMonoImages=false -dColorImageFilter=/FlateEncode -dGrayImageFilter=/FlateEncode -dMonoImageFilter=/CCITTFaxEncode ",
                TimeoutInSeconds = 300,
                TempDirectory = _tempDirectory
            };

            _options = Options.Create(ghostscriptOptions);
            _service = new PdfaConversionService(_logger, _fileService, _processService, _options);
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WithValidPdf_ShouldConvertSuccessfully()
        {
            // Skip if Ghostscript is not available or service not initialized
            if (!File.Exists(_ghostscriptPath) || _service == null)
            {
                return;
            }

            // Arrange - Create a minimal valid PDF (PDF header)
            var pdfBytes = new byte[]
            {
                0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, // %PDF-1.4
                0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, // Binary comment
                0x0A, 0x31, 0x20, 0x30, 0x20, 0x6F, 0x62, 0x6A, // 1 0 obj
                0x0A, 0x3C, 0x3C, 0x2F, 0x54, 0x79, 0x70, 0x65, 0x2F, 0x43, 0x61, 0x74, 0x61, 0x6C, 0x6F, 0x67, 0x3E, 0x3E, // << /Type/Catalog >>
                0x0A, 0x65, 0x6E, 0x64, 0x6F, 0x62, 0x6A, // endobj
                0x0A, 0x78, 0x72, 0x65, 0x66, // xref
                0x0A, 0x30, 0x20, 0x31, // 0 1
                0x0A, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x20, 0x36, 0x35, 0x35, 0x33, 0x35, 0x20, 0x66, // 0000000000 65535 f
                0x0A, 0x74, 0x72, 0x61, 0x69, 0x6C, 0x65, 0x72, // trailer
                0x0A, 0x3C, 0x3C, 0x2F, 0x53, 0x69, 0x7A, 0x65, 0x2F, 0x31, 0x3E, 0x3E, // << /Size/1 >>
                0x0A, 0x73, 0x74, 0x61, 0x72, 0x74, 0x78, 0x72, 0x65, 0x66, // startxref
                0x0A, 0x31, 0x30, 0x30, // 100
                0x0A, 0x25, 0x25, 0x45, 0x4F, 0x46 // %%EOF
            };

            var base64Pdf = Convert.ToBase64String(pdfBytes);

            // Act
            var result = await _service!.ConvertToPdfAAsync(base64Pdf);

            // Assert
            result.Should().NotBeNullOrEmpty();
            var resultBytes = Convert.FromBase64String(result);
            resultBytes.Length.Should().BeGreaterThan(0);
            
            // Verify it's a PDF (starts with %PDF)
            var resultString = System.Text.Encoding.ASCII.GetString(resultBytes, 0, Math.Min(8, resultBytes.Length));
            resultString.Should().StartWith("%PDF");
        }

        public void Dispose()
        {
            // Cleanup
            if (_tempDirectory != null && Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}

