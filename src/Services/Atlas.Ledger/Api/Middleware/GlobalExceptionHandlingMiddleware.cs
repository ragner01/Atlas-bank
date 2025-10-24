using System.Net;
using System.Text.Json;
using Atlas.Ledger.Api.Utilities;
using Atlas.Ledger.Api.Models;

namespace Atlas.Ledger.Api.Middleware;

/// <summary>
/// Global exception handling middleware with standardized error responses
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Mask sensitive data in logs
            var maskedMessage = DataMasking.MaskSensitiveData(ex.Message);
            _logger.LogError(ex, "An unhandled exception occurred with correlation ID {CorrelationId}: {Message}", 
                correlationId, maskedMessage);
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentNullException => ErrorResponse.ValidationError(
                "Required parameter is missing", 
                new[] { exception.Message }, 
                correlationId),
            ArgumentException => ErrorResponse.ValidationError(
                "Invalid request parameters", 
                new[] { exception.Message }, 
                correlationId),
            InvalidOperationException => ErrorResponse.BusinessError(
                "INVALID_OPERATION", 
                exception.Message, 
                correlationId),
            UnauthorizedAccessException => ErrorResponse.Create(
                "UNAUTHORIZED", 
                "Access denied", 
                _environment.IsDevelopment() ? exception.Message : null, 
                correlationId),
            NotImplementedException => ErrorResponse.Create(
                "NOT_IMPLEMENTED", 
                "Feature not implemented", 
                _environment.IsDevelopment() ? exception.Message : null, 
                correlationId),
            TimeoutException => ErrorResponse.Create(
                "TIMEOUT", 
                "Request timeout", 
                _environment.IsDevelopment() ? exception.Message : null, 
                correlationId),
            _ => ErrorResponse.SystemError(
                "An internal server error occurred", 
                _environment.IsDevelopment() ? exception.Message : null, 
                correlationId)
        };

        context.Response.StatusCode = exception switch
        {
            ArgumentNullException or ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            NotImplementedException => (int)HttpStatusCode.NotImplemented,
            TimeoutException => (int)HttpStatusCode.RequestTimeout,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
