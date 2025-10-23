using Atlas.Common.ValueObjects;
using Atlas.Ledger.Domain;

namespace Atlas.Ledger.App;

public record AccountId(string Value);

public interface ILedgerRepository
{
    Task<Account> GetAsync(AccountId id, CancellationToken cancellationToken = default);
    Task SaveAsync(Account account, CancellationToken cancellationToken = default);
}
