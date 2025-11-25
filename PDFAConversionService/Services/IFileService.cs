namespace PDFAConversionService.Services
{
    /// <summary>
    /// Service for managing temporary files used during PDF conversion
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Creates a temporary file with a unique name
        /// </summary>
        /// <param name="extension">File extension (e.g., ".pdf")</param>
        /// <returns>Full path to the created temporary file</returns>
        string CreateTempFile(string extension = ".pdf");

        /// <summary>
        /// Writes bytes to a file asynchronously
        /// </summary>
        Task WriteBytesAsync(string filePath, byte[] bytes);

        /// <summary>
        /// Reads bytes from a file asynchronously
        /// </summary>
        Task<byte[]> ReadBytesAsync(string filePath);

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        bool FileExists(string filePath);

        /// <summary>
        /// Deletes a file asynchronously
        /// </summary>
        Task DeleteFileAsync(string filePath);

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        long GetFileSize(string filePath);
    }
}

