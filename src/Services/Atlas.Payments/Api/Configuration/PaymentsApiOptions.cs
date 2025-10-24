using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Atlas.Payments.Api.Configuration;

/// <summary>
/// Configuration options for the Payments API
/// </summary>
public class PaymentsApiOptions
{
    public const string SectionName = "PaymentsApi";

    /// <summary>
    /// Maximum number of transfers per minute per tenant
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxTransfersPerMinute must be between 1 and 1000")]
    public int MaxTransfersPerMinute { get; set; } = 100;

    /// <summary>
    /// Maximum transfer amount in minor units
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxTransferAmount must be greater than 0")]
    public long MaxTransferAmount { get; set; } = 100000000; // 1M in minor units

    /// <summary>
    /// Idempotency key expiration time in minutes
    /// </summary>
    [Range(1, 1440, ErrorMessage = "IdempotencyExpirationMinutes must be between 1 and 1440")]
    public int IdempotencyExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Enable gRPC communication with Ledger
    /// </summary>
    public bool EnableGrpcLedger { get; set; } = true;

    /// <summary>
    /// Ledger service endpoint
    /// </summary>
    [Required(ErrorMessage = "LedgerEndpoint is required")]
    [Url(ErrorMessage = "LedgerEndpoint must be a valid URL")]
    public string LedgerEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Configuration options for Redis
/// </summary>
public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection timeout in milliseconds
    /// </summary>
    [Range(1000, 30000, ErrorMessage = "ConnectionTimeoutMs must be between 1000 and 30000")]
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Redis sync timeout in milliseconds
    /// </summary>
    [Range(1000, 30000, ErrorMessage = "SyncTimeoutMs must be between 1000 and 30000")]
    public int SyncTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Redis async timeout in milliseconds
    /// </summary>
    [Range(1000, 30000, ErrorMessage = "AsyncTimeoutMs must be between 1000 and 30000")]
    public int AsyncTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of connections in the pool
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxConnections must be between 1 and 100")]
    public int MaxConnections { get; set; } = 10;
}

/// <summary>
/// Configuration options for JWT authentication
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// JWT issuer
    /// </summary>
    [Required(ErrorMessage = "Issuer is required")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT audience
    /// </summary>
    [Required(ErrorMessage = "Audience is required")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// JWT authority
    /// </summary>
    [Required(ErrorMessage = "Authority is required")]
    [Url(ErrorMessage = "Authority must be a valid URL")]
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Clock skew tolerance in minutes
    /// </summary>
    [Range(0, 60, ErrorMessage = "ClockSkewMinutes must be between 0 and 60")]
    public int ClockSkewMinutes { get; set; } = 5;
}

/// <summary>
/// Configuration options for CORS
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Allowed origins for CORS
    /// </summary>
    [Required(ErrorMessage = "AllowedOrigins is required")]
    [MinLength(1, ErrorMessage = "At least one origin must be specified")]
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Allowed methods for CORS
    /// </summary>
    public string[] AllowedMethods { get; set; } = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

    /// <summary>
    /// Allowed headers for CORS
    /// </summary>
    public string[] AllowedHeaders { get; set; } = new[] { "Authorization", "Content-Type", "X-Tenant-Id", "X-Request-ID" };

    /// <summary>
    /// Preflight cache duration in minutes
    /// </summary>
    [Range(1, 60, ErrorMessage = "PreflightCacheMinutes must be between 1 and 60")]
    public int PreflightCacheMinutes { get; set; } = 10;
}

/// <summary>
/// Validator for PaymentsApiOptions
/// </summary>
public class PaymentsApiOptionsValidator : IValidateOptions<PaymentsApiOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentsApiOptions options)
    {
        var errors = new List<string>();

        if (options.MaxTransfersPerMinute <= 0)
            errors.Add("MaxTransfersPerMinute must be greater than 0");

        if (options.MaxTransferAmount <= 0)
            errors.Add("MaxTransferAmount must be greater than 0");

        if (options.IdempotencyExpirationMinutes <= 0)
            errors.Add("IdempotencyExpirationMinutes must be greater than 0");

        if (string.IsNullOrEmpty(options.LedgerEndpoint))
            errors.Add("LedgerEndpoint is required");

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Validator for RedisOptions
/// </summary>
public class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        var errors = new List<string>();

        if (options.ConnectionTimeoutMs <= 0)
            errors.Add("ConnectionTimeoutMs must be greater than 0");

        if (options.SyncTimeoutMs <= 0)
            errors.Add("SyncTimeoutMs must be greater than 0");

        if (options.AsyncTimeoutMs <= 0)
            errors.Add("AsyncTimeoutMs must be greater than 0");

        if (options.MaxConnections <= 0)
            errors.Add("MaxConnections must be greater than 0");

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Validator for JwtOptions
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(options.Issuer))
            errors.Add("Issuer is required");

        if (string.IsNullOrEmpty(options.Audience))
            errors.Add("Audience is required");

        if (string.IsNullOrEmpty(options.Authority))
            errors.Add("Authority is required");

        if (options.ClockSkewMinutes < 0)
            errors.Add("ClockSkewMinutes must be non-negative");

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Validator for CorsOptions
/// </summary>
public class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        var errors = new List<string>();

        if (options.AllowedOrigins == null || options.AllowedOrigins.Length == 0)
            errors.Add("At least one allowed origin must be specified");

        if (options.PreflightCacheMinutes <= 0)
            errors.Add("PreflightCacheMinutes must be greater than 0");

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
}
