using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace AtlasBank.Infrastructure.Performance;

/// <summary>
/// Performance monitoring and optimization
/// </summary>
public static class PerformanceConfiguration
{
    public static void ConfigurePerformance(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure memory cache with performance settings
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10000;
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
        });

        // Add performance monitoring services
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        services.AddSingleton<ICacheManager, CacheManager>();
        services.AddSingleton<IQueryOptimizer, QueryOptimizer>();
        services.AddScoped<IPerformanceMiddleware, PerformanceMiddleware>();

        // Configure response compression
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        // Configure response caching
        services.AddResponseCaching();
    }
}

/// <summary>
/// Performance monitoring service
/// </summary>
public interface IPerformanceMonitor
{
    Task<PerformanceMetrics> GetMetricsAsync();
    Task LogPerformanceAsync(string operation, TimeSpan duration, Dictionary<string, object>? metadata = null);
    Task<bool> IsPerformanceAcceptableAsync(string operation, TimeSpan duration);
    Task<List<PerformanceAlert>> GetPerformanceAlertsAsync();
}

public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly Dictionary<string, List<TimeSpan>> _operationTimes = new();
    private readonly List<PerformanceAlert> _alerts = new();
    private readonly object _lock = new object();

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
    }

    public async Task<PerformanceMetrics> GetMetricsAsync()
    {
        lock (_lock)
        {
            var metrics = new PerformanceMetrics
            {
                TotalOperations = _operationTimes.Values.Sum(times => times.Count),
                AverageResponseTime = CalculateAverageResponseTime(),
                SlowOperations = GetSlowOperations(),
                PerformanceAlerts = _alerts.Count,
                Timestamp = DateTime.UtcNow
            };

            return metrics;
        }
    }

    public async Task LogPerformanceAsync(string operation, TimeSpan duration, Dictionary<string, object>? metadata = null)
    {
        lock (_lock)
        {
            if (!_operationTimes.ContainsKey(operation))
            {
                _operationTimes[operation] = new List<TimeSpan>();
            }

            _operationTimes[operation].Add(duration);

            // Keep only last 1000 measurements per operation
            if (_operationTimes[operation].Count > 1000)
            {
                _operationTimes[operation].RemoveAt(0);
            }

            // Check for performance issues
            if (IsSlowOperation(operation, duration))
            {
                var alert = new PerformanceAlert
                {
                    Operation = operation,
                    Duration = duration,
                    Threshold = GetThresholdForOperation(operation),
                    Timestamp = DateTime.UtcNow,
                    Metadata = metadata
                };

                _alerts.Add(alert);
                _logger.LogWarning("Slow operation detected: {Operation} took {Duration}ms", 
                    operation, duration.TotalMilliseconds);
            }
        }
    }

    public async Task<bool> IsPerformanceAcceptableAsync(string operation, TimeSpan duration)
    {
        var threshold = GetThresholdForOperation(operation);
        return duration <= threshold;
    }

    public async Task<List<PerformanceAlert>> GetPerformanceAlertsAsync()
    {
        lock (_lock)
        {
            return _alerts.ToList();
        }
    }

    private TimeSpan CalculateAverageResponseTime()
    {
        var allTimes = _operationTimes.Values.SelectMany(times => times).ToList();
        if (!allTimes.Any())
            return TimeSpan.Zero;

        return TimeSpan.FromMilliseconds(allTimes.Average(t => t.TotalMilliseconds));
    }

    private List<SlowOperation> GetSlowOperations()
    {
        return _operationTimes
            .Where(kvp => kvp.Value.Any())
            .Select(kvp => new SlowOperation
            {
                Operation = kvp.Key,
                AverageDuration = TimeSpan.FromMilliseconds(kvp.Value.Average(t => t.TotalMilliseconds)),
                MaxDuration = kvp.Value.Max(),
                Count = kvp.Value.Count
            })
            .OrderByDescending(op => op.AverageDuration)
            .Take(10)
            .ToList();
    }

    private bool IsSlowOperation(string operation, TimeSpan duration)
    {
        var threshold = GetThresholdForOperation(operation);
        return duration > threshold;
    }

    private TimeSpan GetThresholdForOperation(string operation)
    {
        return operation.ToLower() switch
        {
            var op when op.Contains("database") => TimeSpan.FromMilliseconds(1000),
            var op when op.Contains("api") => TimeSpan.FromMilliseconds(2000),
            var op when op.Contains("cache") => TimeSpan.FromMilliseconds(100),
            var op when op.Contains("file") => TimeSpan.FromMilliseconds(500),
            _ => TimeSpan.FromMilliseconds(500)
        };
    }
}

/// <summary>
/// Cache management service
/// </summary>
public interface ICacheManager
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task ClearAsync();
    Task<CacheStatistics> GetStatisticsAsync();
}

public class CacheManager : ICacheManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheManager> _logger;
    private readonly Dictionary<string, CacheEntry> _cacheEntries = new();
    private readonly object _lock = new object();

    public CacheManager(IMemoryCache cache, ILogger<CacheManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            if (_cache.TryGetValue(key, out var value))
            {
                lock (_lock)
                {
                    if (_cacheEntries.ContainsKey(key))
                    {
                        _cacheEntries[key].AccessCount++;
                        _cacheEntries[key].LastAccessed = DateTime.UtcNow;
                    }
                }
                return (T?)value;
            }
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                cacheOptions.SlidingExpiration = TimeSpan.FromMinutes(30);
            }

            cacheOptions.RegisterPostEvictionCallback((k, v, reason, state) =>
            {
                lock (_lock)
                {
                    _cacheEntries.Remove(k.ToString() ?? string.Empty);
                }
            });

            _cache.Set(key, value, cacheOptions);

            lock (_lock)
            {
                _cacheEntries[key] = new CacheEntry
                {
                    Key = key,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    AccessCount = 1
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _cache.Remove(key);
            lock (_lock)
            {
                _cacheEntries.Remove(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            _cache.Dispose();
            lock (_lock)
            {
                _cacheEntries.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        lock (_lock)
        {
            return new CacheStatistics
            {
                TotalEntries = _cacheEntries.Count,
                TotalAccesses = _cacheEntries.Values.Sum(entry => entry.AccessCount),
                AverageAccessCount = _cacheEntries.Values.Average(entry => entry.AccessCount),
                MostAccessedKeys = _cacheEntries.Values
                    .OrderByDescending(entry => entry.AccessCount)
                    .Take(10)
                    .Select(entry => entry.Key)
                    .ToList(),
                Timestamp = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Query optimization service
/// </summary>
public interface IQueryOptimizer
{
    Task<string> OptimizeQueryAsync(string query);
    Task<List<QueryPerformanceMetric>> GetQueryMetricsAsync();
    Task<bool> IsQueryOptimizedAsync(string query);
}

public class QueryOptimizer : IQueryOptimizer
{
    private readonly ILogger<QueryOptimizer> _logger;
    private readonly Dictionary<string, QueryPerformanceMetric> _queryMetrics = new();
    private readonly object _lock = new object();

    public QueryOptimizer(ILogger<QueryOptimizer> logger)
    {
        _logger = logger;
    }

    public async Task<string> OptimizeQueryAsync(string query)
    {
        try
        {
            // Basic query optimization
            var optimizedQuery = query;

            // Remove unnecessary whitespace
            optimizedQuery = System.Text.RegularExpressions.Regex.Replace(optimizedQuery, @"\s+", " ");

            // Add query hints for better performance
            if (optimizedQuery.Contains("SELECT") && !optimizedQuery.Contains("LIMIT"))
            {
                optimizedQuery += " LIMIT 1000";
            }

            return optimizedQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing query");
            return query;
        }
    }

    public async Task<List<QueryPerformanceMetric>> GetQueryMetricsAsync()
    {
        lock (_lock)
        {
            return _queryMetrics.Values.ToList();
        }
    }

    public async Task<bool> IsQueryOptimizedAsync(string query)
    {
        // Basic optimization checks
        return !query.Contains("SELECT *") && 
               !query.Contains("ORDER BY") || query.Contains("LIMIT") &&
               !query.Contains("N+1");
    }
}

/// <summary>
/// Performance middleware
/// </summary>
public interface IPerformanceMiddleware
{
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}

public class PerformanceMiddleware : IPerformanceMiddleware
{
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<PerformanceMiddleware> _logger;

    public PerformanceMiddleware(IPerformanceMonitor performanceMonitor, ILogger<PerformanceMiddleware> logger)
    {
        _performanceMonitor = performanceMonitor;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var operation = $"{context.Request.Method} {context.Request.Path}";

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            await _performanceMonitor.LogPerformanceAsync(operation, duration, new Dictionary<string, object>
            {
                ["StatusCode"] = context.Response.StatusCode,
                ["ContentLength"] = context.Response.ContentLength ?? 0,
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString()
            });

            // Add performance headers
            context.Response.Headers.Add("X-Response-Time", duration.TotalMilliseconds.ToString("F2"));
        }
    }
}

/// <summary>
/// Performance models
/// </summary>
public class PerformanceMetrics
{
    public int TotalOperations { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public List<SlowOperation> SlowOperations { get; set; } = new();
    public int PerformanceAlerts { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SlowOperation
{
    public string Operation { get; set; } = string.Empty;
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public int Count { get; set; }
}

public class PerformanceAlert
{
    public string Operation { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public TimeSpan Threshold { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int TotalAccesses { get; set; }
    public double AverageAccessCount { get; set; }
    public List<string> MostAccessedKeys { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class QueryPerformanceMetric
{
    public string Query { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public int RowsAffected { get; set; }
    public DateTime Timestamp { get; set; }
}
