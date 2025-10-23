using Atlas.Common.ValueObjects;
using Atlas.Ledger.Domain;
using Atlas.Messaging;
using System.Text.Json;

namespace Atlas.Ledger.App;

public record PostRequest(string SourceAccountId, string DestinationAccountId, long Minor, string Currency, string Narration);

public record PostJournalEntryCommand(string Narration, (AccountId AccountId, Money Amount)[] Debits, (AccountId AccountId, Money Amount)[] Credits);

public sealed class PostJournalEntryHandler
{
    private readonly ILedgerRepository _repository;
    private readonly IOutboxStore _outbox;
    private readonly ITenantContext _tenantContext;

    public PostJournalEntryHandler(ILedgerRepository repository, IOutboxStore outbox, ITenantContext tenantContext)
    {
        _repository = repository;
        _outbox = outbox;
        _tenantContext = tenantContext;
    }

    public async Task<JournalEntry> HandleAsync(PostJournalEntryCommand command, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsValid)
            throw new InvalidOperationException("Invalid tenant context");

        var entryId = new JournalEntryId(Guid.NewGuid());
        var tenantId = _tenantContext.CurrentTenant;
        
        // Create journal entry
        var entry = new JournalEntry(
            entryId,
            tenantId,
            DateTimeOffset.UtcNow,
            command.Narration,
            command.Debits.Select(d => new JournalEntryLine(d.AccountId, d.Amount, JournalEntryLineType.Debit)),
            command.Credits.Select(c => new JournalEntryLine(c.AccountId, c.Amount, JournalEntryLineType.Credit))
        );

        // Process all account updates in a single transaction
        var accountsToUpdate = new List<Account>();
        
        // Collect all debit operations
        foreach (var debit in command.Debits)
        {
            var account = await _repository.GetAsync(debit.AccountId, cancellationToken);
            var debitResult = account.Debit(debit.Amount);
            if (!debitResult.IsSuccess)
            {
                throw new InvalidOperationException($"Debit failed: {debitResult.Error}");
            }
            accountsToUpdate.Add(account);
        }

        // Collect all credit operations
        foreach (var credit in command.Credits)
        {
            var account = await _repository.GetAsync(credit.AccountId, cancellationToken);
            var creditResult = account.Credit(credit.Amount);
            if (!creditResult.IsSuccess)
            {
                throw new InvalidOperationException($"Credit failed: {creditResult.Error}");
            }
            accountsToUpdate.Add(account);
        }

        // Save all accounts atomically
        foreach (var account in accountsToUpdate)
        {
            await _repository.SaveAsync(account, cancellationToken);
        }

        entry.MarkPosted();

        // Publish to outbox for AML processing
        await _outbox.EnqueueAsync(new OutboxMessage(
            Guid.NewGuid(), 
            "ledger-events", 
            entry.Id.Value.ToString(),
            JsonSerializer.Serialize(new {
                minor = command.Debits.Sum(d => d.Amount.LedgerCents),
                currency = command.Debits.First().Amount.Currency.Code,
                source = command.Debits.First().AccountId.Value,
                dest = command.Credits.First().AccountId.Value
            }),
            DateTimeOffset.UtcNow), 
            cancellationToken);

        return entry;
    }
}
