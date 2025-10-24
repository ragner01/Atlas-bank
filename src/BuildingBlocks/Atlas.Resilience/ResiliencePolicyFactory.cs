using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net;

namespace Atlas.Resilience;

/// <summary>
/// Configuration options for resilience policies
/// </summary>
public class ResilienceOptions
{
    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker duration of break in seconds
    /// </summary>
    public int CircuitBreakerDurationOfBreakSeconds { get; set; } = 30;

    /// <summary>
    /// Retry count for transient failures
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Retry delay in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Timeout duration in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Bulkhead max concurrency
    /// </summary>
    public int BulkheadMaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Bulkhead max queued actions
    /// </summary>
    public int BulkheadMaxQueuedActions { get; set; } = 20;
}

/// <summary>
/// Validates ResilienceOptions configuration
/// </summary>
public class ResilienceOptionsValidator : IValidateOptions<ResilienceOptions>
{
    /// <summary>
    /// Validates the ResilienceOptions configuration
    /// </summary>
    /// <param name="name">The configuration section name</param>
    /// <param name="options">The options to validate</param>
    /// <returns>Validation result</returns>
    public ValidateOptionsResult Validate(string? name, ResilienceOptions options)
    {
        if (options == null)
            return ValidateOptionsResult.Fail("ResilienceOptions cannot be null");

        var failures = new List<string>();

        if (options.CircuitBreakerFailureThreshold <= 0)
            failures.Add("CircuitBreakerFailureThreshold must be greater than 0");

        if (options.CircuitBreakerDurationOfBreakSeconds <= 0)
            failures.Add("CircuitBreakerDurationOfBreakSeconds must be greater than 0");

        if (options.RetryCount < 0)
            failures.Add("RetryCount cannot be negative");

        if (options.RetryDelayMs <= 0)
            failures.Add("RetryDelayMs must be greater than 0");

        if (options.TimeoutSeconds <= 0)
            failures.Add("TimeoutSeconds must be greater than 0");

        if (options.BulkheadMaxConcurrency <= 0)
            failures.Add("BulkheadMaxConcurrency must be greater than 0");

        if (options.BulkheadMaxQueuedActions <= 0)
            failures.Add("BulkheadMaxQueuedActions must be greater than 0");

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Service for creating resilience policies
/// </summary>
public interface IResiliencePolicyFactory
{
    /// <summary>
    /// Creates a circuit breaker policy
    /// </summary>
    IAsyncPolicy<T> CreateCircuitBreakerPolicy<T>();

    /// <summary>
    /// Creates a retry policy for HTTP responses
    /// </summary>
    IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy();

    /// <summary>
    /// Creates a timeout policy
    /// </summary>
    IAsyncPolicy<T> CreateTimeoutPolicy<T>();

    /// <summary>
    /// Creates a bulkhead policy
    /// </summary>
    IAsyncPolicy<T> CreateBulkheadPolicy<T>();

    /// <summary>
    /// Creates a combined resilience policy for HTTP responses
    /// </summary>
    IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy();
}

/// <summary>
/// Implementation of IResiliencePolicyFactory
/// </summary>
public class ResiliencePolicyFactory : IResiliencePolicyFactory
{
    private readonly ResilienceOptions _options;
    private readonly ILogger<ResiliencePolicyFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the ResiliencePolicyFactory
    /// </summary>
    /// <param name="options">The resilience options</param>
    /// <param name="logger">The logger</param>
    public ResiliencePolicyFactory(IOptions<ResilienceOptions> options, ILogger<ResiliencePolicyFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a circuit breaker policy
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <returns>A circuit breaker policy</returns>
    public IAsyncPolicy<T> CreateCircuitBreakerPolicy<T>()
    {
        return Policy<T>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                _options.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(_options.CircuitBreakerDurationOfBreakSeconds),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Duration}s due to {Exception}", 
                        duration.TotalSeconds, exception.Exception?.Message ?? exception.Result?.ToString() ?? "Unknown");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open");
                });
    }

    /// <summary>
    /// Creates a retry policy for HTTP responses
    /// </summary>
    /// <returns>A retry policy for HTTP responses</returns>
    public IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .OrResult(response => 
                response.StatusCode == HttpStatusCode.TooManyRequests || 
                response.StatusCode >= HttpStatusCode.InternalServerError)
            .WaitAndRetryAsync(
                _options.RetryCount,
                retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMs * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}ms due to {Outcome}", 
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? outcome.Result?.ToString() ?? "Unknown");
                });
    }

    /// <summary>
    /// Creates a timeout policy
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <returns>A timeout policy</returns>
    public IAsyncPolicy<T> CreateTimeoutPolicy<T>()
    {
        return Policy.TimeoutAsync<T>(TimeSpan.FromSeconds(_options.TimeoutSeconds));
    }

    /// <summary>
    /// Creates a bulkhead policy
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <returns>A bulkhead policy</returns>
    public IAsyncPolicy<T> CreateBulkheadPolicy<T>()
    {
        return Policy.BulkheadAsync<T>(
            _options.BulkheadMaxConcurrency,
            _options.BulkheadMaxQueuedActions);
    }

    /// <summary>
    /// Creates a combined resilience policy for HTTP responses
    /// </summary>
    /// <returns>A combined resilience policy</returns>
    public IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy()
    {
        return Policy.WrapAsync(
            CreateBulkheadPolicy<HttpResponseMessage>(),
            CreateTimeoutPolicy<HttpResponseMessage>(),
            CreateRetryPolicy(),
            CreateCircuitBreakerPolicy<HttpResponseMessage>());
    }
}

/// <summary>
/// Extension methods for dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds resilience services to the service collection
    /// </summary>
    public static IServiceCollection AddResilience(this IServiceCollection services, Action<ResilienceOptions>? configureOptions = null)
    {
        services.Configure<ResilienceOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddSingleton<IValidateOptions<ResilienceOptions>, ResilienceOptionsValidator>();
        services.AddSingleton<IResiliencePolicyFactory, ResiliencePolicyFactory>();

        return services;
    }
}
