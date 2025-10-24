using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AtlasBank.Infrastructure.ErrorHandling;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IErrorLogger _errorLogger;

    public GlobalExceptionMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionMiddleware> logger,
        IErrorLogger errorLogger)
    {
        _next = next;
        _logger = logger;
        _errorLogger = errorLogger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;

        _logger.LogError(exception, 
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
            correlationId, requestPath, requestMethod);

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = requestPath,
            Method = requestMethod
        };

        switch (exception)
        {
            case ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "VALIDATION_ERROR",
                    Message = "Request validation failed",
                    Details = validationEx.Errors
                };
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "UNAUTHORIZED",
                    Message = "Access denied"
                };
                break;

            case ArgumentException argEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INVALID_ARGUMENT",
                    Message = argEx.Message
                };
                break;

            case TimeoutException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "TIMEOUT",
                    Message = "Request timeout"
                };
                break;

            case HttpRequestException httpEx:
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "EXTERNAL_SERVICE_ERROR",
                    Message = "External service unavailable"
                };
                break;

            case InvalidOperationException invalidOpEx:
                response.StatusCode = (int)HttpStatusCode.Conflict;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INVALID_OPERATION",
                    Message = invalidOpEx.Message
                };
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Error = new ErrorDetail
                {
                    Code = "INTERNAL_SERVER_ERROR",
                    Message = "An unexpected error occurred"
                };

                // Log critical errors
                await _errorLogger.LogCriticalErrorAsync(
                    $"Unhandled exception in {requestMethod} {requestPath}",
                    exception,
                    new Dictionary<string, object>
                    {
                        ["CorrelationId"] = correlationId,
                        ["RequestPath"] = requestPath,
                        ["RequestMethod"] = requestMethod,
                        ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                        ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString()
                    });
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await response.WriteAsync(jsonResponse);
    }
}

/// <summary>
/// Custom validation exception
/// </summary>
public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors) : base("Validation failed")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error) : base("Validation failed")
    {
        Errors = new Dictionary<string, string[]> { [field] = new[] { error } };
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
    public ErrorDetail Error { get; set; } = new();
}

public class ErrorDetail
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}

/// <summary>
/// API error result for consistent error responses
/// </summary>
public class ApiErrorResult
{
    public static IResult BadRequest(string message, object? details = null)
    {
        return Results.BadRequest(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "BAD_REQUEST",
                Message = message,
                Details = details
            }
        });
    }

    public static IResult Unauthorized(string message = "Unauthorized")
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "UNAUTHORIZED",
                Message = message
            }
        }, statusCode: 401);
    }

    public static IResult Forbidden(string message = "Forbidden")
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "FORBIDDEN",
                Message = message
            }
        }, statusCode: 403);
    }

    public static IResult NotFound(string message = "Resource not found")
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "NOT_FOUND",
                Message = message
            }
        }, statusCode: 404);
    }

    public static IResult Conflict(string message, object? details = null)
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "CONFLICT",
                Message = message,
                Details = details
            }
        }, statusCode: 409);
    }

    public static IResult InternalServerError(string message = "Internal server error")
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = message
            }
        }, statusCode: 500);
    }

    public static IResult ServiceUnavailable(string message = "Service temporarily unavailable")
    {
        return Results.Json(new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = "SERVICE_UNAVAILABLE",
                Message = message
            }
        }, statusCode: 503);
    }
}
