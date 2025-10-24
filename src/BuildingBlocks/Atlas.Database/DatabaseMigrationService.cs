using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Atlas.Database;

/// <summary>
/// Configuration options for database migrations
/// </summary>
public class DatabaseMigrationOptions
{
    /// <summary>
    /// Connection string for the database
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Whether to run migrations automatically on startup
    /// </summary>
    public bool AutoMigrate { get; set; } = false;

    /// <summary>
    /// Whether to create database if it doesn't exist
    /// </summary>
    public bool CreateDatabaseIfNotExists { get; set; } = false;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable sensitive data logging
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;
}

/// <summary>
/// Validates DatabaseMigrationOptions configuration
/// </summary>
public class DatabaseMigrationOptionsValidator : IValidateOptions<DatabaseMigrationOptions>
{
    /// <summary>
    /// Validates the DatabaseMigrationOptions configuration
    /// </summary>
    /// <param name="name">The configuration section name</param>
    /// <param name="options">The options to validate</param>
    /// <returns>Validation result</returns>
    public ValidateOptionsResult Validate(string? name, DatabaseMigrationOptions options)
    {
        if (options == null)
            return ValidateOptionsResult.Fail("DatabaseMigrationOptions cannot be null");

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            failures.Add("ConnectionString is required");

        if (options.CommandTimeoutSeconds <= 0)
            failures.Add("CommandTimeoutSeconds must be greater than 0");

        return failures.Count > 0 
            ? ValidateOptionsResult.Fail(failures) 
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Service for managing database migrations
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Ensures the database exists
    /// </summary>
    Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies pending migrations
    /// </summary>
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending migrations
    /// </summary>
    Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets applied migrations
    /// </summary>
    Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IDatabaseMigrationService
/// </summary>
public class DatabaseMigrationService<TContext> : IDatabaseMigrationService where TContext : DbContext
{
    private readonly TContext _context;
    private readonly DatabaseMigrationOptions _options;
    private readonly ILogger<DatabaseMigrationService<TContext>> _logger;

    public DatabaseMigrationService(
        TContext context, 
        IOptions<DatabaseMigrationOptions> options, 
        ILogger<DatabaseMigrationService<TContext>> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CreateDatabaseIfNotExists)
            return;

        try
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_options.ConnectionString);
            var databaseName = connectionStringBuilder.Database;
            connectionStringBuilder.Database = "postgres"; // Connect to default database

            using var connection = new NpgsqlConnection(connectionStringBuilder.ToString());
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT 1 FROM pg_database WHERE datname = @databaseName";
            command.Parameters.AddWithValue("databaseName", databaseName ?? "");

            var exists = await command.ExecuteScalarAsync(cancellationToken) != null;

            if (!exists)
            {
                _logger.LogInformation("Creating database {DatabaseName}", databaseName);
                command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Database {DatabaseName} created successfully", databaseName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database exists");
            throw;
        }
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Applying database migrations");

            var pendingMigrations = await GetPendingMigrationsAsync(cancellationToken);
            if (!pendingMigrations.Any())
            {
                _logger.LogInformation("No pending migrations to apply");
                return;
            }

            _logger.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pendingMigrations));

            await _context.Database.MigrateAsync(cancellationToken);

            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var appliedMigrations = await GetAppliedMigrationsAsync(cancellationToken);
            var allMigrations = _context.Database.GetMigrations();
            
            return allMigrations.Except(appliedMigrations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending migrations");
            throw;
        }
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.GetAppliedMigrationsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get applied migrations");
            throw;
        }
    }
}

/// <summary>
/// Extension methods for dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds database migration services to the service collection
    /// </summary>
    public static IServiceCollection AddDatabaseMigrations<TContext>(
        this IServiceCollection services, 
        Action<DatabaseMigrationOptions>? configureOptions = null) 
        where TContext : DbContext
    {
        services.Configure<DatabaseMigrationOptions>(options =>
        {
            configureOptions?.Invoke(options);
        });

        services.AddSingleton<IValidateOptions<DatabaseMigrationOptions>, DatabaseMigrationOptionsValidator>();
        services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService<TContext>>();

        return services;
    }
}
