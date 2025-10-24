using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AtlasBank.Infrastructure.Configuration;

/// <summary>
/// Secure configuration management
/// </summary>
public static class SecureConfigurationManagement
{
    public static void ConfigureSecureSettings(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure and validate all settings
        services.ConfigureAndValidate<DatabaseSettings>(configuration.GetSection("Database"));
        services.ConfigureAndValidate<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.ConfigureAndValidate<RedisSettings>(configuration.GetSection("Redis"));
        services.ConfigureAndValidate<KafkaSettings>(configuration.GetSection("Kafka"));
        services.ConfigureAndValidate<SecuritySettings>(configuration.GetSection("Security"));
        services.ConfigureAndValidate<MonitoringSettings>(configuration.GetSection("Monitoring"));
        services.ConfigureAndValidate<RateLimitSettings>(configuration.GetSection("RateLimit"));
        services.ConfigureAndValidate<EncryptionSettings>(configuration.GetSection("Encryption"));

        // Add configuration validation
        services.AddSingleton<IValidateOptions<DatabaseSettings>, DatabaseSettingsValidator>();
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddSingleton<IValidateOptions<SecuritySettings>, SecuritySettingsValidator>();
    }

    private static void ConfigureAndValidate<T>(this IServiceCollection services, IConfigurationSection section) where T : class
    {
        services.Configure<T>(section);
        services.AddSingleton<IValidateOptions<T>, ConfigurationValidator<T>>();
    }
}

/// <summary>
/// Database settings with validation
/// </summary>
public class DatabaseSettings
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int MaxRetryCount { get; set; } = 3;
    
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
    
    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 100;
    
    [Range(1, 1000)]
    public int MinPoolSize { get; set; } = 5;
    
    public bool EnableSensitiveDataLogging { get; set; } = false;
    
    public bool EnableDetailedErrors { get; set; } = false;
}

/// <summary>
/// JWT settings with validation
/// </summary>
public class JwtSettings
{
    [Required]
    [MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;
    
    [Required]
    public string Issuer { get; set; } = string.Empty;
    
    [Required]
    public string Audience { get; set; } = string.Empty;
    
    [Range(1, 1440)]
    public int ExpirationMinutes { get; set; } = 60;
    
    [Range(1, 30)]
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// Redis settings with validation
/// </summary>
public class RedisSettings
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int Database { get; set; } = 0;
    
    [Range(1, 300)]
    public int ConnectTimeoutSeconds { get; set; } = 30;
    
    [Range(1, 300)]
    public int SyncTimeoutSeconds { get; set; } = 30;
    
    public bool AbortOnConnectFail { get; set; } = false;
}

/// <summary>
/// Kafka settings with validation
/// </summary>
public class KafkaSettings
{
    [Required]
    public string BootstrapServers { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    [Range(1, 300)]
    public int RequestTimeoutMs { get; set; } = 30000;
    
    [Range(1, 100)]
    public int RetryCount { get; set; } = 3;
    
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 16384;
    
    [Range(1, 30000)]
    public int LingerMs { get; set; } = 5;
}

/// <summary>
/// Security settings with validation
/// </summary>
public class SecuritySettings
{
    [Required]
    [MinLength(32)]
    public string EncryptionKey { get; set; } = string.Empty;
    
    [Required]
    public string HmacSecret { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int MaxLoginAttempts { get; set; } = 5;
    
    [Range(1, 60)]
    public int LockoutDurationMinutes { get; set; } = 15;
    
    [Range(1, 100)]
    public int PasswordMinLength { get; set; } = 8;
    
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireNumbers { get; set; } = true;
    public bool RequireSpecialCharacters { get; set; } = true;
    
    [Range(1, 365)]
    public int PasswordExpirationDays { get; set; } = 90;
}

/// <summary>
/// Monitoring settings with validation
/// </summary>
public class MonitoringSettings
{
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    
    [Range(1, 1000)]
    public int MetricsPort { get; set; } = 9090;
    
    [Range(1, 1000)]
    public int TracingPort { get; set; } = 14268;
    
    public string JaegerEndpoint { get; set; } = string.Empty;
    public string PrometheusEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Rate limit settings with validation
/// </summary>
public class RateLimitSettings
{
    [Range(1, 10000)]
    public int RequestsPerMinute { get; set; } = 100;
    
    [Range(1, 1000)]
    public int RequestsPerHour { get; set; } = 1000;
    
    [Range(1, 10000)]
    public int RequestsPerDay { get; set; } = 10000;
    
    [Range(1, 100)]
    public int BurstLimit { get; set; } = 10;
    
    public bool EnableRateLimiting { get; set; } = true;
}

/// <summary>
/// Encryption settings with validation
/// </summary>
public class EncryptionSettings
{
    [Required]
    [MinLength(32)]
    public string AesKey { get; set; } = string.Empty;
    
    [Required]
    [MinLength(16)]
    public string AesIv { get; set; } = string.Empty;
    
    [Required]
    public string HsmEndpoint { get; set; } = string.Empty;
    
    [Required]
    public string HsmApiKey { get; set; } = string.Empty;
    
    public bool EnableHsm { get; set; } = false;
}

/// <summary>
/// Configuration validator base class
/// </summary>
public class ConfigurationValidator<T> : IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);
        
        if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
        {
            var errors = validationResults.Select(r => r.ErrorMessage).ToList();
            return ValidateOptionsResult.Fail(errors);
        }
        
        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Database settings validator
/// </summary>
public class DatabaseSettingsValidator : IValidateOptions<DatabaseSettings>
{
    public ValidateOptionsResult Validate(string? name, DatabaseSettings options)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            errors.Add("Database connection string is required");
        }
        
        if (options.MaxPoolSize < options.MinPoolSize)
        {
            errors.Add("Max pool size must be greater than or equal to min pool size");
        }
        
        if (options.EnableSensitiveDataLogging && options.EnableDetailedErrors)
        {
            errors.Add("Sensitive data logging and detailed errors should not be enabled in production");
        }
        
        return errors.Any() ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// JWT settings validator
/// </summary>
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(options.SecretKey))
        {
            errors.Add("JWT secret key is required");
        }
        else if (options.SecretKey.Length < 32)
        {
            errors.Add("JWT secret key must be at least 32 characters long");
        }
        
        if (string.IsNullOrEmpty(options.Issuer))
        {
            errors.Add("JWT issuer is required");
        }
        
        if (string.IsNullOrEmpty(options.Audience))
        {
            errors.Add("JWT audience is required");
        }
        
        if (options.ExpirationMinutes > 1440) // 24 hours
        {
            errors.Add("JWT expiration should not exceed 24 hours");
        }
        
        return errors.Any() ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Security settings validator
/// </summary>
public class SecuritySettingsValidator : IValidateOptions<SecuritySettings>
{
    public ValidateOptionsResult Validate(string? name, SecuritySettings options)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(options.EncryptionKey))
        {
            errors.Add("Encryption key is required");
        }
        else if (options.EncryptionKey.Length < 32)
        {
            errors.Add("Encryption key must be at least 32 characters long");
        }
        
        if (string.IsNullOrEmpty(options.HmacSecret))
        {
            errors.Add("HMAC secret is required");
        }
        
        if (options.MaxLoginAttempts < 3)
        {
            errors.Add("Max login attempts should be at least 3");
        }
        
        if (options.PasswordMinLength < 8)
        {
            errors.Add("Password minimum length should be at least 8 characters");
        }
        
        return errors.Any() ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Configuration security helper
/// </summary>
public static class ConfigurationSecurityHelper
{
    public static string MaskSensitiveValue(string value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= visibleChars)
        {
            return "***";
        }
        
        var masked = new string('*', value.Length - visibleChars);
        return masked + value.Substring(value.Length - visibleChars);
    }
    
    public static bool IsSensitiveKey(string key)
    {
        var sensitiveKeys = new[]
        {
            "password", "secret", "key", "token", "connectionstring", "connection_string"
        };
        
        return sensitiveKeys.Any(sensitive => 
            key.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }
}
