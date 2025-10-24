using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Extensions.Http;
using System.Data;

namespace AtlasBank.Infrastructure.Database;

/// <summary>
/// Database connection configuration with resilience and optimization
/// </summary>
public static class DatabaseConfiguration
{
    public static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured");
        }

        // Configure NpgsqlDataSource for connection pooling
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        
        // Configure connection pool settings
        dataSourceBuilder.ConnectionStringBuilder.Pooling = true;
        dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 5;
        dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 100;
        dataSourceBuilder.ConnectionStringBuilder.ConnectionIdleLifetime = 300; // 5 minutes
        dataSourceBuilder.ConnectionStringBuilder.ConnectionPruningInterval = 10; // 10 seconds
        
        // Configure timeouts
        dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = 30;
        dataSourceBuilder.ConnectionStringBuilder.Timeout = 15;
        
        // Configure SSL for production
        if (configuration.GetValue<bool>("Database:UseSSL", true))
        {
            dataSourceBuilder.ConnectionStringBuilder.SslMode = SslMode.Require;
            dataSourceBuilder.ConnectionStringBuilder.TrustServerCertificate = false;
        }

        var dataSource = dataSourceBuilder.Build();
        
        // Register data source as singleton
        services.AddSingleton(dataSource);

        // Configure Entity Framework with resilience
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            // Enable sensitive data logging only in development
            if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging", false))
            {
                options.EnableSensitiveDataLogging();
            }

            // Configure query splitting for better performance
            options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        // Register database health check
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "database", "postgresql" });
    }
}

/// <summary>
/// Database resilience policies
/// </summary>
public static class DatabaseResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount} after {timespan} seconds due to: {outcome.Exception?.Message}");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    Console.WriteLine($"Circuit breaker opened for {duration} seconds due to: {exception.Exception?.Message}");
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit breaker reset");
                });
    }
}

/// <summary>
/// Database connection monitoring
/// </summary>
public class DatabaseConnectionMonitor : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DatabaseConnectionMonitor> _logger;
    private readonly Timer _timer;
    private bool _disposed = false;

    public DatabaseConnectionMonitor(NpgsqlDataSource dataSource, ILogger<DatabaseConnectionMonitor> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        
        // Monitor connection pool every 30 seconds
        _timer = new Timer(MonitorConnections, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private void MonitorConnections(object state)
    {
        try
        {
            var stats = _dataSource.Statistics;
            
            _logger.LogInformation("Database Connection Pool Stats: " +
                "Total: {Total}, Idle: {Idle}, Busy: {Busy}, " +
                "Requests: {Requests}, WaitTime: {WaitTime}ms",
                stats.Total, stats.Idle, stats.Busy, 
                stats.TotalRequests, stats.AverageWaitTime);
            
            // Alert if pool is getting full
            if (stats.Total > 80) // 80% capacity
            {
                _logger.LogWarning("Database connection pool is {Percentage}% full", 
                    (stats.Total / 100.0) * 100);
            }
            
            // Alert if average wait time is high
            if (stats.AverageWaitTime > 1000) // 1 second
            {
                _logger.LogWarning("Database connection wait time is high: {WaitTime}ms", 
                    stats.AverageWaitTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring database connections");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Database migration runner with retry logic
/// </summary>
public class DatabaseMigrationRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationRunner> _logger;

    public DatabaseMigrationRunner(IServiceProvider serviceProvider, ILogger<DatabaseMigrationRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task RunMigrationsAsync()
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Migration retry {RetryCount} after {Timespan} seconds due to: {Exception}",
                        retryCount, timespan, exception.Message);
                });

        await retryPolicy.ExecuteAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            _logger.LogInformation("Starting database migration...");
            
            await context.Database.MigrateAsync();
            
            _logger.LogInformation("Database migration completed successfully");
        });
    }
}

