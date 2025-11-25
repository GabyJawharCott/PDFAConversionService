using FluentAssertions;
using PDFAConversionService.Models;
using PDFAConversionService.Validators;
using Xunit;

namespace PDFAConversionService.Tests.Validators
{
    public class PdfaConversionRequestValidatorTests
    {
        private readonly PdfaConversionRequestValidator _validator;

        public PdfaConversionRequestValidatorTests()
        {
            _validator = new PdfaConversionRequestValidator();
        }

        [Fact]
        public void Validate_WithEmptyBase64_ShouldFail()
        {
            // Arrange
            var request = new PdfaConversionRequest { Base64Pdf = string.Empty };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Base64 PDF string is required"));
        }

        [Fact]
        public void Validate_WithNullBase64_ShouldFail()
        {
            // Arrange
            var request = new PdfaConversionRequest { Base64Pdf = null! };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Base64 PDF string is required"));
        }

        [Fact]
        public void Validate_WithInvalidBase64_ShouldFail()
        {
            // Arrange
            var request = new PdfaConversionRequest { Base64Pdf = "invalid-base64!!!" };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid base64 format"));
        }

        [Fact]
        public void Validate_WithValidBase64_ShouldSucceed()
        {
            // Arrange
            var validBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
            var request = new PdfaConversionRequest { Base64Pdf = validBase64 };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_WithBase64ExceedingSizeLimit_ShouldFail()
        {
            // Arrange
            // Create a base64 string that exceeds 100 MB when decoded
            var largeByteArray = new byte[101 * 1024 * 1024]; // 101 MB
            Array.Fill(largeByteArray, (byte)0x25);
            var largeBase64 = Convert.ToBase64String(largeByteArray);
            var request = new PdfaConversionRequest { Base64Pdf = largeBase64 };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exceeds maximum allowed size"));
        }

        [Fact]
        public void Validate_WithValidSizeBase64_ShouldSucceed()
        {
            // Arrange
            // Create a base64 string that is within the 100 MB limit
            var smallByteArray = new byte[10 * 1024 * 1024]; // 10 MB
            Array.Fill(smallByteArray, (byte)0x25);
            var smallBase64 = Convert.ToBase64String(smallByteArray);
            var request = new PdfaConversionRequest { Base64Pdf = smallBase64 };

            // Act
            var result = _validator.Validate(request);

            // Assert
            result.IsValid.Should().BeTrue();
        }
    }
}

