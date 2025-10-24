using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using System.Text.Json;

namespace AtlasBank.BuildingBlocks.RateLimiting;

/// <summary>
/// Redis-based distributed rate limiter for AtlasBank services
/// Implements sliding window rate limiting with Redis backend
/// </summary>
public class RedisDistributedRateLimiter : RateLimiter
{
    private readonly IDatabase _database;
    private readonly RateLimiterOptions _options;
    private readonly SemaphoreSlim _semaphore;

    public RedisDistributedRateLimiter(IDatabase database, RateLimiterOptions options)
    {
        _database = database;
        _options = options;
        _semaphore = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        return AttemptAcquireCoreAsync(permitCount).GetAwaiter().GetResult();
    }

    protected override ValueTask<RateLimitLease> AttemptAcquireCoreAsync(int permitCount)
    {
        return AttemptAcquireCoreAsync(permitCount, CancellationToken.None);
    }

    protected override ValueTask<RateLimitLease> AttemptAcquireCoreAsync(int permitCount, CancellationToken cancellationToken)
    {
        return new ValueTask<RateLimitLease>(AttemptAcquireCoreAsyncInternal(permitCount, cancellationToken));
    }

    private async Task<RateLimitLease> AttemptAcquireCoreAsyncInternal(int permitCount, CancellationToken cancellationToken)
    {
        var acquired = await _semaphore.WaitAsync(0, cancellationToken);
        if (!acquired)
        {
            return new RedisRateLimitLease(false, TimeSpan.Zero, _options);
        }

        try
        {
            var key = $"rate_limit:{_options.KeyPrefix}:{DateTime.UtcNow:yyyyMMddHHmm}";
            var current = await _database.StringIncrementAsync(key);
            
            if (current == 1)
            {
                await _database.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
            }

            if (current <= _options.PermitsPerMinute)
            {
                return new RedisRateLimitLease(true, TimeSpan.FromMinutes(1), _options);
            }

            return new RedisRateLimitLease(false, TimeSpan.FromMinutes(1), _options);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _semaphore?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class RateLimiterOptions
{
    public string KeyPrefix { get; set; } = "default";
    public int PermitsPerMinute { get; set; } = 100;
    public int MaxConcurrentRequests { get; set; } = 10;
}

public class RedisRateLimitLease : RateLimitLease
{
    private readonly bool _isAcquired;
    private readonly TimeSpan _retryAfter;
    private readonly RateLimiterOptions _options;

    public RedisRateLimitLease(bool isAcquired, TimeSpan retryAfter, RateLimiterOptions options)
    {
        _isAcquired = isAcquired;
        _retryAfter = retryAfter;
        _options = options;
    }

    public override bool IsAcquired => _isAcquired;

    public override IEnumerable<string> MetadataNames => new[] { "RetryAfter", "PermitsPerMinute" };

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        switch (metadataName)
        {
            case "RetryAfter":
                metadata = _retryAfter.TotalSeconds;
                return true;
            case "PermitsPerMinute":
                metadata = _options.PermitsPerMinute;
                return true;
            default:
                metadata = null;
                return false;
        }
    }
}

/// <summary>
/// Circuit breaker implementation for dependency failures
/// Implements the Circuit Breaker pattern with Redis state persistence
/// </summary>
public class RedisCircuitBreaker
{
    private readonly IDatabase _database;
    private readonly CircuitBreakerOptions _options;
    private readonly SemaphoreSlim _semaphore;

    public RedisCircuitBreaker(IDatabase database, CircuitBreakerOptions options)
    {
        _database = database;
        _options = options;
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<CircuitBreakerState> GetStateAsync(string serviceName)
    {
        var key = $"circuit_breaker:{serviceName}";
        var stateJson = await _database.StringGetAsync(key);
        
        if (stateJson.IsNullOrEmpty)
        {
            return new CircuitBreakerState { State = CircuitState.Closed, FailureCount = 0, LastFailureTime = null };
        }

        return JsonSerializer.Deserialize<CircuitBreakerState>(stateJson!)!;
    }

    public async Task<bool> CanExecuteAsync(string serviceName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var state = await GetStateAsync(serviceName);
            var now = DateTime.UtcNow;

            switch (state.State)
            {
                case CircuitState.Closed:
                    return true;
                
                case CircuitState.Open:
                    if (now - state.LastFailureTime > _options.Timeout)
                    {
                        // Move to half-open
                        state.State = CircuitState.HalfOpen;
                        await SaveStateAsync(serviceName, state);
                        return true;
                    }
                    return false;
                
                case CircuitState.HalfOpen:
                    return true;
                
                default:
                    return false;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecordSuccessAsync(string serviceName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var state = await GetStateAsync(serviceName);
            state.State = CircuitState.Closed;
            state.FailureCount = 0;
            state.LastFailureTime = null;
            await SaveStateAsync(serviceName, state);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecordFailureAsync(string serviceName)
    {
        await _semaphore.WaitAsync();
        try
        {
            var state = await GetStateAsync(serviceName);
            state.FailureCount++;
            state.LastFailureTime = DateTime.UtcNow;

            if (state.FailureCount >= _options.FailureThreshold)
            {
                state.State = CircuitState.Open;
            }

            await SaveStateAsync(serviceName, state);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveStateAsync(string serviceName, CircuitBreakerState state)
    {
        var key = $"circuit_breaker:{serviceName}";
        var stateJson = JsonSerializer.Serialize(state);
        await _database.StringSetAsync(key, stateJson, TimeSpan.FromHours(1));
    }
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
}

public class CircuitBreakerState
{
    public CircuitState State { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastFailureTime { get; set; }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}
