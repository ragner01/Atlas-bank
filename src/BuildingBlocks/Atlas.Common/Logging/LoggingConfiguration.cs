using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace AtlasBank.Common.Logging;

/// <summary>
/// Centralized logging configuration for AtlasBank services
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configure Serilog with structured logging, security, and performance optimizations
    /// </summary>
    public static void ConfigureLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var configuration = builder.Configuration;
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Version", GetVersion())
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information
            )
            .WriteTo.File(
                new JsonFormatter(),
                path: $"logs/{serviceName.ToLower()}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100_000_000, // 100MB
                rollOnFileSizeLimit: true,
                restrictedToMinimumLevel: LogEventLevel.Information
            )
            .WriteTo.File(
                path: $"logs/{serviceName.ToLower()}-errors-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90,
                fileSizeLimitBytes: 50_000_000, // 50MB
                rollOnFileSizeLimit: true,
                restrictedToMinimumLevel: LogEventLevel.Error
            )
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    /// <summary>
    /// Add request/response logging middleware with security considerations
    /// </summary>
    public static void UseRequestLogging(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                              Guid.NewGuid().ToString();
            
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
            using (LogContext.PushProperty("UserAgent", context.Request.Headers.UserAgent.ToString()))
            using (LogContext.PushProperty("RemoteIP", GetClientIP(context)))
            {
                // Log request
                Log.Information("Incoming request: {Method} {Path} {QueryString} from {RemoteIP}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString,
                    GetClientIP(context));

                // Add correlation ID to response headers
                context.Response.Headers.Add("X-Correlation-ID", correlationId);

                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unhandled exception in request pipeline: {Method} {Path}",
                        context.Request.Method, context.Request.Path);
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    
                    // Log response (mask sensitive data)
                    Log.Information("Outgoing response: {StatusCode} in {ElapsedMs}ms for {Method} {Path}",
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        context.Request.Method,
                        context.Request.Path);
                }
            }
        });
    }

    /// <summary>
    /// Add global exception handling middleware
    /// </summary>
    public static void UseGlobalExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
                
                if (exception != null)
                {
                    Log.Error(exception, "Unhandled exception occurred");
                    
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    
                    var response = new
                    {
                        error = "An internal error occurred",
                        correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault(),
                        timestamp = DateTime.UtcNow
                    };
                    
                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                }
            });
        });
    }

    /// <summary>
    /// Add security headers middleware
    /// </summary>
    public static void UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Security headers
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Frame-Options", "DENY");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            
            // Remove server header
            context.Response.Headers.Remove("Server");
            
            await next();
        });
    }

    /// <summary>
    /// Mask sensitive data in logs
    /// </summary>
    public static string MaskSensitiveData(string data)
    {
        if (string.IsNullOrEmpty(data)) return data;
        
        // Mask credit card numbers (PAN)
        data = System.Text.RegularExpressions.Regex.Replace(data, 
            @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", 
            "****-****-****-****");
        
        // Mask email addresses
        data = System.Text.RegularExpressions.Regex.Replace(data, 
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", 
            "***@***.***");
        
        // Mask phone numbers
        data = System.Text.RegularExpressions.Regex.Replace(data, 
            @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b", 
            "***-***-****");
        
        return data;
    }

    private static string GetClientIP(HttpContext context)
    {
        // Check for forwarded headers first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIP))
        {
            return realIP;
        }
        
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GetVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "1.0.0";
    }
}

/// <summary>
/// Extension methods for structured logging
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Log business events with structured data
    /// </summary>
    public static void LogBusinessEvent(this ILogger logger, string eventName, object data, LogEventLevel level = LogEventLevel.Information)
    {
        logger.Write(level, "Business Event: {EventName} {@EventData}", eventName, data);
    }

    /// <summary>
    /// Log performance metrics
    /// </summary>
    public static void LogPerformanceMetric(this ILogger logger, string operation, long elapsedMs, object? additionalData = null)
    {
        logger.Information("Performance: {Operation} completed in {ElapsedMs}ms {@AdditionalData}", 
            operation, elapsedMs, additionalData);
    }

    /// <summary>
    /// Log security events
    /// </summary>
    public static void LogSecurityEvent(this ILogger logger, string eventType, string details, LogEventLevel level = LogEventLevel.Warning)
    {
        logger.Write(level, "Security Event: {EventType} - {Details}", eventType, details);
    }
}

