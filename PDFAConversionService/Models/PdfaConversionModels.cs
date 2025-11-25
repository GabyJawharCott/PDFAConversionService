using System.ComponentModel.DataAnnotations;

namespace PDFAConversionService.Models
{
    public class PdfaConversionRequest
    {
        [Required(ErrorMessage = "Base64 PDF string is required")]
        public string Base64Pdf { get; set; } = string.Empty;
    }

    public class PdfaConversionResponse
    {
        public bool Success { get; set; }
        public string Base64PdfA { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}