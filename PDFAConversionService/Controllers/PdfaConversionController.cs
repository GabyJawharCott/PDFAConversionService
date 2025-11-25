using Microsoft.AspNetCore.Mvc;
using PDFAConversionService.Models;
using PDFAConversionService.Services;

namespace PDFAConversionService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfaConversionController : ControllerBase
    {
        private readonly IPdfaConversionService _conversionService;
        private readonly ILogger<PdfaConversionController> _logger;

        public PdfaConversionController(IPdfaConversionService conversionService, ILogger<PdfaConversionController> logger)
        {
            _conversionService = conversionService;
            _logger = logger;
        }

        [HttpPost("convert")]
        public async Task<ActionResult<PdfaConversionResponse>> ConvertToPdfA([FromBody] PdfaConversionRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new PdfaConversionResponse
                    {
                        Success = false,
                        ErrorMessage = "Request body is required"
                    });
                }

                // FluentValidation will handle validation automatically if configured
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new PdfaConversionResponse
                    {
                        Success = false,
                        ErrorMessage = string.Join("; ", errors)
                    });
                }

                _logger.LogInformation("Processing PDF conversion request");

                var convertedBase64 = await _conversionService.ConvertToPdfAAsync(request.Base64Pdf);

                return Ok(new PdfaConversionResponse
                {
                    Success = true,
                    Base64PdfA = convertedBase64
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request argument: {Message}", ex.Message);
                return BadRequest(new PdfaConversionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "Conversion timeout: {Message}", ex.Message);
                return StatusCode(504, new PdfaConversionResponse
                {
                    Success = false,
                    ErrorMessage = "Conversion timed out. The PDF may be too large, complex or wrong settings has been set. Please try again or contact support."
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Conversion operation failed: {Message}", ex.Message);
                return StatusCode(500, new PdfaConversionResponse
                {
                    Success = false,
                    ErrorMessage = $"Conversion failed: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing conversion request");
                return StatusCode(500, new PdfaConversionResponse
                {
                    Success = false,
                    ErrorMessage = "An unexpected error occurred during conversion. Please try again or contact support."
                });
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }
    }
}