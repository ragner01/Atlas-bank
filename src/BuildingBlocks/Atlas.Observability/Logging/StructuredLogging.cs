using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AtlasBank.BuildingBlocks.Logging;

/// <summary>
/// Comprehensive structured logging configuration for AtlasBank services
/// Includes correlation IDs, sensitive data masking, and performance metrics
/// </summary>
public static class LoggingConfiguration
{
    public static void ConfigureLogging(this WebApplicationBuilder builder, string serviceName)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Version", GetVersion())
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithCorrelationId()
            .Enrich.WithRequestId()
            .Enrich.WithPerformanceMetrics()
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.File(new JsonFormatter(), 
                path: $"logs/{serviceName.ToLowerInvariant()}-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 100 * 1024 * 1024) // 100MB
            .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341")
            .CreateLogger();

        builder.Host.UseSerilog();
    }

    private static string GetVersion()
    {
        return typeof(LoggingConfiguration).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}

/// <summary>
/// Serilog enricher for correlation IDs
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}

/// <summary>
/// Serilog enricher for request IDs
/// </summary>
public class RequestIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var requestId = Activity.Current?.GetBaggageItem("RequestId") ?? Guid.NewGuid().ToString();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", requestId));
    }
}

/// <summary>
/// Serilog enricher for performance metrics
/// </summary>
public class PerformanceMetricsEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var process = Process.GetCurrentProcess();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsage", process.WorkingSet64));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CpuTime", process.TotalProcessorTime.TotalMilliseconds));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadCount", process.Threads.Count));
    }
}

/// <summary>
/// Middleware for request/response logging with sensitive data masking
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly SensitiveDataMasker _masker;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _masker = new SensitiveDataMasker();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            // Log request
            await LogRequestAsync(context);

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                
                // Log response
                await LogResponseAsync(context, stopwatch.ElapsedMilliseconds);

                // Copy response back to original stream
                await responseBody.CopyToAsync(originalBodyStream);
                context.Response.Body = originalBodyStream;
            }
        }
    }

    private async Task LogRequestAsync(HttpContext context)
    {
        var request = context.Request;
        var requestBody = await ReadRequestBodyAsync(request);

        _logger.LogInformation("Incoming request: {Method} {Path} {QueryString} {Headers} {Body}",
            request.Method,
            request.Path,
            request.QueryString,
            _masker.MaskHeaders(request.Headers),
            _masker.MaskSensitiveData(requestBody));
    }

    private async Task LogResponseAsync(HttpContext context, long elapsedMs)
    {
        var response = context.Response;
        var responseBody = await ReadResponseBodyAsync(response);

        _logger.LogInformation("Outgoing response: {StatusCode} {Headers} {Body} {ElapsedMs}ms",
            response.StatusCode,
            _masker.MaskHeaders(response.Headers),
            _masker.MaskSensitiveData(responseBody),
            elapsedMs);
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }
}

/// <summary>
/// Sensitive data masker for logs
/// </summary>
public class SensitiveDataMasker
{
    private readonly Dictionary<string, string> _patterns = new()
    {
        { @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", "****-****-****-****" }, // Credit card numbers
        { @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "***@***.***" }, // Email addresses
        { @"\b(?:\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})\b", "***-***-****" }, // Phone numbers
        { @"""password""\s*:\s*""[^""]+""", @"""password"":""***""" }, // Password fields
        { @"""pin""\s*:\s*""[^""]+""", @"""pin"":""***""" }, // PIN fields
        { @"""token""\s*:\s*""[^""]+""", @"""token"":""***""" }, // Token fields
        { @"""secret""\s*:\s*""[^""]+""", @"""secret"":""***""" }, // Secret fields
    };

    public string MaskSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var masked = input;
        foreach (var pattern in _patterns)
        {
            masked = Regex.Replace(masked, pattern.Key, pattern.Value, RegexOptions.IgnoreCase);
        }
        return masked;
    }

    public Dictionary<string, string> MaskHeaders(IHeaderDictionary headers)
    {
        var maskedHeaders = new Dictionary<string, string>();
        var sensitiveHeaders = new[] { "authorization", "x-api-key", "x-auth-token", "cookie" };

        foreach (var header in headers)
        {
            if (sensitiveHeaders.Contains(header.Key.ToLowerInvariant()))
            {
                maskedHeaders[header.Key] = "***";
            }
            else
            {
                maskedHeaders[header.Key] = string.Join(", ", header.Value);
            }
        }

        return maskedHeaders;
    }
}

/// <summary>
/// Extension methods for Serilog configuration
/// </summary>
public static class SerilogExtensions
{
    public static LoggerConfiguration EnrichWithCorrelationId(this LoggerConfiguration config)
    {
        return config.Enrich.With<CorrelationIdEnricher>();
    }

    public static LoggerConfiguration EnrichWithRequestId(this LoggerConfiguration config)
    {
        return config.Enrich.With<RequestIdEnricher>();
    }

    public static LoggerConfiguration EnrichWithPerformanceMetrics(this LoggerConfiguration config)
    {
        return config.Enrich.With<PerformanceMetricsEnricher>();
    }
}
