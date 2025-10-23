namespace Atlas.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default);
}

public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<OutboxMessage[]> DequeueAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);
}

public record OutboxMessage(Guid Id, string Topic, string Key, string Payload, DateTimeOffset OccurredAt);
