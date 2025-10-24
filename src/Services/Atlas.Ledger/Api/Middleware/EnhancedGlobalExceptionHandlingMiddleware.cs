using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Atlas.Ledger.Api.Middleware;

/// <summary>
/// Enhanced global exception handling middleware with structured logging and correlation IDs
/// </summary>
public class EnhancedGlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnhancedGlobalExceptionHandlingMiddleware> _logger;

    public EnhancedGlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<EnhancedGlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Add correlation ID to response headers
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            
            // Add correlation ID to log context
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method,
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            });

            await _next(context);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            await HandleExceptionAsync(context, ex, correlationId, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context, 
        Exception exception, 
        string correlationId, 
        long elapsedMs)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method,
            ElapsedMs = elapsedMs
        };

        switch (exception)
        {
            case ArgumentException argEx:
                response.StatusCode = 400;
                errorResponse.Error = "Bad Request";
                errorResponse.Message = MaskSensitiveData(argEx.Message);
                errorResponse.Details = new { Parameter = argEx.ParamName };
                _logger.LogWarning(exception, "Bad request: {Message} for {Path}", argEx.Message, context.Request.Path);
                break;

            case UnauthorizedAccessException:
                response.StatusCode = 401;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "Access denied";
                _logger.LogWarning(exception, "Unauthorized access to {Path}", context.Request.Path);
                break;

            case InvalidOperationException invOpEx:
                response.StatusCode = 422;
                errorResponse.Error = "Unprocessable Entity";
                errorResponse.Message = MaskSensitiveData(invOpEx.Message);
                _logger.LogWarning(exception, "Invalid operation: {Message} for {Path}", invOpEx.Message, context.Request.Path);
                break;

            case TimeoutException:
                response.StatusCode = 408;
                errorResponse.Error = "Request Timeout";
                errorResponse.Message = "The request timed out";
                _logger.LogError(exception, "Request timeout for {Path}", context.Request.Path);
                break;

            case TaskCanceledException:
                response.StatusCode = 408;
                errorResponse.Error = "Request Timeout";
                errorResponse.Message = "The request was cancelled";
                _logger.LogWarning(exception, "Request cancelled for {Path}", context.Request.Path);
                break;

            default:
                response.StatusCode = 500;
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = "An unexpected error occurred";
                _logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);
                break;
        }

        // Log structured error information
        _logger.LogError("Error response: {StatusCode} - {Error} - {Message} - CorrelationId: {CorrelationId} - ElapsedMs: {ElapsedMs}",
            response.StatusCode, errorResponse.Error, errorResponse.Message, correlationId, elapsedMs);

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await response.WriteAsync(jsonResponse);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ??
                           context.Request.Headers["X-Request-ID"].FirstOrDefault() ??
                           Guid.NewGuid().ToString();

        return correlationId;
    }

    private static string MaskSensitiveData(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Mask common sensitive patterns
        var patterns = new[]
        {
            (@"password\s*=\s*[^\s,;]+", "password=***"),
            (@"token\s*=\s*[^\s,;]+", "token=***"),
            (@"key\s*=\s*[^\s,;]+", "key=***"),
            (@"secret\s*=\s*[^\s,;]+", "secret=***"),
            (@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", "****-****-****-****"), // Credit card
            (@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "***@***.***") // Email
        };

        var maskedMessage = message;
        foreach (var (pattern, replacement) in patterns)
        {
            maskedMessage = System.Text.RegularExpressions.Regex.Replace(maskedMessage, pattern, replacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return maskedMessage;
    }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}
