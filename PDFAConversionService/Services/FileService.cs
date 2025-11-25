using Microsoft.Extensions.Options;
using PDFAConversionService.Models;

namespace PDFAConversionService.Services
{
    public class FileService : IFileService
    {
        private readonly string _tempDirectory;
        private readonly ILogger<FileService> _logger;

        public FileService(IOptions<GhostscriptOptions> options, ILogger<FileService> logger)
        {
            _logger = logger;
            _tempDirectory = options.Value.TempDirectory ?? Path.Combine(Path.GetTempPath(), "PdfaConversion");
            
            try
            {
                Directory.CreateDirectory(_tempDirectory);
                _logger.LogInformation("Temp directory initialized: {TempDirectory}", _tempDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create temp directory: {TempDirectory}", _tempDirectory);
                throw new InvalidOperationException($"Failed to create temp directory: {_tempDirectory}", ex);
            }
        }

        public string CreateTempFile(string extension = ".pdf")
        {
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_tempDirectory, fileName);
            _logger.LogDebug("Created temp file path: {FilePath}", filePath);
            return filePath;
        }

        public async Task WriteBytesAsync(string filePath, byte[] bytes)
        {
            try
            {
                await File.WriteAllBytesAsync(filePath, bytes);
                _logger.LogDebug("Wrote {Size} bytes to file: {FilePath}", bytes.Length, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write bytes to file: {FilePath}", filePath);
                throw;
            }
        }

        public async Task<byte[]> ReadBytesAsync(string filePath)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);
                _logger.LogDebug("Read {Size} bytes from file: {FilePath}", bytes.Length, filePath);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read bytes from file: {FilePath}", filePath);
                throw;
            }
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public async Task DeleteFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogDebug("Deleted file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file: {FilePath}", filePath);
                // Don't throw - file cleanup failures shouldn't break the flow
            }
            await Task.CompletedTask;
        }

        public long GetFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file size: {FilePath}", filePath);
                return 0;
            }
        }
    }
}

