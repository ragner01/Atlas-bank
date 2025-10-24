using System.Net;

namespace Atlas.Ledger.Api.Models;

/// <summary>
/// Standardized error response model for consistent API responses
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Additional details for debugging (only in development)
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional context information
    /// </summary>
    public Dictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Creates a standardized error response
    /// </summary>
    public static ErrorResponse Create(string code, string message, string? details = null, 
        string? correlationId = null, Dictionary<string, object>? context = null)
    {
        return new ErrorResponse
        {
            Code = code,
            Message = message,
            Details = details,
            CorrelationId = correlationId,
            Context = context
        };
    }

    /// <summary>
    /// Creates a validation error response
    /// </summary>
    public static ErrorResponse ValidationError(string message, IEnumerable<string>? validationErrors = null, 
        string? correlationId = null)
    {
        var context = validationErrors != null ? new Dictionary<string, object> { ["validationErrors"] = validationErrors } : null;
        return Create("VALIDATION_ERROR", message, null, correlationId, context);
    }

    /// <summary>
    /// Creates a business logic error response
    /// </summary>
    public static ErrorResponse BusinessError(string code, string message, string? correlationId = null)
    {
        return Create(code, message, null, correlationId);
    }

    /// <summary>
    /// Creates a system error response
    /// </summary>
    public static ErrorResponse SystemError(string message, string? details = null, string? correlationId = null)
    {
        return Create("SYSTEM_ERROR", message, details, correlationId);
    }
}

/// <summary>
/// Standardized success response model
/// </summary>
public record SuccessResponse<T>
{
    /// <summary>
    /// The response data
    /// </summary>
    public T Data { get; init; } = default!;

    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; init; } = "Success";

    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a standardized success response
    /// </summary>
    public static SuccessResponse<T> Create(T data, string message = "Success", string? correlationId = null)
    {
        return new SuccessResponse<T>
        {
            Data = data,
            Message = message,
            CorrelationId = correlationId
        };
    }
}
