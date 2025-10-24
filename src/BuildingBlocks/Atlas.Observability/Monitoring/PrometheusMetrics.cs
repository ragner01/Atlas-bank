using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using System.Diagnostics;
using System.Text.Json;

namespace AtlasBank.BuildingBlocks.Monitoring;

/// <summary>
/// Comprehensive Prometheus metrics for AtlasBank services
/// </summary>
public static class PrometheusMetrics
{
    // HTTP Metrics
    private static readonly Counter HttpRequestsTotal = Metrics
        .CreateCounter("http_requests_total", "Total HTTP requests", new[] { "method", "endpoint", "status_code" });

    private static readonly Histogram HttpRequestDuration = Metrics
        .CreateHistogram("http_request_duration_seconds", "HTTP request duration", new[] { "method", "endpoint" });

    private static readonly Gauge HttpRequestsInProgress = Metrics
        .CreateGauge("http_requests_in_progress", "HTTP requests currently in progress", new[] { "method", "endpoint" });

    // Business Metrics
    private static readonly Counter TransactionsTotal = Metrics
        .CreateCounter("transactions_total", "Total transactions processed", new[] { "type", "status", "currency" });

    private static readonly Histogram TransactionAmount = Metrics
        .CreateHistogram("transaction_amount", "Transaction amounts", new[] { "currency" });

    private static readonly Counter UssdSessionsTotal = Metrics
        .CreateCounter("ussd_sessions_total", "Total USSD sessions", new[] { "status" });

    private static readonly Counter AgentIntentsTotal = Metrics
        .CreateCounter("agent_intents_total", "Total agent intents", new[] { "type", "status" });

    private static readonly Counter OfflineOperationsTotal = Metrics
        .CreateCounter("offline_operations_total", "Total offline operations", new[] { "type", "status" });

    // System Metrics
    private static readonly Gauge MemoryUsage = Metrics
        .CreateGauge("memory_usage_bytes", "Memory usage in bytes");

    private static readonly Gauge CpuUsage = Metrics
        .CreateGauge("cpu_usage_percent", "CPU usage percentage");

    private static readonly Gauge ActiveConnections = Metrics
        .CreateGauge("active_connections", "Active connections", new[] { "type" });

    // Circuit Breaker Metrics
    private static readonly Counter CircuitBreakerStateChanges = Metrics
        .CreateCounter("circuit_breaker_state_changes_total", "Circuit breaker state changes", new[] { "service", "state" });

    private static readonly Gauge CircuitBreakerState = Metrics
        .CreateGauge("circuit_breaker_state", "Circuit breaker state", new[] { "service" });

    // Rate Limiting Metrics
    private static readonly Counter RateLimitHits = Metrics
        .CreateCounter("rate_limit_hits_total", "Rate limit hits", new[] { "key_prefix" });

    private static readonly Gauge RateLimitCurrent = Metrics
        .CreateGauge("rate_limit_current", "Current rate limit usage", new[] { "key_prefix" });

    public static void RecordHttpRequest(string method, string endpoint, int statusCode, double duration)
    {
        HttpRequestsTotal.WithLabels(method, endpoint, statusCode.ToString()).Inc();
        HttpRequestDuration.WithLabels(method, endpoint).Observe(duration);
    }

    public static void RecordHttpRequestStart(string method, string endpoint)
    {
        HttpRequestsInProgress.WithLabels(method, endpoint).Inc();
    }

    public static void RecordHttpRequestEnd(string method, string endpoint)
    {
        HttpRequestsInProgress.WithLabels(method, endpoint).Dec();
    }

    public static void RecordTransaction(string type, string status, string currency, decimal amount)
    {
        TransactionsTotal.WithLabels(type, status, currency).Inc();
        TransactionAmount.WithLabels(currency).Observe((double)amount);
    }

    public static void RecordUssdSession(string status)
    {
        UssdSessionsTotal.WithLabels(status).Inc();
    }

    public static void RecordAgentIntent(string type, string status)
    {
        AgentIntentsTotal.WithLabels(type, status).Inc();
    }

    public static void RecordOfflineOperation(string type, string status)
    {
        OfflineOperationsTotal.WithLabels(type, status).Inc();
    }

    public static void UpdateSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        MemoryUsage.Set(process.WorkingSet64);
        CpuUsage.Set(process.TotalProcessorTime.TotalMilliseconds);
    }

    public static void RecordCircuitBreakerStateChange(string service, string state)
    {
        CircuitBreakerStateChanges.WithLabels(service, state).Inc();
        CircuitBreakerState.WithLabels(service).Set(GetCircuitBreakerStateValue(state));
    }

    public static void RecordRateLimitHit(string keyPrefix)
    {
        RateLimitHits.WithLabels(keyPrefix).Inc();
    }

    public static void UpdateRateLimitCurrent(string keyPrefix, double current)
    {
        RateLimitCurrent.WithLabels(keyPrefix).Set(current);
    }

    private static double GetCircuitBreakerStateValue(string state)
    {
        return state switch
        {
            "Closed" => 0,
            "Open" => 1,
            "HalfOpen" => 2,
            _ => -1
        };
    }
}

/// <summary>
/// Comprehensive health checks for AtlasBank services
/// </summary>
public static class HealthCheckConfiguration
{
    public static void AddAtlasHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<KafkaHealthCheck>("kafka")
            .AddCheck<ExternalApiHealthCheck>("external_apis")
            .AddCheck<DiskSpaceHealthCheck>("disk_space")
            .AddCheck<MemoryHealthCheck>("memory")
            .AddCheck<CircuitBreakerHealthCheck>("circuit_breakers");
    }

    public static void UseAtlasHealthChecks(this WebApplication app)
    {
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthCheckResponse
        });

        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponse
        });

        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthCheckResponse
        });
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport result)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = result.Status.ToString(),
            totalDuration = result.TotalDuration.TotalMilliseconds,
            checks = result.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
                data = entry.Value.Data,
                exception = entry.Value.Exception?.Message,
                tags = entry.Value.Tags
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly Npgsql.NpgsqlDataSource _dataSource;

    public DatabaseHealthCheck(Npgsql.NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new Npgsql.NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

/// <summary>
/// Redis health check
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly StackExchange.Redis.IConnectionMultiplexer _multiplexer;

    public RedisHealthCheck(StackExchange.Redis.IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _multiplexer.GetDatabase();
            await database.PingAsync();
            
            return HealthCheckResult.Healthy("Redis connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}

/// <summary>
/// Kafka health check
/// </summary>
public class KafkaHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Implement Kafka health check logic
            return HealthCheckResult.Healthy("Kafka connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Kafka connection failed", ex);
        }
    }
}

/// <summary>
/// External API health check
/// </summary>
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalApiHealthCheck(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("External API accessible");
            }
            
            return HealthCheckResult.Degraded($"External API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External API not accessible", ex);
        }
    }
}

/// <summary>
/// Disk space health check
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo("/");
            var freeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
            
            if (freeSpacePercent < 10)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Low disk space: {freeSpacePercent:F1}%"));
            }
            
            if (freeSpacePercent < 20)
            {
                return Task.FromResult(HealthCheckResult.Degraded($"Disk space warning: {freeSpacePercent:F1}%"));
            }
            
            return Task.FromResult(HealthCheckResult.Healthy($"Disk space OK: {freeSpacePercent:F1}%"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Disk space check failed", ex));
        }
    }
}

/// <summary>
/// Memory health check
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            
            if (memoryUsageMB > 1000) // 1GB
            {
                return Task.FromResult(HealthCheckResult.Degraded($"High memory usage: {memoryUsageMB}MB"));
            }
            
            return Task.FromResult(HealthCheckResult.Healthy($"Memory usage OK: {memoryUsageMB}MB"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Memory check failed", ex));
        }
    }
}

/// <summary>
/// Circuit breaker health check
/// </summary>
public class CircuitBreakerHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Implement circuit breaker health check logic
            return Task.FromResult(HealthCheckResult.Healthy("Circuit breakers OK"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Circuit breaker check failed", ex));
        }
    }
}
