using System.Collections.Concurrent;

namespace Atlas.Messaging;

public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentQueue<OutboxMessage> _queue = new();
    private readonly HashSet<Guid> _sent = new();

    public Task EnqueueAsync(OutboxMessage message, CancellationToken ct)
    {
        _queue.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task<OutboxMessage[]> DequeueAsync(int count, CancellationToken ct)
    {
        var messages = new List<OutboxMessage>();
        for (int i = 0; i < count && _queue.TryDequeue(out var message); i++)
        {
            if (!_sent.Contains(message.Id))
            {
                messages.Add(message);
            }
        }
        return Task.FromResult(messages.ToArray());
    }

    public Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        _sent.Add(id);
        return Task.CompletedTask;
    }
}
