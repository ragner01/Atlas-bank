using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole;
using Serilog.Sinks.File;
using System.Diagnostics;
using System.Text.Json;

namespace AtlasBank.Infrastructure.Logging;

/// <summary>
/// Comprehensive error logging configuration
/// </summary>
public static class LoggingConfiguration
{
    public static void ConfigureLogging(IServiceCollection services, IConfiguration configuration)
    {
        var logLevel = configuration.GetValue<string>("Logging:LogLevel:Default") ?? "Information";
        var logPath = configuration.GetValue<string>("Logging:FilePath") ?? "logs/atlasbank-.log";
        var enableConsole = configuration.GetValue<bool>("Logging:EnableConsole", true);
        var enableFile = configuration.GetValue<bool>("Logging:EnableFile", true);
        var enableStructuredLogging = configuration.GetValue<bool>("Logging:EnableStructuredLogging", true);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AtlasBank")
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");

        if (enableConsole)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        if (enableFile)
        {
            loggerConfig.WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        if (enableStructuredLogging)
        {
            loggerConfig.WriteTo.File(
                path: "logs/atlasbank-structured-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                formatter: new Serilog.Formatting.Json.JsonFormatter());
        }

        // Configure different log levels for different environments
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            loggerConfig.MinimumLevel.Debug();
        }

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        // Register custom logging services
        services.AddScoped<IErrorLogger, ErrorLogger>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IPerformanceLogger, PerformanceLogger>();
    }
}

/// <summary>
/// Custom error logger for critical errors
/// </summary>
public interface IErrorLogger
{
    Task LogCriticalErrorAsync(string message, Exception exception, Dictionary<string, object>? context = null);
    Task LogSecurityEventAsync(string eventType, string message, Dictionary<string, object>? context = null);
    Task LogBusinessErrorAsync(string operation, string message, Dictionary<string, object>? context = null);
}

public class ErrorLogger : IErrorLogger
{
    private readonly ILogger<ErrorLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ErrorLogger(ILogger<ErrorLogger> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogCriticalErrorAsync(string message, Exception exception, Dictionary<string, object>? context = null)
    {
        var logContext = BuildLogContext(context);
        
        _logger.LogError(exception, "CRITICAL ERROR: {Message} {Context}", message, JsonSerializer.Serialize(logContext));
        
        // In production, send to external monitoring service
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
        {
            await SendToExternalMonitoring("CRITICAL_ERROR", message, exception, logContext);
        }
    }

    public async Task LogSecurityEventAsync(string eventType, string message, Dictionary<string, object>? context = null)
    {
        var logContext = BuildLogContext(context);
        logContext["EventType"] = eventType;
        logContext["SecurityEvent"] = true;
        
        _logger.LogWarning("SECURITY EVENT: {EventType} - {Message} {Context}", 
            eventType, message, JsonSerializer.Serialize(logContext));
        
        // Security events should always be sent to external monitoring
        await SendToExternalMonitoring("SECURITY_EVENT", message, null, logContext);
    }

    public async Task LogBusinessErrorAsync(string operation, string message, Dictionary<string, object>? context = null)
    {
        var logContext = BuildLogContext(context);
        logContext["Operation"] = operation;
        logContext["BusinessError"] = true;
        
        _logger.LogError("BUSINESS ERROR: {Operation} - {Message} {Context}", 
            operation, message, JsonSerializer.Serialize(logContext));
        
        // Business errors may need immediate attention
        if (IsHighPriorityBusinessError(operation))
        {
            await SendToExternalMonitoring("BUSINESS_ERROR", message, null, logContext);
        }
    }

    private Dictionary<string, object> BuildLogContext(Dictionary<string, object>? additionalContext)
    {
        var context = new Dictionary<string, object>
        {
            ["Timestamp"] = DateTime.UtcNow,
            ["MachineName"] = Environment.MachineName,
            ["ProcessId"] = Environment.ProcessId,
            ["ThreadId"] = Environment.CurrentManagedThreadId
        };

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            context["RequestId"] = httpContext.TraceIdentifier;
            context["UserAgent"] = httpContext.Request.Headers.UserAgent.ToString();
            context["RemoteIpAddress"] = httpContext.Connection.RemoteIpAddress?.ToString();
            context["RequestPath"] = httpContext.Request.Path;
            context["RequestMethod"] = httpContext.Request.Method;
            
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                context["UserId"] = httpContext.User.Identity.Name;
            }
        }

        if (additionalContext != null)
        {
            foreach (var kvp in additionalContext)
            {
                context[kvp.Key] = kvp.Value;
            }
        }

        return context;
    }

    private async Task SendToExternalMonitoring(string eventType, string message, Exception? exception, Dictionary<string, object> context)
    {
        try
        {
            // TODO: Implement external monitoring service integration
            // Examples: Sentry, DataDog, New Relic, Application Insights
            
            var payload = new
            {
                EventType = eventType,
                Message = message,
                Exception = exception?.ToString(),
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            // For now, just log to console in production
            Console.WriteLine($"EXTERNAL MONITORING: {JsonSerializer.Serialize(payload)}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error to external monitoring service");
        }
    }

    private bool IsHighPriorityBusinessError(string operation)
    {
        var highPriorityOperations = new[]
        {
            "TRANSFER", "PAYMENT", "WITHDRAWAL", "DEPOSIT", "AUTHENTICATION", "AUTHORIZATION"
        };
        
        return highPriorityOperations.Any(op => operation.Contains(op, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Audit logger for compliance and security
/// </summary>
public interface IAuditLogger
{
    Task LogUserActionAsync(string userId, string action, string resource, Dictionary<string, object>? details = null);
    Task LogDataAccessAsync(string userId, string operation, string table, Dictionary<string, object>? details = null);
    Task LogSystemEventAsync(string eventType, string description, Dictionary<string, object>? details = null);
}

public class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogUserActionAsync(string userId, string action, string resource, Dictionary<string, object>? details = null)
    {
        var auditData = new
        {
            UserId = userId,
            Action = action,
            Resource = resource,
            Details = details,
            Timestamp = DateTime.UtcNow,
            Type = "USER_ACTION"
        };

        _logger.LogInformation("AUDIT: {AuditData}", JsonSerializer.Serialize(auditData));
        await Task.CompletedTask;
    }

    public async Task LogDataAccessAsync(string userId, string operation, string table, Dictionary<string, object>? details = null)
    {
        var auditData = new
        {
            UserId = userId,
            Operation = operation,
            Table = table,
            Details = details,
            Timestamp = DateTime.UtcNow,
            Type = "DATA_ACCESS"
        };

        _logger.LogInformation("AUDIT: {AuditData}", JsonSerializer.Serialize(auditData));
        await Task.CompletedTask;
    }

    public async Task LogSystemEventAsync(string eventType, string description, Dictionary<string, object>? details = null)
    {
        var auditData = new
        {
            EventType = eventType,
            Description = description,
            Details = details,
            Timestamp = DateTime.UtcNow,
            Type = "SYSTEM_EVENT"
        };

        _logger.LogInformation("AUDIT: {AuditData}", JsonSerializer.Serialize(auditData));
        await Task.CompletedTask;
    }
}

/// <summary>
/// Performance logger for monitoring
/// </summary>
public interface IPerformanceLogger
{
    Task LogPerformanceAsync(string operation, TimeSpan duration, Dictionary<string, object>? metrics = null);
    Task LogDatabaseQueryAsync(string query, TimeSpan duration, int rowsAffected);
    Task LogApiCallAsync(string endpoint, TimeSpan duration, int statusCode);
}

public class PerformanceLogger : IPerformanceLogger
{
    private readonly ILogger<PerformanceLogger> _logger;

    public PerformanceLogger(ILogger<PerformanceLogger> logger)
    {
        _logger = logger;
    }

    public async Task LogPerformanceAsync(string operation, TimeSpan duration, Dictionary<string, object>? metrics = null)
    {
        var performanceData = new
        {
            Operation = operation,
            Duration = duration.TotalMilliseconds,
            Metrics = metrics,
            Timestamp = DateTime.UtcNow,
            Type = "PERFORMANCE"
        };

        if (duration.TotalMilliseconds > 1000) // Log slow operations
        {
            _logger.LogWarning("SLOW OPERATION: {PerformanceData}", JsonSerializer.Serialize(performanceData));
        }
        else
        {
            _logger.LogInformation("PERFORMANCE: {PerformanceData}", JsonSerializer.Serialize(performanceData));
        }

        await Task.CompletedTask;
    }

    public async Task LogDatabaseQueryAsync(string query, TimeSpan duration, int rowsAffected)
    {
        var queryData = new
        {
            Query = query.Length > 200 ? query.Substring(0, 200) + "..." : query,
            Duration = duration.TotalMilliseconds,
            RowsAffected = rowsAffected,
            Timestamp = DateTime.UtcNow,
            Type = "DATABASE_QUERY"
        };

        if (duration.TotalMilliseconds > 500) // Log slow queries
        {
            _logger.LogWarning("SLOW QUERY: {QueryData}", JsonSerializer.Serialize(queryData));
        }

        await Task.CompletedTask;
    }

    public async Task LogApiCallAsync(string endpoint, TimeSpan duration, int statusCode)
    {
        var apiData = new
        {
            Endpoint = endpoint,
            Duration = duration.TotalMilliseconds,
            StatusCode = statusCode,
            Timestamp = DateTime.UtcNow,
            Type = "API_CALL"
        };

        if (duration.TotalMilliseconds > 2000) // Log slow API calls
        {
            _logger.LogWarning("SLOW API CALL: {ApiData}", JsonSerializer.Serialize(apiData));
        }

        await Task.CompletedTask;
    }
}
