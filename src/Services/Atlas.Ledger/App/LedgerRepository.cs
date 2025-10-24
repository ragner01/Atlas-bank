using Atlas.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.App;

/// <summary>
/// Entity Framework implementation of the ledger repository with batch operations
/// </summary>
public sealed class EfLedgerRepository : ILedgerRepository
{
    private readonly LedgerDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<EfLedgerRepository> _logger;
    
    public EfLedgerRepository(LedgerDbContext db, ITenantContext tenantContext, ILogger<EfLedgerRepository> logger) 
    { 
        _db = db; 
        _tenantContext = tenantContext; 
        _logger = logger;
    }

    /// <summary>
    /// Gets a single account by ID
    /// </summary>
    public async Task<Account> GetAsync(AccountId id, CancellationToken ct)
    {
        var row = await _db.Accounts.FindAsync([id.Value], ct);
        if (row is null)
        {
            _logger.LogWarning("Account {AccountId} not found", id.Value);
            throw new InvalidOperationException($"Account {id.Value} not found. Accounts must be created explicitly.");
        }
        
        return MapToAccount(row);
    }

    /// <summary>
    /// Gets multiple accounts by IDs in a single query (batch operation)
    /// </summary>
    public async Task<IReadOnlyList<Account>> GetBatchAsync(AccountId[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
            return Array.Empty<Account>();

        var idValues = ids.Select(id => id.Value).ToArray();
        var rows = await _db.Accounts
            .Where(a => idValues.Contains(a.Id))
            .ToListAsync(ct);

        _logger.LogDebug("Loaded {LoadedCount} out of {RequestedCount} accounts", rows.Count, ids.Length);

        return rows.Select(MapToAccount).ToList();
    }

    /// <summary>
    /// Saves a single account
    /// </summary>
    public async Task SaveAsync(Account account, CancellationToken ct)
    {
        var row = await _db.Accounts.FindAsync([account.Id.Value], ct);
        if (row is null)
        {
            row = new AccountRow 
            { 
                Id = account.Id.Value, 
                TenantId = account.TenantId.Value, 
                Currency = account.Currency.Code, 
                LedgerCents = account.Balance.LedgerCents 
            };
            _db.Accounts.Add(row);
            _logger.LogDebug("Added new account {AccountId}", account.Id.Value);
        }
        else 
        {
            row.LedgerCents = account.Balance.LedgerCents;
            _logger.LogDebug("Updated account {AccountId} balance to {Balance}", account.Id.Value, account.Balance.LedgerCents);
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Saves multiple accounts atomically (batch operation)
    /// </summary>
    public async Task SaveBatchAsync(IReadOnlyList<Account> accounts, CancellationToken ct)
    {
        if (accounts.Count == 0)
            return;

        var accountIds = accounts.Select(a => a.Id.Value).ToArray();
        var existingRows = await _db.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var accountsToAdd = new List<AccountRow>();
        var accountsToUpdate = new List<AccountRow>();

        foreach (var account in accounts)
        {
            if (existingRows.TryGetValue(account.Id.Value, out var existingRow))
            {
                existingRow.LedgerCents = account.Balance.LedgerCents;
                accountsToUpdate.Add(existingRow);
            }
            else
            {
                accountsToAdd.Add(new AccountRow
                {
                    Id = account.Id.Value,
                    TenantId = account.TenantId.Value,
                    Currency = account.Currency.Code,
                    LedgerCents = account.Balance.LedgerCents
                });
            }
        }

        if (accountsToAdd.Any())
        {
            _db.Accounts.AddRange(accountsToAdd);
            _logger.LogDebug("Added {Count} new accounts", accountsToAdd.Count);
        }

        if (accountsToUpdate.Any())
        {
            _logger.LogDebug("Updated {Count} existing accounts", accountsToUpdate.Count);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets accounts by tenant ID
    /// </summary>
    public async Task<IReadOnlyList<Account>> GetByTenantAsync(TenantId tenantId, CancellationToken ct)
    {
        var rows = await _db.Accounts
            .Where(a => a.TenantId == tenantId.Value)
            .ToListAsync(ct);

        _logger.LogDebug("Loaded {Count} accounts for tenant {TenantId}", rows.Count, tenantId.Value);
        return rows.Select(MapToAccount).ToList();
    }

    /// <summary>
    /// Maps AccountRow to Account domain object
    /// </summary>
    private static Account MapToAccount(AccountRow row)
    {
        var account = new Account(
            new EntityId(row.Id),
            new TenantId(row.TenantId),
            row.Id,
            $"Account {row.Id}",
            AccountType.Asset, // TODO: Add AccountType to database schema
            Currency.FromCode(row.Currency)
        );
        account.RestoreBalance(new Money(row.LedgerCents, Currency.FromCode(row.Currency), 2));
        return account;
    }
}
