namespace PDFAConversionService.Services
{
    public interface IPdfaConversionService
    {
        Task<string> ConvertToPdfAAsync(string base64Pdf);
    }
}