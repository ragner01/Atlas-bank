using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Serilog;
using Serilog.Context;

namespace AtlasBank.BuildingBlocks.Audit;

/// <summary>
/// Comprehensive audit logging middleware
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly AuditConfiguration _config;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger, AuditConfiguration config)
    {
        _next = next;
        _logger = logger;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            // Log request
            await LogRequest(context, correlationId);

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
                stopwatch.Stop();

                // Log response
                await LogResponse(context, correlationId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogException(context, correlationId, ex, stopwatch.ElapsedMilliseconds);
                throw;
            }
            finally
            {
                // Restore response body
                await responseBody.CopyToAsync(originalBodyStream);
                context.Response.Body = originalBodyStream;
            }
        }
    }

    private bool ShouldAudit(HttpContext context)
    {
        // Skip health checks and metrics endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.Contains("/health") || path.Contains("/metrics") || path.Contains("/swagger"))
        {
            return false;
        }

        // Check if endpoint is in audit configuration
        return _config.AuditEndpoints.Any(endpoint => 
            path.StartsWith(endpoint.Path, StringComparison.OrdinalIgnoreCase) &&
            endpoint.Methods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase));
    }

    private async Task LogRequest(HttpContext context, string correlationId)
    {
        var requestInfo = new
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value,
            Headers = GetSafeHeaders(context.Request.Headers),
            ClientIp = GetClientIp(context),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            ContentType = context.Request.ContentType,
            ContentLength = context.Request.ContentLength,
            Body = await GetRequestBody(context)
        };

        _logger.LogInformation("AUDIT_REQUEST: {RequestInfo}", JsonSerializer.Serialize(requestInfo));
    }

    private async Task LogResponse(HttpContext context, string correlationId, long durationMs)
    {
        var responseInfo = new
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            StatusCode = context.Response.StatusCode,
            Headers = GetSafeHeaders(context.Response.Headers),
            ContentType = context.Response.ContentType,
            ContentLength = context.Response.ContentLength,
            DurationMs = durationMs,
            Body = await GetResponseBody(context)
        };

        _logger.LogInformation("AUDIT_RESPONSE: {ResponseInfo}", JsonSerializer.Serialize(responseInfo));
    }

    private async Task LogException(HttpContext context, string correlationId, Exception ex, long durationMs)
    {
        var exceptionInfo = new
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            StatusCode = context.Response.StatusCode,
            DurationMs = durationMs,
            ExceptionType = ex.GetType().Name,
            ExceptionMessage = ex.Message,
            StackTrace = ex.StackTrace
        };

        _logger.LogError(ex, "AUDIT_EXCEPTION: {ExceptionInfo}", JsonSerializer.Serialize(exceptionInfo));
    }

    private async Task<string> GetRequestBody(HttpContext context)
    {
        if (context.Request.ContentLength == 0 || !_config.LogRequestBody)
            return "";

        try
        {
            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;
            
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            return MaskSensitiveData(body);
        }
        catch
        {
            return "[Error reading request body]";
        }
    }

    private async Task<string> GetResponseBody(HttpContext context)
    {
        if (!_config.LogResponseBody)
            return "";

        try
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            return MaskSensitiveData(body);
        }
        catch
        {
            return "[Error reading response body]";
        }
    }

    private Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        
        foreach (var header in headers)
        {
            if (_config.SensitiveHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                safeHeaders[header.Key] = "[REDACTED]";
            }
            else
            {
                safeHeaders[header.Key] = header.Value.ToString();
            }
        }

        return safeHeaders;
    }

    private string GetClientIp(HttpContext context)
    {
        // Check for forwarded headers first
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string MaskSensitiveData(string data)
    {
        if (string.IsNullOrEmpty(data))
            return data;

        try
        {
            // Mask common sensitive fields
            var patterns = new Dictionary<string, string>
            {
                { @"""pin""\s*:\s*""[^""]+""", @"""pin"":""[REDACTED]""" },
                { @"""password""\s*:\s*""[^""]+""", @"""password"":""[REDACTED]""" },
                { @"""token""\s*:\s*""[^""]+""", @"""token"":""[REDACTED]""" },
                { @"""secret""\s*:\s*""[^""]+""", @"""secret"":""[REDACTED]""" },
                { @"""key""\s*:\s*""[^""]+""", @"""key"":""[REDACTED]""" },
                { @"""msisdn""\s*:\s*""[^""]+""", @"""msisdn"":""[REDACTED]""" },
                { @"""accountId""\s*:\s*""[^""]+""", @"""accountId"":""[REDACTED]""" }
            };

            foreach (var pattern in patterns)
            {
                data = System.Text.RegularExpressions.Regex.Replace(data, pattern.Key, pattern.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return data;
        }
        catch
        {
            return "[Error masking sensitive data]";
        }
    }
}

/// <summary>
/// Audit configuration
/// </summary>
public class AuditConfiguration
{
    public bool LogRequestBody { get; set; } = true;
    public bool LogResponseBody { get; set; } = true;
    public List<string> SensitiveHeaders { get; set; } = new()
    {
        "Authorization",
        "X-API-Key",
        "X-Auth-Token",
        "Cookie"
    };
    public List<AuditEndpoint> AuditEndpoints { get; set; } = new();
}

/// <summary>
/// Audit endpoint configuration
/// </summary>
public class AuditEndpoint
{
    public string Path { get; set; } = "";
    public List<string> Methods { get; set; } = new();
    public bool LogRequestBody { get; set; } = true;
    public bool LogResponseBody { get; set; } = true;
}

/// <summary>
/// Business event audit logger
/// </summary>
public class BusinessEventLogger
{
    private readonly ILogger<BusinessEventLogger> _logger;

    public BusinessEventLogger(ILogger<BusinessEventLogger> logger)
    {
        _logger = logger;
    }

    public void LogTransactionEvent(string eventType, string accountId, decimal amount, string currency, string status, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            AccountId = accountId,
            Amount = amount,
            Currency = currency,
            Status = status,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("BUSINESS_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogUssdEvent(string eventType, string msisdn, string sessionId, string step, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Msisdn = msisdn,
            SessionId = sessionId,
            Step = step,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("USSD_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogAgentEvent(string eventType, string agentCode, string msisdn, decimal amount, string currency, string status, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            AgentCode = agentCode,
            Msisdn = msisdn,
            Amount = amount,
            Currency = currency,
            Status = status,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("AGENT_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogOfflineEvent(string eventType, string deviceId, string operationType, string status, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            DeviceId = deviceId,
            OperationType = operationType,
            Status = status,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("OFFLINE_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogSecurityEvent(string eventType, string severity, string description, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Severity = severity,
            Description = description,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        var logLevel = severity.ToLowerInvariant() switch
        {
            "critical" => LogLevel.Critical,
            "high" => LogLevel.Error,
            "medium" => LogLevel.Warning,
            "low" => LogLevel.Information,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "SECURITY_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }
}

/// <summary>
/// Compliance audit logger
/// </summary>
public class ComplianceAuditLogger
{
    private readonly ILogger<ComplianceAuditLogger> _logger;

    public ComplianceAuditLogger(ILogger<ComplianceAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogKycEvent(string eventType, string msisdn, string status, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Msisdn = msisdn,
            Status = status,
            ComplianceType = "KYC",
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("COMPLIANCE_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogAmlEvent(string eventType, string accountId, string riskLevel, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            AccountId = accountId,
            RiskLevel = riskLevel,
            ComplianceType = "AML",
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("COMPLIANCE_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }

    public void LogSanctionsEvent(string eventType, string entityId, string matchType, Dictionary<string, object>? metadata = null)
    {
        var eventData = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            EntityId = entityId,
            MatchType = matchType,
            ComplianceType = "SANCTIONS",
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        _logger.LogInformation("COMPLIANCE_EVENT: {EventData}", JsonSerializer.Serialize(eventData));
    }
}
