using System.Text.Json;
using Atlas.Common.ValueObjects;

namespace Atlas.Messaging.Events;

/// <summary>
/// Base interface for all domain events
/// </summary>
public interface IDomainEvent
{
    string EventId { get; }
    string EventType { get; }
    TenantId TenantId { get; }
    Timestamp OccurredAt { get; }
    long Version { get; }
}

/// <summary>
/// Base class for domain events
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = string.Empty;
    public TenantId TenantId { get; init; }
    public Timestamp OccurredAt { get; init; } = Timestamp.Now;
    public long Version { get; init; } = 1;

    protected DomainEvent(TenantId tenantId)
    {
        TenantId = tenantId;
        EventType = GetType().Name;
    }
}

/// <summary>
/// Represents a message envelope for event publishing
/// </summary>
public record MessageEnvelope<T>
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public string CausationId { get; init; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; init; }
    public Timestamp Timestamp { get; init; } = Timestamp.Now;
    public T Payload { get; init; } = default!;
    public Dictionary<string, string> Headers { get; init; } = new();

    public MessageEnvelope(T payload, TenantId tenantId)
    {
        Payload = payload;
        TenantId = tenantId;
    }
}

/// <summary>
/// Interface for message publishers
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync<T>(MessageEnvelope<T> message, CancellationToken cancellationToken = default);
    Task PublishAsync<T>(T message, TenantId tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for message consumers
/// </summary>
public interface IMessageConsumer<T>
{
    Task ConsumeAsync(MessageEnvelope<T> message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for outbox pattern implementation
/// </summary>
public interface IOutboxStore
{
    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task MarkAsPublishedAsync(string messageId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an outbox message
/// </summary>
public record OutboxMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public string Topic { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
    public TenantId TenantId { get; init; }
    public Timestamp CreatedAt { get; init; } = Timestamp.Now;
    public OutboxMessageStatus Status { get; init; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; init; } = 0;
    public string? Error { get; init; }
    public Timestamp? PublishedAt { get; init; }
}

public enum OutboxMessageStatus
{
    Pending,
    Published,
    Failed
}
