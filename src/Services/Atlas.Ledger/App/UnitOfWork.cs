using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Atlas.Ledger.Domain;
using System.Data;
using Npgsql;

namespace Atlas.Ledger.App;

/// <summary>
/// Unit of Work pattern for managing transactions across multiple operations
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Begins a new transaction with the specified isolation level
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.Serializable, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Commits the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation within a transaction with automatic retry for serialization failures
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, int maxRetries = 3, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an operation within a transaction with automatic retry for serialization failures
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, int maxRetries = 3, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Unit of Work pattern using Entity Framework
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly LedgerDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public EfUnitOfWork(LedgerDbContext context, ILogger<EfUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.Serializable, CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("Transaction already started");

        _transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        _logger.LogDebug("Transaction started with isolation level: {IsolationLevel}", isolationLevel);
        return _transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("No active transaction to commit");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return;

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Transaction rolled back");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction");
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var result = await operation();
                await CommitAsync(cancellationToken);
                return result;
            }
            catch (PostgresException ex) when (ex.SqlState == "40001" && attempt < maxRetries) // serialization_failure
            {
                _logger.LogWarning("Serialization failure on attempt {Attempt}/{MaxRetries}, retrying...", attempt, maxRetries);
                await RollbackAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken); // Exponential backoff
            }
            catch (Exception)
            {
                await RollbackAsync(cancellationToken);
                throw;
            }
        }
        
        throw new InvalidOperationException($"Operation failed after {maxRetries} attempts due to serialization conflicts");
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, maxRetries, cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _context.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_transaction != null)
                await _transaction.DisposeAsync();
            await _context.DisposeAsync();
            _disposed = true;
        }
    }
}
