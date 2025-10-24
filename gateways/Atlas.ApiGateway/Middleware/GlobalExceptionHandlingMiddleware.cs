using System.Text.Json;

namespace Atlas.ApiGateway.Middleware;

/// <summary>
/// Global exception handling middleware for the API Gateway
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. RequestId: {RequestId}", 
                context.TraceIdentifier);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = new
            {
                code = GetErrorCode(exception),
                message = GetErrorMessage(exception),
                requestId = context.TraceIdentifier,
                timestamp = DateTimeOffset.UtcNow
            }
        };

        context.Response.StatusCode = GetStatusCode(exception);
        
        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await context.Response.WriteAsync(jsonResponse);
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => 401,
        ArgumentException => 400,
        InvalidOperationException => 400,
        TimeoutException => 504,
        HttpRequestException => 502,
        _ => 500
    };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "UNAUTHORIZED",
        ArgumentException => "INVALID_ARGUMENT",
        InvalidOperationException => "INVALID_OPERATION",
        TimeoutException => "TIMEOUT",
        HttpRequestException => "SERVICE_UNAVAILABLE",
        _ => "INTERNAL_ERROR"
    };

    private static string GetErrorMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Authentication required",
        ArgumentException => "Invalid request parameters",
        InvalidOperationException => "Invalid operation",
        TimeoutException => "Request timeout",
        HttpRequestException => "Service temporarily unavailable",
        _ => "An internal error occurred"
    };
}
