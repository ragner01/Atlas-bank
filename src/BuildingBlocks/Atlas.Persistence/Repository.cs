using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Common.ValueObjects;
using Atlas.Common.Results;
using Atlas.Messaging.Events;

namespace Atlas.Persistence;

/// <summary>
/// Base interface for repositories
/// </summary>
public interface IRepository<TEntity, TKey> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base repository implementation with EF Core
/// </summary>
public abstract class Repository<TEntity, TKey> : IRepository<TEntity, TKey> 
    where TEntity : class
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    protected Repository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await DbSet.AddAsync(entity, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            return Result<TEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<TEntity>.Failure($"Failed to add entity: {ex.Message}");
        }
    }

    public virtual async Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        try
        {
            DbSet.Update(entity);
            await Context.SaveChangesAsync(cancellationToken);
            return Result<TEntity>.Success(entity);
        }
        catch (Exception ex)
        {
            return Result<TEntity>.Failure($"Failed to update entity: {ex.Message}");
        }
    }

    public virtual async Task<Result> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return Result.Failure("Entity not found");

            DbSet.Remove(entity);
            await Context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete entity: {ex.Message}");
        }
    }
}

/// <summary>
/// Unit of Work pattern implementation
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    Task<Result> ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
    IDbContextTransaction BeginTransaction();
}

/// <summary>
/// EF Core Unit of Work implementation
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(DbContext context)
    {
        _context = context;
    }

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Result.Failure($"Concurrency conflict: {ex.Message}", "CONCURRENCY_CONFLICT");
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure($"Database update failed: {ex.Message}", "DATABASE_UPDATE_FAILED");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Unexpected error: {ex.Message}", "UNEXPECTED_ERROR");
        }
    }

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<T>.Failure($"Transaction failed: {ex.Message}");
        }
    }

    public async Task<Result> ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation();
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure($"Transaction failed: {ex.Message}");
        }
    }

    public IDbContextTransaction BeginTransaction()
    {
        _transaction = _context.Database.BeginTransaction();
        return _transaction;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

/// <summary>
/// Outbox pattern implementation for reliable message publishing
/// </summary>
public class OutboxStore : IOutboxStore
{
    private readonly DbContext _context;
    private readonly DbSet<OutboxMessage> _outboxMessages;

    public OutboxStore(DbContext context)
    {
        _context = context;
        _outboxMessages = context.Set<OutboxMessage>();
    }

    public async Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return await _outboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsPublishedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var message = await _outboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message = message with 
            { 
                Status = OutboxMessageStatus.Published, 
                PublishedAt = Timestamp.Now 
            };
            _outboxMessages.Update(message);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _outboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message = message with 
            { 
                Status = OutboxMessageStatus.Failed, 
                Error = error,
                RetryCount = message.RetryCount + 1
            };
            _outboxMessages.Update(message);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Service collection extensions for persistence
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence<TContext>(this IServiceCollection services, string connectionString)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            });
            
            options.EnableSensitiveDataLogging(false);
            options.EnableServiceProviderCaching();
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxStore, OutboxStore>();

        return services;
    }
}
