# ADR-001: Ledger Consistency with Serializable Isolation

## Status
Accepted

## Context
The ledger is the core of the banking system and must maintain absolute consistency. We need to ensure that:
- Double-entry accounting principles are always maintained
- Concurrent transactions don't lead to inconsistent balances
- Audit trails are immutable and verifiable
- Performance requirements are met (≥ 2k postings/sec)

## Decision
We will use PostgreSQL with SERIALIZABLE isolation level for all ledger operations, implementing retry logic with exponential backoff for serialization failures.

## Rationale

### Why Serializable Isolation?
- **ACID Compliance**: Ensures complete isolation between concurrent transactions
- **Double-Entry Guarantee**: Prevents phantom reads that could break accounting invariants
- **Audit Integrity**: Maintains immutable audit trail without gaps
- **Regulatory Compliance**: Meets banking regulations for financial record keeping

### Why PostgreSQL?
- **Proven Reliability**: Battle-tested in financial systems
- **Serializable Snapshot Isolation**: Efficient implementation of serializable isolation
- **JSON Support**: Native support for complex data structures
- **Performance**: Can handle high-throughput with proper indexing
- **Multi-tenancy**: Schema-per-tenant isolation

### Retry Strategy
```csharp
public async Task<Result<T>> ExecuteWithRetry<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (PostgresException ex) when (ex.SqlState == "40001") // Serialization failure
        {
            if (attempt == maxRetries - 1) throw;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100));
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}
```

## Consequences

### Positive
- **Strong Consistency**: Guaranteed double-entry accounting integrity
- **Audit Compliance**: Immutable audit trail for regulatory requirements
- **Data Integrity**: No phantom reads or dirty reads possible
- **Regulatory Approval**: Meets banking industry standards

### Negative
- **Performance Impact**: Serializable isolation can reduce throughput
- **Retry Logic**: Additional complexity in application code
- **Deadlock Potential**: Higher chance of serialization failures under load
- **Monitoring Required**: Need to track retry rates and performance

### Mitigation Strategies
- **Connection Pooling**: Optimize database connections
- **Batch Operations**: Group related operations in single transactions
- **Read Replicas**: Use read replicas for reporting queries
- **Circuit Breakers**: Implement circuit breakers for external dependencies
- **Monitoring**: Track serialization failure rates and adjust retry policies

## Implementation Details

### Database Configuration
```sql
-- Enable serializable isolation
SET default_transaction_isolation = 'serializable';

-- Optimize for serializable transactions
SET random_page_cost = 1.1;
SET effective_cache_size = '4GB';
```

### Application Code
```csharp
public class LedgerService
{
    public async Task<Result<EntityId>> PostJournalEntry(PostJournalEntryCommand command)
    {
        return await ExecuteWithRetry(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            
            // Validate double-entry principle
            var totalDebits = command.Lines.Where(l => l.Type == Debit).Sum(l => l.Amount.Value);
            var totalCredits = command.Lines.Where(l => l.Type == Credit).Sum(l => l.Amount.Value);
            
            if (Math.Abs(totalDebits - totalCredits) > 0.01m)
                throw new InvalidOperationException("Journal entry not balanced");
            
            // Update account balances atomically
            foreach (var line in command.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (line.Type == Debit)
                    account.Debit(line.Amount);
                else
                    account.Credit(line.Amount);
            }
            
            // Create journal entry
            var journalEntry = new JournalEntry(command);
            _context.JournalEntries.Add(journalEntry);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return Result<EntityId>.Success(journalEntry.Id);
        });
    }
}
```

## Monitoring & Alerting

### Key Metrics
- **Serialization Failure Rate**: Should be < 5% under normal load
- **Transaction Duration**: P99 < 100ms for ledger operations
- **Retry Count**: Track retry attempts per operation
- **Deadlock Detection**: Monitor for deadlock patterns

### Alerts
- Serialization failure rate > 10%
- Transaction duration P99 > 200ms
- Retry count > 5 per operation
- Database connection pool exhaustion

## Review Date
2025-06-01

## Related ADRs
- ADR-002: Outbox Pattern for Event Publishing
- ADR-003: Multi-Tenancy with Schema-Per-Tenant
- ADR-004: PCI DSS Compliance Architecture
