using Atlas.Common.ValueObjects;
using Atlas.Ledger.Domain;

namespace Atlas.Ledger.App;

/// <summary>
/// Represents a unique account identifier
/// </summary>
public record AccountId(string Value);

/// <summary>
/// Repository interface for ledger operations with batch support
/// </summary>
public interface ILedgerRepository
{
    /// <summary>
    /// Gets a single account by ID
    /// </summary>
    Task<Account> GetAsync(AccountId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets multiple accounts by IDs in a single query (batch operation)
    /// </summary>
    Task<IReadOnlyList<Account>> GetBatchAsync(AccountId[] ids, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves a single account
    /// </summary>
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves multiple accounts atomically (batch operation)
    /// </summary>
    Task SaveBatchAsync(IReadOnlyList<Account> accounts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets accounts by tenant ID
    /// </summary>
    Task<IReadOnlyList<Account>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
