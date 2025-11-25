using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PDFAConversionService.Controllers;
using PDFAConversionService.Models;
using PDFAConversionService.Services;
using Xunit;

namespace PDFAConversionService.Tests.Controllers
{
    public class PdfaConversionControllerTests
    {
        private readonly Mock<IPdfaConversionService> _conversionServiceMock;
        private readonly Mock<ILogger<PdfaConversionController>> _loggerMock;
        private readonly PdfaConversionController _controller;

        public PdfaConversionControllerTests()
        {
            _conversionServiceMock = new Mock<IPdfaConversionService>();
            _loggerMock = new Mock<ILogger<PdfaConversionController>>();
            _controller = new PdfaConversionController(_conversionServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ConvertToPdfA_WithNullRequest_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.ConvertToPdfA(null!);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = result.Result as BadRequestObjectResult;
            var response = badRequest!.Value as PdfaConversionResponse;
            response!.Success.Should().BeFalse();
            response.ErrorMessage.Should().Contain("Request body is required");
        }

        [Fact]
        public async Task ConvertToPdfA_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new PdfaConversionRequest { Base64Pdf = string.Empty };
            _controller.ModelState.AddModelError("Base64Pdf", "Base64 PDF string is required");

            // Act
            var result = await _controller.ConvertToPdfA(request);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task ConvertToPdfA_WithValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new PdfaConversionRequest 
            { 
                Base64Pdf = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }) 
            };
            var expectedBase64 = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 });

            _conversionServiceMock.Setup(x => x.ConvertToPdfAAsync(request.Base64Pdf))
                .ReturnsAsync(expectedBase64);

            // Act
            var result = await _controller.ConvertToPdfA(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as PdfaConversionResponse;
            response!.Success.Should().BeTrue();
            response.Base64PdfA.Should().Be(expectedBase64);
        }

        [Fact]
        public async Task ConvertToPdfA_WithArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var request = new PdfaConversionRequest 
            { 
                Base64Pdf = "invalid" 
            };

            _conversionServiceMock.Setup(x => x.ConvertToPdfAAsync(request.Base64Pdf))
                .ThrowsAsync(new ArgumentException("Invalid base64 format"));

            // Act
            var result = await _controller.ConvertToPdfA(request);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = result.Result as BadRequestObjectResult;
            var response = badRequest!.Value as PdfaConversionResponse;
            response!.Success.Should().BeFalse();
        }

        [Fact]
        public async Task ConvertToPdfA_WithTimeoutException_ReturnsGatewayTimeout()
        {
            // Arrange
            var request = new PdfaConversionRequest 
            { 
                Base64Pdf = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }) 
            };

            _conversionServiceMock.Setup(x => x.ConvertToPdfAAsync(request.Base64Pdf))
                .ThrowsAsync(new TimeoutException("Conversion timed out"));

            // Act
            var result = await _controller.ConvertToPdfA(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(504);
            var response = objectResult.Value as PdfaConversionResponse;
            response!.Success.Should().BeFalse();
            response.ErrorMessage.Should().Contain("timed out");
        }

        [Fact]
        public async Task ConvertToPdfA_WithInvalidOperationException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new PdfaConversionRequest 
            { 
                Base64Pdf = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 }) 
            };

            _conversionServiceMock.Setup(x => x.ConvertToPdfAAsync(request.Base64Pdf))
                .ThrowsAsync(new InvalidOperationException("Conversion failed"));

            // Act
            var result = await _controller.ConvertToPdfA(request);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = result.Result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            var response = objectResult.Value as PdfaConversionResponse;
            response!.Success.Should().BeFalse();
        }

        [Fact]
        public void HealthCheck_ReturnsOk()
        {
            // Act
            var result = _controller.HealthCheck();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().NotBeNull();
        }
    }
}

