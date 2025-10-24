using Microsoft.Extensions.Options;

namespace Atlas.Ledger.Api.Configuration;

/// <summary>
/// Configuration options for the Ledger API
/// </summary>
public class LedgerApiOptions
{
    public const string SectionName = "LedgerApi";

    /// <summary>
    /// Database connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Kafka bootstrap servers
    /// </summary>
    public string KafkaBootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Hedged read configuration
    /// </summary>
    public HedgedReadOptions HedgedRead { get; set; } = new();

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    /// <summary>
    /// Security configuration
    /// </summary>
    public SecurityOptions Security { get; set; } = new();
}

/// <summary>
/// Hedged read configuration options
/// </summary>
public class HedgedReadOptions
{
    /// <summary>
    /// Delay in milliseconds before starting database read
    /// </summary>
    public int DelayMs { get; set; } = 12;
}

/// <summary>
/// Retry policy configuration options
/// </summary>
public class RetryPolicyOptions
{
    /// <summary>
    /// Maximum number of retries
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum retry delay in seconds
    /// </summary>
    public int MaxRetryDelaySeconds { get; set; } = 5;
}

/// <summary>
/// Security configuration options
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// Enable security headers
    /// </summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    /// <summary>
    /// Enable CORS
    /// </summary>
    public bool EnableCors { get; set; } = false;
}

/// <summary>
/// Validates LedgerApiOptions configuration
/// </summary>
public class LedgerApiOptionsValidator : IValidateOptions<LedgerApiOptions>
{
    public ValidateOptionsResult Validate(string? name, LedgerApiOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("ConnectionString is required");
        }

        if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            failures.Add("RedisConnectionString is required");
        }

        if (string.IsNullOrWhiteSpace(options.KafkaBootstrapServers))
        {
            failures.Add("KafkaBootstrapServers is required");
        }

        if (options.HedgedRead.DelayMs < 0 || options.HedgedRead.DelayMs > 1000)
        {
            failures.Add("HedgedRead.DelayMs must be between 0 and 1000 milliseconds");
        }

        if (options.RetryPolicy.MaxRetries < 0 || options.RetryPolicy.MaxRetries > 10)
        {
            failures.Add("RetryPolicy.MaxRetries must be between 0 and 10");
        }

        if (options.RetryPolicy.MaxRetryDelaySeconds < 1 || options.RetryPolicy.MaxRetryDelaySeconds > 60)
        {
            failures.Add("RetryPolicy.MaxRetryDelaySeconds must be between 1 and 60 seconds");
        }

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}
