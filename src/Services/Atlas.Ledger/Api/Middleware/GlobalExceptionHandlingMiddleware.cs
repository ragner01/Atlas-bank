using System.Net;
using System.Text.Json;
using Atlas.Ledger.Api.Utilities;

namespace Atlas.Ledger.Api.Middleware;

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
            // Mask sensitive data in logs
            var maskedMessage = DataMasking.MaskSensitiveData(ex.Message);
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", maskedMessage);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentException => new ErrorResponse("Invalid request", HttpStatusCode.BadRequest),
            InvalidOperationException => new ErrorResponse("Operation not allowed", HttpStatusCode.BadRequest),
            UnauthorizedAccessException => new ErrorResponse("Unauthorized", HttpStatusCode.Unauthorized),
            NotImplementedException => new ErrorResponse("Feature not implemented", HttpStatusCode.NotImplemented),
            _ => new ErrorResponse("An internal server error occurred", HttpStatusCode.InternalServerError)
        };

        context.Response.StatusCode = (int)response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

public record ErrorResponse(string Message, HttpStatusCode StatusCode);
