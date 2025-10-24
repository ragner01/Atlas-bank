using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using System.Net;

namespace Atlas.Ussd.Gateway.Resilience;

/// <summary>
/// Circuit breaker and resilience policies for external service calls
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Circuit breaker policy for external API calls
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    logger.LogWarning("Circuit breaker opened for {Duration} due to {Exception}", 
                        duration, exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset - service is healthy again");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open - testing service health");
                });
    }

    /// <summary>
    /// Retry policy with exponential backoff
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => 
                r.StatusCode == HttpStatusCode.RequestTimeout ||
                r.StatusCode == HttpStatusCode.TooManyRequests ||
                r.StatusCode >= HttpStatusCode.InternalServerError)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} in {Delay}ms due to {Exception}", 
                        retryCount, timespan.TotalMilliseconds, 
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Timeout policy for external calls
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(ILogger logger)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(10),
            onTimeout: (context, timespan, task) =>
            {
                logger.LogWarning("Request timed out after {Timeout}s", timespan.TotalSeconds);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Combined resilience policy
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger)
    {
        return Policy.WrapAsync(
            GetRetryPolicy(logger),
            GetCircuitBreakerPolicy(logger),
            GetTimeoutPolicy(logger));
    }
}

/// <summary>
/// Service for making resilient HTTP calls
/// </summary>
public class ResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(HttpClient httpClient, ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _policy = ResiliencePolicies.GetCombinedPolicy(logger);
    }

    /// <summary>
    /// Make a GET request with resilience policies
    /// </summary>
    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Making GET request to {Uri}", requestUri);
            return await _httpClient.GetAsync(requestUri, cancellationToken);
        });
    }

    /// <summary>
    /// Make a POST request with resilience policies
    /// </summary>
    public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(async () =>
        {
            _logger.LogDebug("Making POST request to {Uri}", requestUri);
            return await _httpClient.PostAsync(requestUri, content, cancellationToken);
        });
    }
}

/// <summary>
/// Global exception handling middleware
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            error = "An internal error occurred",
            correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault(),
            timestamp = DateTime.UtcNow
        };

        context.Response.StatusCode = exception switch
        {
            ArgumentException => 400,
            UnauthorizedAccessException => 401,
            NotImplementedException => 501,
            _ => 500
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}

