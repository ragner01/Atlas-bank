# ADR-002: Outbox Pattern for Reliable Event Publishing

## Status
Accepted

## Context
In a microservices architecture with event-driven communication, we need to ensure that:
- Domain events are reliably published after state changes
- No events are lost due to system failures
- Events are published exactly once (idempotency)
- The system maintains consistency between local state and published events

## Decision
We will implement the Outbox Pattern using PostgreSQL as the transactional store, with a background service to publish events to Kafka/Event Hubs.

## Rationale

### Why Outbox Pattern?
- **Transactional Consistency**: Events are stored in the same transaction as state changes
- **Reliability**: No events are lost even if the message broker is temporarily unavailable
- **Idempotency**: Duplicate events can be detected and handled
- **Ordering**: Events can be published in the correct order
- **Audit Trail**: Complete audit trail of all events

### Why PostgreSQL for Outbox?
- **ACID Properties**: Leverages database transactions for consistency
- **Performance**: Efficient queries and indexing
- **Reliability**: PostgreSQL is battle-tested for critical data
- **Integration**: Same database as domain data for transactional consistency

### Why Background Service?
- **Decoupling**: Separates event publishing from business logic
- **Resilience**: Can retry failed publications
- **Performance**: Doesn't block business transactions
- **Scalability**: Can scale independently

## Implementation

### Database Schema
```sql
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    message_id UUID NOT NULL UNIQUE,
    topic VARCHAR(255) NOT NULL,
    payload JSONB NOT NULL,
    headers JSONB NOT NULL DEFAULT '{}',
    tenant_id VARCHAR(50) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    retry_count INTEGER NOT NULL DEFAULT 0,
    error TEXT,
    published_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_outbox_status_created ON outbox_messages(status, created_at);
CREATE INDEX idx_outbox_tenant ON outbox_messages(tenant_id);
```

### Domain Service Integration
```csharp
public class LedgerService
{
    public async Task<Result<EntityId>> PostJournalEntry(PostJournalEntryCommand command)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Update domain state
            var journalEntry = new JournalEntry(command);
            _context.JournalEntries.Add(journalEntry);
            
            // 2. Update account balances
            foreach (var line in command.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (line.Type == Debit)
                    account.Debit(line.Amount);
                else
                    account.Credit(line.Amount);
            }
            
            // 3. Store outbox message in same transaction
            var domainEvent = new JournalEntryPostedEvent(
                command.TenantId,
                journalEntry.Id,
                journalEntry.Reference,
                journalEntry.Lines
            );
            
            var outboxMessage = new OutboxMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                Topic = "journal-entry-posted",
                Payload = JsonSerializer.Serialize(domainEvent),
                Headers = new Dictionary<string, string>
                {
                    ["event-type"] = nameof(JournalEntryPostedEvent),
                    ["tenant-id"] = command.TenantId.Value
                },
                TenantId = command.TenantId
            };
            
            _context.OutboxMessages.Add(outboxMessage);
            
            // 4. Commit transaction atomically
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return Result<EntityId>.Success(journalEntry.Id);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Background Publisher Service
```csharp
public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly IMessagePublisher _messagePublisher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                
                // Get pending messages
                var messages = await outboxStore.GetPendingMessagesAsync(100, stoppingToken);
                
                foreach (var message in messages)
                {
                    try
                    {
                        // Publish to message broker
                        await _messagePublisher.PublishAsync(
                            message.Topic,
                            message.Payload,
                            message.Headers,
                            stoppingToken
                        );
                        
                        // Mark as published
                        await outboxStore.MarkAsPublishedAsync(message.Id, stoppingToken);
                        
                        _logger.LogDebug("Published outbox message {MessageId}", message.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                        
                        // Mark as failed
                        await outboxStore.MarkAsFailedAsync(message.Id, ex.Message, stoppingToken);
                        
                        // Implement exponential backoff for retries
                        if (message.RetryCount < 5)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, message.RetryCount)), stoppingToken);
                        }
                    }
                }
                
                // Wait before next batch
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox publisher service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
```

### Idempotent Consumer
```csharp
public class JournalEntryPostedEventHandler : IMessageConsumer<JournalEntryPostedEvent>
{
    private readonly IInboxStore _inboxStore;
    private readonly ILogger<JournalEntryPostedEventHandler> _logger;

    public async Task ConsumeAsync(MessageEnvelope<JournalEntryPostedEvent> message, CancellationToken cancellationToken)
    {
        // Check if already processed
        var existing = await _inboxStore.GetByMessageIdAsync(message.MessageId, cancellationToken);
        if (existing != null)
        {
            _logger.LogDebug("Message {MessageId} already processed", message.MessageId);
            return;
        }
        
        // Store in inbox for idempotency
        var inboxMessage = new InboxMessage
        {
            MessageId = message.MessageId,
            EventType = message.Payload.GetType().Name,
            Payload = JsonSerializer.Serialize(message.Payload),
            ProcessedAt = Timestamp.Now
        };
        
        await _inboxStore.AddAsync(inboxMessage, cancellationToken);
        
        // Process the event
        await ProcessJournalEntryPosted(message.Payload, cancellationToken);
        
        _logger.LogInformation("Processed journal entry posted event {JournalEntryId}", message.Payload.JournalEntryId);
    }
    
    private async Task ProcessJournalEntryPosted(JournalEntryPostedEvent @event, CancellationToken cancellationToken)
    {
        // Update read models, send notifications, etc.
        // This is idempotent and can be safely retried
    }
}
```

## Consequences

### Positive
- **Reliability**: No events are lost due to system failures
- **Consistency**: Events are published in the same transaction as state changes
- **Idempotency**: Duplicate events are handled gracefully
- **Audit Trail**: Complete audit trail of all events
- **Performance**: Business transactions are not blocked by event publishing

### Negative
- **Complexity**: Additional infrastructure and code complexity
- **Latency**: Events are published asynchronously (eventual consistency)
- **Storage**: Additional storage requirements for outbox messages
- **Monitoring**: Need to monitor outbox processing and failures

### Mitigation Strategies
- **Cleanup**: Regularly clean up processed outbox messages
- **Monitoring**: Monitor outbox processing lag and failure rates
- **Alerting**: Alert on outbox processing failures
- **Retry Logic**: Implement exponential backoff for retries
- **Dead Letter Queue**: Handle permanently failed messages

## Monitoring & Alerting

### Key Metrics
- **Outbox Processing Lag**: Time between creation and publication
- **Failed Publications**: Count of failed outbox publications
- **Retry Rate**: Percentage of messages requiring retries
- **Outbox Queue Size**: Number of pending outbox messages

### Alerts
- Outbox processing lag > 5 minutes
- Failed publication rate > 1%
- Outbox queue size > 1000 messages
- Retry rate > 10%

## Review Date
2025-06-01

## Related ADRs
- ADR-001: Ledger Consistency with Serializable Isolation
- ADR-003: Multi-Tenancy with Schema-Per-Tenant
- ADR-005: Event Sourcing for Audit Trail
