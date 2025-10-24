using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Extensions.Http;
using System.Data;

namespace AtlasBank.Infrastructure.Database;

/// <summary>
/// Database connection resilience configuration
/// </summary>
public static class DatabaseResilienceConfiguration
{
    public static void ConfigureDatabaseResilience(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure connection string with retry logic
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Database connection string not found");

        // Add retry policy for database connections
        services.AddSingleton<IDbConnectionFactory>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DatabaseConnectionFactory>>();
            return new DatabaseConnectionFactory(connectionString, logger);
        });

        // Configure Entity Framework with resilience
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                
                npgsqlOptions.CommandTimeout(30);
            });
        });

        // Add health checks for database
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "database", "postgresql" });
    }
}

/// <summary>
/// Database connection factory with resilience
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
    Task<IDbConnection> CreateConnectionWithRetryAsync(int maxRetries = 3);
}

public class DatabaseConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseConnectionFactory> _logger;
    private readonly IAsyncPolicy<IDbConnection> _retryPolicy;

    public DatabaseConnectionFactory(string connectionString, ILogger<DatabaseConnectionFactory> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        
        _retryPolicy = Policy
            .Handle<SqlException>()
            .Or<NpgsqlException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Database connection retry {RetryCount} after {Delay}ms. Error: {Error}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message);
                });
    }

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        try
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database connection");
            throw;
        }
    }

    public async Task<IDbConnection> CreateConnectionWithRetryAsync(int maxRetries = 3)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // Test connection with a simple query
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();
            
            return connection;
        });
    }
}

/// <summary>
/// Database transaction manager with resilience
/// </summary>
public interface IDatabaseTransactionManager
{
    Task<T> ExecuteInTransactionAsync<T>(Func<IDbTransaction, Task<T>> operation);
    Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> operation);
}

public class DatabaseTransactionManager : IDatabaseTransactionManager
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseTransactionManager> _logger;

    public DatabaseTransactionManager(IDbConnectionFactory connectionFactory, ILogger<DatabaseTransactionManager> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<IDbTransaction, Task<T>> operation)
    {
        var connection = await _connectionFactory.CreateConnectionWithRetryAsync();
        var transaction = connection.BeginTransaction();
        
        try
        {
            var result = await operation(transaction);
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public async Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> operation)
    {
        var connection = await _connectionFactory.CreateConnectionWithRetryAsync();
        var transaction = connection.BeginTransaction();
        
        try
        {
            await operation(transaction);
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory, ILogger<DatabaseHealthCheck> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionWithRetryAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            
            if (result?.ToString() == "1")
            {
                return HealthCheckResult.Healthy("Database connection is healthy");
            }
            
            return HealthCheckResult.Unhealthy("Database query returned unexpected result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

