using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PDFAConversionService.Middleware
{
    /// <summary>
    /// Middleware to add correlation ID to requests for better traceability
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private const string CorrelationIdKey = "CorrelationId";

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Try to get correlation ID from request header, or generate a new one
            var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() 
                ?? Guid.NewGuid().ToString();

            // Add to response header
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            // Add to HttpContext items for use in logging
            context.Items[CorrelationIdKey] = correlationId;

            // Add to scope for structured logging
            using (_logger.BeginScope(new Dictionary<string, object> { { CorrelationIdKey, correlationId } }))
            {
                await _next(context);
            }
        }
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}

