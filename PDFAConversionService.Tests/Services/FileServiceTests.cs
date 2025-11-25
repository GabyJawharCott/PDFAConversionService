using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using Xunit;

namespace PDFAConversionService.Tests.Services
{
    public class FileServiceTests : IDisposable
    {
        private readonly Mock<ILogger<FileService>> _loggerMock;
        private readonly Mock<IOptions<GhostscriptOptions>> _optionsMock;
        private readonly string _tempDirectory;
        private readonly FileService _service;

        public FileServiceTests()
        {
            _loggerMock = new Mock<ILogger<FileService>>();
            _optionsMock = new Mock<IOptions<GhostscriptOptions>>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), "PdfaConversionTests", Guid.NewGuid().ToString());

            var options = new GhostscriptOptions
            {
                TempDirectory = _tempDirectory
            };

            _optionsMock.Setup(x => x.Value).Returns(options);

            _service = new FileService(_optionsMock.Object, _loggerMock.Object);
        }

        [Fact]
        public void CreateTempFile_ShouldReturnValidPath()
        {
            // Act
            var path = _service.CreateTempFile(".pdf");

            // Assert
            path.Should().NotBeNullOrEmpty();
            path.Should().EndWith(".pdf");
            path.Should().Contain(_tempDirectory);
        }

        [Fact]
        public async Task WriteBytesAsync_ShouldCreateFile()
        {
            // Arrange
            var testBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            var filePath = _service.CreateTempFile(".pdf");

            // Act
            await _service.WriteBytesAsync(filePath, testBytes);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var readBytes = await File.ReadAllBytesAsync(filePath);
            readBytes.Should().BeEquivalentTo(testBytes);
        }

        [Fact]
        public async Task ReadBytesAsync_WithExistingFile_ShouldReturnBytes()
        {
            // Arrange
            var testBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            var filePath = _service.CreateTempFile(".pdf");
            await File.WriteAllBytesAsync(filePath, testBytes);

            // Act
            var result = await _service.ReadBytesAsync(filePath);

            // Assert
            result.Should().BeEquivalentTo(testBytes);
        }

        [Fact]
        public void FileExists_WithExistingFile_ShouldReturnTrue()
        {
            // Arrange
            var filePath = _service.CreateTempFile(".pdf");
            File.WriteAllBytes(filePath, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            // Act
            var result = _service.FileExists(filePath);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void FileExists_WithNonExistingFile_ShouldReturnFalse()
        {
            // Arrange
            var filePath = Path.Combine(_tempDirectory, "nonexistent.pdf");

            // Act
            var result = _service.FileExists(filePath);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileAsync_WithExistingFile_ShouldDeleteFile()
        {
            // Arrange
            var filePath = _service.CreateTempFile(".pdf");
            await File.WriteAllBytesAsync(filePath, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            // Act
            await _service.DeleteFileAsync(filePath);

            // Assert
            File.Exists(filePath).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteFileAsync_WithNonExistingFile_ShouldNotThrow()
        {
            // Arrange
            var filePath = Path.Combine(_tempDirectory, "nonexistent.pdf");

            // Act & Assert
            await _service.DeleteFileAsync(filePath); // Should not throw
        }

        [Fact]
        public void GetFileSize_WithExistingFile_ShouldReturnCorrectSize()
        {
            // Arrange
            var testBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
            var filePath = _service.CreateTempFile(".pdf");
            File.WriteAllBytes(filePath, testBytes);

            // Act
            var size = _service.GetFileSize(filePath);

            // Assert
            size.Should().Be(testBytes.Length);
        }

        [Fact]
        public void GetFileSize_WithNonExistingFile_ShouldReturnZero()
        {
            // Arrange
            var filePath = Path.Combine(_tempDirectory, "nonexistent.pdf");

            // Act
            var size = _service.GetFileSize(filePath);

            // Assert
            size.Should().Be(0);
        }

        public void Dispose()
        {
            // Cleanup
            if (Directory.Exists(_tempDirectory))
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

