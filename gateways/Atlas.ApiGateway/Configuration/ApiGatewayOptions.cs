using Microsoft.Extensions.Options;

namespace Atlas.ApiGateway.Configuration;

/// <summary>
/// Configuration options for the API Gateway
/// </summary>
public class ApiGatewayOptions
{
    public const string SectionName = "ApiGateway";

    /// <summary>
    /// Authentication configuration
    /// </summary>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    public RateLimitingOptions RateLimiting { get; set; } = new();

    /// <summary>
    /// CORS configuration
    /// </summary>
    public CorsOptions Cors { get; set; } = new();

    /// <summary>
    /// Security configuration
    /// </summary>
    public SecurityOptions Security { get; set; } = new();
}

/// <summary>
/// Authentication configuration options
/// </summary>
public class AuthenticationOptions
{
    /// <summary>
    /// JWT authority URL
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// JWT audience
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Require HTTPS metadata
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}

/// <summary>
/// Rate limiting configuration options
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Permits per minute
    /// </summary>
    public int PermitPerMinute { get; set; } = 120;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string Redis { get; set; } = string.Empty;
}

/// <summary>
/// CORS configuration options
/// </summary>
public class CorsOptions
{
    /// <summary>
    /// Allowed origins
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Security configuration options
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// Enable HTTPS redirection
    /// </summary>
    public bool EnableHttpsRedirection { get; set; } = true;

    /// <summary>
    /// Enable HSTS
    /// </summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>
    /// HSTS max age in days
    /// </summary>
    public int HstsMaxAgeDays { get; set; } = 365;
}

/// <summary>
/// Validator for API Gateway configuration
/// </summary>
public class ApiGatewayOptionsValidator : IValidateOptions<ApiGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiGatewayOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Authentication.Authority))
        {
            failures.Add("Authentication.Authority is required");
        }

        if (string.IsNullOrWhiteSpace(options.Authentication.Audience))
        {
            failures.Add("Authentication.Audience is required");
        }

        if (options.RateLimiting.PermitPerMinute <= 0)
        {
            failures.Add("RateLimiting.PermitPerMinute must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(options.RateLimiting.Redis))
        {
            failures.Add("RateLimiting.Redis connection string is required");
        }

        if (options.Cors.AllowedOrigins.Length == 0)
        {
            failures.Add("Cors.AllowedOrigins must contain at least one origin");
        }

        if (options.Security.HstsMaxAgeDays <= 0)
        {
            failures.Add("Security.HstsMaxAgeDays must be greater than 0");
        }

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}
