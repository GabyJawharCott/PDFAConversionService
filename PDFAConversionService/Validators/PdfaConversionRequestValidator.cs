using FluentValidation;
using PDFAConversionService.Models;

namespace PDFAConversionService.Validators
{
    public class PdfaConversionRequestValidator : AbstractValidator<PdfaConversionRequest>
    {
        private const int MaxInputSizeBytes = 100 * 1024 * 1024; // 100 MB limit

        public PdfaConversionRequestValidator()
        {
            RuleFor(x => x.Base64Pdf)
                .NotEmpty()
                .WithMessage("Base64 PDF string is required")
                .Must(BeValidBase64)
                .WithMessage("Invalid base64 format")
                .Must(NotExceedSizeLimit)
                .WithMessage($"Input PDF size exceeds maximum allowed size ({MaxInputSizeBytes / 1024 / 1024} MB)");
        }

        private bool BeValidBase64(string? base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return false;

            try
            {
                // Validate base64 format
                var bytes = Convert.FromBase64String(base64String);
                return bytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool NotExceedSizeLimit(string? base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return true;

            try
            {
                // Estimate size (base64 is ~33% larger than binary)
                var estimatedSize = (base64String.Length * 3) / 4;
                if (estimatedSize > MaxInputSizeBytes)
                    return false;

                // Validate actual decoded size
                var bytes = Convert.FromBase64String(base64String);
                return bytes.Length <= MaxInputSizeBytes;
            }
            catch
            {
                return false;
            }
        }
    }
}

