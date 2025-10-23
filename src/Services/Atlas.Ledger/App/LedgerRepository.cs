using Atlas.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.App;

public sealed class EfLedgerRepository : ILedgerRepository
{
    private readonly LedgerDbContext _db;
    public EfLedgerRepository(LedgerDbContext db) => _db = db;

    public async Task<Account> GetAsync(AccountId id, CancellationToken ct)
    {
        var row = await _db.Accounts.FindAsync([id.Value], ct);
        if (row is null)
        {
            // Create a new account if it doesn't exist
            var newAccount = new Account(
                new EntityId(id.Value),
                new TenantId("tnt_demo"), // TODO: Get from context
                id.Value,
                $"Account {id.Value}",
                AccountType.Asset, // Default to Asset
                Currency.FromCode("NGN") // Default currency
            );
            return newAccount;
        }
        
        var account = new Account(
            new EntityId(row.Id),
            new TenantId(row.TenantId),
            row.Id,
            $"Account {row.Id}",
            AccountType.Asset, // TODO: Store account type in DB
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
