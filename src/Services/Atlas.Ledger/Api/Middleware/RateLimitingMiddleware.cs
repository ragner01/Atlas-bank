using System.Net;
using System.Text.Json;
using StackExchange.Redis;
using Atlas.Ledger.Api.Models;

namespace Atlas.Ledger.Api.Middleware;

/// <summary>
/// Rate limiting middleware with Redis-backed distributed rate limiting
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly RateLimitingOptions _options;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, 
        IConnectionMultiplexer redis, RateLimitingOptions options)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointIdentifier(context);
        var correlationId = context.TraceIdentifier;

        // Check global rate limit
        if (!await CheckRateLimitAsync($"global:{clientId}", _options.GlobalRequestsPerMinute, 60))
        {
            _logger.LogWarning("Global rate limit exceeded for client {ClientId} with correlation ID {CorrelationId}", 
                clientId, correlationId);
            await ReturnRateLimitResponse(context, "Global rate limit exceeded", correlationId);
            return;
        }

        // Check endpoint-specific rate limit
        if (!await CheckRateLimitAsync($"endpoint:{clientId}:{endpoint}", _options.EndpointRequestsPerMinute, 60))
        {
            _logger.LogWarning("Endpoint rate limit exceeded for client {ClientId} on endpoint {Endpoint} with correlation ID {CorrelationId}", 
                clientId, endpoint, correlationId);
            await ReturnRateLimitResponse(context, $"Rate limit exceeded for endpoint {endpoint}", correlationId);
            return;
        }

        // Check burst protection (short-term limit)
        if (!await CheckRateLimitAsync($"burst:{clientId}", _options.BurstRequestsPerSecond, 1))
        {
            _logger.LogWarning("Burst rate limit exceeded for client {ClientId} with correlation ID {CorrelationId}", 
                clientId, correlationId);
            await ReturnRateLimitResponse(context, "Burst rate limit exceeded", correlationId);
            return;
        }

        await _next(context);
    }

    private async Task<bool> CheckRateLimitAsync(string key, int limit, int windowSeconds)
    {
        try
        {
            var db = _redis.GetDatabase();
            var currentTime = DateTimeOffset.UtcNow;
            var windowStart = currentTime.AddSeconds(-windowSeconds);

            // Use Redis sorted set for sliding window rate limiting
            var script = @"
                local key = KEYS[1]
                local limit = tonumber(ARGV[1])
                local window = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])
                
                -- Remove expired entries
                redis.call('ZREMRANGEBYSCORE', key, 0, now - window)
                
                -- Count current entries
                local current = redis.call('ZCARD', key)
                
                if current < limit then
                    -- Add current request
                    redis.call('ZADD', key, now, now .. ':' .. math.random())
                    redis.call('EXPIRE', key, window)
                    return 1
                else
                    return 0
                end";

            var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { key }, 
                new RedisValue[] { limit, windowSeconds, currentTime.ToUnixTimeSeconds() });

            return result.ToString() == "1";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for key {Key}", key);
            // Fail open - allow request if Redis is unavailable
            return true;
        }
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get client IP from various headers (for load balancer scenarios)
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                      context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
                      context.Connection.RemoteIpAddress?.ToString() ??
                      "unknown";

        // Extract first IP if comma-separated (X-Forwarded-For can contain multiple IPs)
        if (clientIp.Contains(','))
        {
            clientIp = clientIp.Split(',')[0].Trim();
        }

        // For authenticated users, use user ID instead of IP
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{clientIp}";
    }

    private string GetEndpointIdentifier(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        
        // Normalize path for rate limiting (remove dynamic segments)
        var normalizedPath = NormalizePath(path);
        
        return $"{method}:{normalizedPath}";
    }

    private string NormalizePath(string path)
    {
        // Replace dynamic segments with placeholders
        var normalized = path
            .Replace("/ledger/accounts/", "/ledger/accounts/{id}")
            .Replace("/ledger/entries/", "/ledger/entries/{id}")
            .Replace("/payments/transfers/", "/payments/transfers/{id}");

        return normalized;
    }

    private async Task ReturnRateLimitResponse(HttpContext context, string message, string correlationId)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;

        var response = ErrorResponse.Create(
            "RATE_LIMIT_EXCEEDED",
            message,
            null,
            correlationId,
            new Dictionary<string, object>
            {
                ["retryAfter"] = 60, // Retry after 60 seconds
                ["limitType"] = "per-minute"
            });

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _options.GlobalRequestsPerMinute.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = "0";
        context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();
        context.Response.Headers["Retry-After"] = "60";

        await context.Response.WriteAsync(jsonResponse);
    }
}

/// <summary>
/// Rate limiting configuration options
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Global requests per minute per client
    /// </summary>
    public int GlobalRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Endpoint-specific requests per minute per client
    /// </summary>
    public int EndpointRequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Burst requests per second per client
    /// </summary>
    public int BurstRequestsPerSecond { get; set; } = 10;

    /// <summary>
    /// Whether to enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;
}
