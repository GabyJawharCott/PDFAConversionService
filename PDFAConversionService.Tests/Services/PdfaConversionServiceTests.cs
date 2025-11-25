using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using Xunit;

namespace PDFAConversionService.Tests.Services
{
    public class PdfaConversionServiceTests
    {
        private readonly Mock<ILogger<PdfaConversionService>> _loggerMock;
        private readonly Mock<IFileService> _fileServiceMock;
        private readonly Mock<IProcessService> _processServiceMock;
        private readonly Mock<IOptions<GhostscriptOptions>> _optionsMock;
        private readonly PdfaConversionService _service;

        public PdfaConversionServiceTests()
        {
            _loggerMock = new Mock<ILogger<PdfaConversionService>>();
            _fileServiceMock = new Mock<IFileService>();
            _processServiceMock = new Mock<IProcessService>();
            _optionsMock = new Mock<IOptions<GhostscriptOptions>>();

            var options = new GhostscriptOptions
            {
                ExecutablePath = "C:\\Program Files\\gs\\gs10.06.0\\bin\\gswin64c.exe",
                BaseParameters = "-dNOPAUSE -dBATCH -dSAFER",
                TimeoutInSeconds = 300
            };

            _optionsMock.Setup(x => x.Value).Returns(options);

            _service = new PdfaConversionService(
                _loggerMock.Object,
                _fileServiceMock.Object,
                _processServiceMock.Object,
                _optionsMock.Object);
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WithNullBase64_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.ConvertToPdfAAsync(null!));
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WithEmptyBase64_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.ConvertToPdfAAsync(string.Empty));
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WithInvalidBase64_ThrowsArgumentException()
        {
            // Arrange
            var invalidBase64 = "invalid-base64-string!!!";

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.ConvertToPdfAAsync(invalidBase64));
            exception.Message.Should().Contain("Invalid base64 format");
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WithValidPdf_ReturnsBase64String()
        {
            // Arrange
            var inputBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF" in bytes
            var inputPath = "input.pdf";
            var outputPath = "output.pdf";
            var outputBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
            var expectedBase64 = Convert.ToBase64String(outputBytes);

            _fileServiceMock.SetupSequence(x => x.CreateTempFile(".pdf"))
                .Returns(inputPath)
                .Returns(outputPath);

            _fileServiceMock.Setup(x => x.WriteBytesAsync(inputPath, It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask);

            _fileServiceMock.Setup(x => x.ReadBytesAsync(outputPath))
                .ReturnsAsync(outputBytes);

            _fileServiceMock.Setup(x => x.FileExists(outputPath))
                .Returns(true);

            _fileServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _processServiceMock.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult
                {
                    ExitCode = 0,
                    TimedOut = false,
                    StandardOutput = "Success",
                    StandardError = string.Empty,
                    ExecutionTime = TimeSpan.FromSeconds(1)
                });

            // Act
            var result = await _service.ConvertToPdfAAsync(inputBase64);

            // Assert
            result.Should().Be(expectedBase64);
            _fileServiceMock.Verify(x => x.WriteBytesAsync(inputPath, It.IsAny<byte[]>()), Times.Once);
            _fileServiceMock.Verify(x => x.ReadBytesAsync(outputPath), Times.Once);
            _fileServiceMock.Verify(x => x.DeleteFileAsync(inputPath), Times.Once);
            _fileServiceMock.Verify(x => x.DeleteFileAsync(outputPath), Times.Once);
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WhenProcessTimesOut_ThrowsTimeoutException()
        {
            // Arrange
            var inputBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            var inputPath = "input.pdf";
            var outputPath = "output.pdf";

            _fileServiceMock.SetupSequence(x => x.CreateTempFile(".pdf"))
                .Returns(inputPath)
                .Returns(outputPath);

            _fileServiceMock.Setup(x => x.WriteBytesAsync(inputPath, It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask);

            _fileServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _processServiceMock.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult
                {
                    ExitCode = -1,
                    TimedOut = true,
                    ExecutionTime = TimeSpan.FromSeconds(300)
                });

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _service.ConvertToPdfAAsync(inputBase64));
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WhenProcessFails_ThrowsInvalidOperationException()
        {
            // Arrange
            var inputBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            var inputPath = "input.pdf";
            var outputPath = "output.pdf";

            _fileServiceMock.SetupSequence(x => x.CreateTempFile(".pdf"))
                .Returns(inputPath)
                .Returns(outputPath);

            _fileServiceMock.Setup(x => x.WriteBytesAsync(inputPath, It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask);

            _fileServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _processServiceMock.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult
                {
                    ExitCode = 1,
                    TimedOut = false,
                    StandardError = "Ghostscript error",
                    ExecutionTime = TimeSpan.FromSeconds(1)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConvertToPdfAAsync(inputBase64));
            exception.Message.Should().Contain("Ghostscript conversion failed");
        }

        [Fact]
        public async Task ConvertToPdfAAsync_WhenOutputFileNotCreated_ThrowsInvalidOperationException()
        {
            // Arrange
            var inputBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            var inputPath = "input.pdf";
            var outputPath = "output.pdf";

            _fileServiceMock.SetupSequence(x => x.CreateTempFile(".pdf"))
                .Returns(inputPath)
                .Returns(outputPath);

            _fileServiceMock.Setup(x => x.WriteBytesAsync(inputPath, It.IsAny<byte[]>()))
                .Returns(Task.CompletedTask);

            _fileServiceMock.Setup(x => x.FileExists(outputPath))
                .Returns(false);

            _fileServiceMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _processServiceMock.Setup(x => x.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessExecutionResult
                {
                    ExitCode = 0,
                    TimedOut = false,
                    ExecutionTime = TimeSpan.FromSeconds(1)
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConvertToPdfAAsync(inputBase64));
            exception.Message.Should().Contain("output file was not created");
        }
    }
}

