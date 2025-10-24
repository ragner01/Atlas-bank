using Atlas.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.App;

public sealed class EfLedgerRepository : ILedgerRepository
{
    private readonly LedgerDbContext _db;
    private readonly ITenantContext _tenantContext;
    
    public EfLedgerRepository(LedgerDbContext db, ITenantContext tenantContext) 
    { 
        _db = db; 
        _tenantContext = tenantContext; 
    }

    public async Task<Account> GetAsync(AccountId id, CancellationToken ct)
    {
        var row = await _db.Accounts.FindAsync([id.Value], ct);
        if (row is null)
        {
            // Create a new account if it doesn't exist
            var tenantId = _tenantContext.IsValid ? _tenantContext.CurrentTenant : new TenantId("tnt_demo");
            var newAccount = new Account(
                new EntityId(id.Value),
                tenantId,
                id.Value,
                $"Account {id.Value}",
                AccountType.Asset, // Default to Asset - in production this should be determined by business rules
                Currency.FromCode("NGN") // Default currency - in production this should be configurable
            );
            return newAccount;
        }
        
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
        }
        else 
        {
            row.LedgerCents = account.Balance.LedgerCents;
        }
        await _db.SaveChangesAsync(ct);
    }
}
