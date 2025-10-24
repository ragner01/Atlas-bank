using Atlas.Common.ValueObjects;
using Atlas.Ledger.Domain;
using Atlas.Messaging;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Ledger.App;

/// <summary>
/// Request model for posting journal entries
/// </summary>
public record PostRequest(string SourceAccountId, string DestinationAccountId, long Minor, string Currency, string Narration);

/// <summary>
/// Command for posting journal entries with proper transaction management
/// </summary>
public record PostJournalEntryCommand(string Narration, (AccountId AccountId, Money Amount)[] Debits, (AccountId AccountId, Money Amount)[] Credits);

/// <summary>
/// Handler for posting journal entries with proper transaction management and batch operations
/// </summary>
public sealed class PostJournalEntryHandler
{
    private readonly ILedgerRepository _repository;
    private readonly IOutboxStore _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostJournalEntryHandler> _logger;

    public PostJournalEntryHandler(
        ILedgerRepository repository, 
        IOutboxStore outbox, 
        ITenantContext tenantContext, 
        IUnitOfWork unitOfWork,
        ILogger<PostJournalEntryHandler> logger)
    {
        _repository = repository;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Handles posting a journal entry with proper transaction management and batch operations
    /// </summary>
    public async Task<JournalEntry> HandleAsync(PostJournalEntryCommand command, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting journal entry processing with correlation ID: {CorrelationId}", correlationId);

        if (!_tenantContext.IsValid)
        {
            _logger.LogError("Invalid tenant context for correlation ID: {CorrelationId}", correlationId);
            throw new InvalidOperationException("Invalid tenant context");
        }

        // Validate journal entry before processing
        ValidateJournalEntry(command);

        var entryId = new JournalEntryId(Guid.NewGuid());
        var tenantId = _tenantContext.CurrentTenant;
        
        _logger.LogDebug("Processing journal entry {EntryId} for tenant {TenantId} with correlation ID: {CorrelationId}", 
            entryId.Value, tenantId.Value, correlationId);

        // Execute all operations within a transaction with automatic retry
        return await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Create journal entry
            var entry = new JournalEntry(
                entryId,
                tenantId,
                DateTimeOffset.UtcNow,
                command.Narration,
                command.Debits.Select(d => new JournalEntryLine(d.AccountId, d.Amount, JournalEntryLineType.Debit)),
                command.Credits.Select(c => new JournalEntryLine(c.AccountId, c.Amount, JournalEntryLineType.Credit))
            );

            // Get all unique account IDs to avoid N+1 queries
            var allAccountIds = command.Debits.Select(d => d.AccountId)
                .Concat(command.Credits.Select(c => c.AccountId))
                .Distinct()
                .ToArray();

            _logger.LogDebug("Loading {AccountCount} accounts for correlation ID: {CorrelationId}", 
                allAccountIds.Length, correlationId);

            // Batch load all accounts in a single query
            var accounts = await _repository.GetBatchAsync(allAccountIds, cancellationToken);
            var accountLookup = accounts.ToDictionary(a => a.Id.Value);

            // Validate all accounts exist
            var missingAccounts = allAccountIds.Where(id => !accountLookup.ContainsKey(id.Value)).ToArray();
            if (missingAccounts.Any())
            {
                var missingIds = string.Join(", ", missingAccounts.Select(a => a.Value));
                _logger.LogError("Missing accounts: {MissingAccounts} for correlation ID: {CorrelationId}", 
                    missingIds, correlationId);
                throw new InvalidOperationException($"Accounts not found: {missingIds}");
            }

            // Process all debit operations
            foreach (var debit in command.Debits)
            {
                var account = accountLookup[debit.AccountId.Value];
                var debitResult = account.Debit(debit.Amount);
                if (!debitResult.IsSuccess)
                {
                    _logger.LogError("Debit failed for account {AccountId}: {Error} for correlation ID: {CorrelationId}", 
                        debit.AccountId.Value, debitResult.Error, correlationId);
                    throw new InvalidOperationException($"Debit failed for account {debit.AccountId.Value}: {debitResult.Error}");
                }
                _logger.LogDebug("Debited {Amount} from account {AccountId} for correlation ID: {CorrelationId}", 
                    debit.Amount.LedgerCents, debit.AccountId.Value, correlationId);
            }

            // Process all credit operations
            foreach (var credit in command.Credits)
            {
                var account = accountLookup[credit.AccountId.Value];
                var creditResult = account.Credit(credit.Amount);
                if (!creditResult.IsSuccess)
                {
                    _logger.LogError("Credit failed for account {AccountId}: {Error} for correlation ID: {CorrelationId}", 
                        credit.AccountId.Value, creditResult.Error, correlationId);
                    throw new InvalidOperationException($"Credit failed for account {credit.AccountId.Value}: {creditResult.Error}");
                }
                _logger.LogDebug("Credited {Amount} to account {AccountId} for correlation ID: {CorrelationId}", 
                    credit.Amount.LedgerCents, credit.AccountId.Value, correlationId);
            }

            // Batch save all accounts atomically
            _logger.LogDebug("Saving {AccountCount} accounts for correlation ID: {CorrelationId}", 
                accounts.Count, correlationId);
            await _repository.SaveBatchAsync(accounts, cancellationToken);

            entry.MarkPosted();

            // Publish to outbox for AML processing (after successful transaction)
            var eventPayload = new
            {
                correlationId,
                tenant = tenantId.Value,
                minor = command.Debits.Sum(d => d.Amount.LedgerCents),
                currency = command.Debits.First().Amount.Currency.Code,
                source = command.Debits.First().AccountId.Value,
                dest = command.Credits.First().AccountId.Value,
                entryId = entryId.Value
            };

            await _outbox.EnqueueAsync(new OutboxMessage(
                Guid.NewGuid(), 
                "ledger-events", 
                entry.Id.Value.ToString(),
                JsonSerializer.Serialize(eventPayload),
                DateTimeOffset.UtcNow), 
                cancellationToken);

            _logger.LogInformation("Successfully processed journal entry {EntryId} with correlation ID: {CorrelationId}", 
                entryId.Value, correlationId);

            return entry;
        }, cancellationToken: cancellationToken);
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
