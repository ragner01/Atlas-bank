using Atlas.Common.ValueObjects;
using Atlas.Messaging.Events;
using Atlas.Ledger.App;

namespace Atlas.Ledger.Domain;

public enum JournalEntryLineType { Debit, Credit }
public enum JournalEntryStatus { Pending, Posted, Cancelled }

public record JournalEntryLine(AccountId AccountId, Money Amount, JournalEntryLineType Type);

public record JournalEntryId(Guid Value);

public record JournalEntryPostedEvent(JournalEntryId JournalEntryId, TenantId TenantId, DateTimeOffset BookingDate) : DomainEvent(TenantId);

public class JournalEntry
{
    public JournalEntryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset BookingDate { get; private set; }
    public string Narrative { get; private set; }
    public List<JournalEntryLine> Debits { get; private set; }
    public List<JournalEntryLine> Credits { get; private set; }
    public JournalEntryStatus Status { get; private set; }

    // For EF Core
    private JournalEntry()
    {
        Id = new JournalEntryId(Guid.NewGuid());
        TenantId = default!;
        Narrative = string.Empty;
        Debits = new List<JournalEntryLine>();
        Credits = new List<JournalEntryLine>();
    }

    public JournalEntry(JournalEntryId id, TenantId tenantId, DateTimeOffset bookingDate, string narrative, IEnumerable<JournalEntryLine> debits, IEnumerable<JournalEntryLine> credits)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(narrative))
            throw new ArgumentException("Narrative cannot be empty", nameof(narrative));
        
        var debitList = debits.ToList();
        var creditList = credits.ToList();
        
        if (debitList.Count == 0 || creditList.Count == 0)
            throw new ArgumentException("Journal entry must have at least one debit and one credit");
        
        // Validate that total debits equal total credits
        var totalDebits = debitList.Sum(d => d.Amount.LedgerCents);
        var totalCredits = creditList.Sum(c => c.Amount.LedgerCents);
        
        if (totalDebits != totalCredits)
            throw new ArgumentException($"Journal entry is not balanced: Debits {totalDebits} != Credits {totalCredits}");
        
        // Validate all amounts are positive
        if (debitList.Any(d => d.Amount.LedgerCents <= 0))
            throw new ArgumentException("All debit amounts must be positive");
        
        if (creditList.Any(c => c.Amount.LedgerCents <= 0))
            throw new ArgumentException("All credit amounts must be positive");

        Id = id;
        TenantId = tenantId;
        BookingDate = bookingDate;
        Narrative = narrative;
        Debits = debitList;
        Credits = creditList;
        Status = JournalEntryStatus.Pending;
    }

    public void MarkPosted()
    {
        Status = JournalEntryStatus.Posted;
    }

    public void Cancel()
    {
        Status = JournalEntryStatus.Cancelled;
    }
}