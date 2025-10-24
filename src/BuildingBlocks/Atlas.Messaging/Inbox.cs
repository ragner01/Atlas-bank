namespace Atlas.Messaging;

public interface IInbox
{
    Task<bool> HasProcessedAsync(string consumer, string messageId, CancellationToken ct);
    Task MarkProcessedAsync(string consumer, string messageId, CancellationToken ct);
}
