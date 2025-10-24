using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace AtlasBank.Common.Configuration;

/// <summary>
/// Base configuration class with validation
/// </summary>
public abstract class BaseConfiguration
{
    /// <summary>
    /// Validate configuration on startup
    /// </summary>
    public virtual void Validate()
    {
        var validationContext = new ValidationContext(this);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(this, validationContext, validationResults, true))
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Configuration validation failed: {errors}");
        }
    }
}

/// <summary>
/// Database configuration with validation
/// </summary>
public class DatabaseConfiguration : BaseConfiguration
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 10;
    
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
    
    public bool EnableRetryOnFailure { get; set; } = true;
    
    [Range(1, 10)]
    public int MaxRetryCount { get; set; } = 3;
}

/// <summary>
/// Redis configuration with validation
/// </summary>
public class RedisConfiguration : BaseConfiguration
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int Database { get; set; } = 0;
    
    [Range(1, 300)]
    public int ConnectTimeoutMs { get; set; } = 5000;
    
    [Range(1, 300)]
    public int SyncTimeoutMs { get; set; } = 5000;
    
    public bool AbortOnConnectFail { get; set; } = false;
}

/// <summary>
/// Kafka configuration with validation
/// </summary>
public class KafkaConfiguration : BaseConfiguration
{
    [Required]
    public string BootstrapServers { get; set; } = string.Empty;
    
    [Required]
    public string TopicPrefix { get; set; } = string.Empty;
    
    [Range(1, 300)]
    public int RequestTimeoutMs { get; set; } = 30000;
    
    [Range(1, 100)]
    public int RetryCount { get; set; } = 3;
    
    public bool EnableIdempotence { get; set; } = true;
    
    public string Acks { get; set; } = "all";
}

/// <summary>
/// Security configuration with validation
/// </summary>
public class SecurityConfiguration : BaseConfiguration
{
    [Required]
    public string JwtSecret { get; set; } = string.Empty;
    
    [Range(1, 24)]
    public int JwtExpirationHours { get; set; } = 1;
    
    [Required]
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    
    public bool RequireHttps { get; set; } = true;
    
    [Range(1, 300)]
    public int RateLimitRequestsPerMinute { get; set; } = 100;
}

/// <summary>
/// Service configuration with validation
/// </summary>
public class ServiceConfiguration : BaseConfiguration
{
    [Required]
    public string ServiceName { get; set; } = string.Empty;
    
    [Range(1, 65535)]
    public int Port { get; set; } = 5000;
    
    [Required]
    public string Environment { get; set; } = "Production";
    
    public bool EnableSwagger { get; set; } = false;
    
    public bool EnableMetrics { get; set; } = true;
    
    public bool EnableTracing { get; set; } = true;
}

/// <summary>
/// Configuration validation service
/// </summary>
public class ConfigurationValidator<T> : IValidateOptions<T> where T : BaseConfiguration
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail($"Configuration validation failed for {typeof(T).Name}: {ex.Message}");
        }
    }
}

/// <summary>
/// Configuration extensions for dependency injection
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Add validated configuration to DI container
    /// </summary>
    public static IServiceCollection AddValidatedConfiguration<T>(
        this IServiceCollection services, 
        IConfiguration configuration, 
        string sectionName) where T : BaseConfiguration, new()
    {
        services.Configure<T>(configuration.GetSection(sectionName));
        services.AddSingleton<IValidateOptions<T>, ConfigurationValidator<T>>();
        
        // Validate on startup
        services.AddSingleton<IHostedService, ConfigurationValidationService<T>>();
        
        return services;
    }

    /// <summary>
    /// Get configuration with validation
    /// </summary>
    public static T GetValidatedConfiguration<T>(this IServiceProvider serviceProvider) where T : BaseConfiguration
    {
        var options = serviceProvider.GetRequiredService<IOptions<T>>();
        return options.Value;
    }
}

/// <summary>
/// Service to validate configuration on startup
/// </summary>
public class ConfigurationValidationService<T> : IHostedService where T : BaseConfiguration
{
    private readonly IOptions<T> _options;
    private readonly ILogger<ConfigurationValidationService<T>> _logger;

    public ConfigurationValidationService(IOptions<T> options, ILogger<ConfigurationValidationService<T>> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _options.Value.Validate();
            _logger.LogInformation("Configuration validation passed for {ConfigurationType}", typeof(T).Name);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed for {ConfigurationType}", typeof(T).Name);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Environment variable helper
/// </summary>
public static class EnvironmentHelper
{
    /// <summary>
    /// Get environment variable with fallback
    /// </summary>
    public static string GetEnvironmentVariable(string name, string defaultValue = "")
    {
        return Environment.GetEnvironmentVariable(name) ?? defaultValue;
    }

    /// <summary>
    /// Get required environment variable
    /// </summary>
    public static string GetRequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{name}' is not set");
        }
        return value;
    }

    /// <summary>
    /// Get boolean environment variable
    /// </summary>
    public static bool GetBooleanEnvironmentVariable(string name, bool defaultValue = false)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Get integer environment variable
    /// </summary>
    public static int GetIntegerEnvironmentVariable(string name, int defaultValue = 0)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}

