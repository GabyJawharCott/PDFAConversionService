namespace PDFAConversionService.Models
{
    public class GhostscriptOptions
    {
        public string? Version { get; set; }
        public string? ExecutablePath { get; set; }
        public string? TempDirectory { get; set; }
        public string? BaseParameters { get; set; }
        public int? TimeoutInSeconds { get; set; }
    }
}