using Atlas.Common.ValueObjects;
using Atlas.Ledger.Domain;
using Atlas.Messaging;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Ledger.App;

public record PostRequest(string SourceAccountId, string DestinationAccountId, long Minor, string Currency, string Narration);

public record PostJournalEntryCommand(string Narration, (AccountId AccountId, Money Amount)[] Debits, (AccountId AccountId, Money Amount)[] Credits);

public sealed class PostJournalEntryHandler
{
    private readonly ILedgerRepository _repository;
    private readonly IOutboxStore _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly LedgerDbContext _dbContext;

    public PostJournalEntryHandler(ILedgerRepository repository, IOutboxStore outbox, ITenantContext tenantContext, LedgerDbContext dbContext)
    {
        _repository = repository;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
    }

    public async Task<JournalEntry> HandleAsync(PostJournalEntryCommand command, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsValid)
            throw new InvalidOperationException("Invalid tenant context");

        // Validate journal entry before processing
        ValidateJournalEntry(command);

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

        // Process all account updates within the existing transaction
        var accountsToUpdate = new List<Account>();
        
        // Collect all debit operations
        foreach (var debit in command.Debits)
        {
            var account = await _repository.GetAsync(debit.AccountId, cancellationToken);
            if (account == null)
                throw new InvalidOperationException($"Account {debit.AccountId.Value} not found");
                
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
            if (account == null)
                throw new InvalidOperationException($"Account {credit.AccountId.Value} not found");
                
            var creditResult = account.Credit(credit.Amount);
            if (!creditResult.IsSuccess)
            {
                throw new InvalidOperationException($"Credit failed: {creditResult.Error}");
            }
            accountsToUpdate.Add(account);
        }

        // Save all accounts atomically (within existing transaction)
        foreach (var account in accountsToUpdate)
        {
            await _repository.SaveAsync(account, cancellationToken);
        }

        entry.MarkPosted();

        // Publish to outbox for AML processing (after successful transaction)
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

    private static void ValidateJournalEntry(PostJournalEntryCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Narration))
            throw new ArgumentException("Narration cannot be empty", nameof(command.Narration));

        if (command.Debits.Length == 0 || command.Credits.Length == 0)
            throw new ArgumentException("Journal entry must have at least one debit and one credit");

        // Validate that total debits equal total credits
        var totalDebits = command.Debits.Sum(d => d.Amount.LedgerCents);
        var totalCredits = command.Credits.Sum(c => c.Amount.LedgerCents);
        
        if (totalDebits != totalCredits)
            throw new ArgumentException($"Journal entry is not balanced: Debits {totalDebits} != Credits {totalCredits}");

        // Validate all amounts are positive
        if (command.Debits.Any(d => d.Amount.LedgerCents <= 0))
            throw new ArgumentException("All debit amounts must be positive");
        
        if (command.Credits.Any(c => c.Amount.LedgerCents <= 0))
            throw new ArgumentException("All credit amounts must be positive");

        // Validate currency consistency
        var currencies = command.Debits.Select(d => d.Amount.Currency.Code)
            .Concat(command.Credits.Select(c => c.Amount.Currency.Code))
            .Distinct()
            .ToList();
            
        if (currencies.Count > 1)
            throw new ArgumentException($"All amounts must be in the same currency, found: {string.Join(", ", currencies)}");
    }
}
