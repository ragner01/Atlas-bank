using System.Collections.Concurrent;

namespace Atlas.Messaging;

public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentQueue<OutboxMessage> _queue = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _sent = new();
    private readonly Timer _cleanupTimer;

    public InMemoryOutboxStore()
    {
        // Clean up old sent messages every hour
        _cleanupTimer = new Timer(CleanupOldMessages, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

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
            if (!_sent.ContainsKey(message.Id))
            {
                messages.Add(message);
            }
        }
        return Task.FromResult(messages.ToArray());
    }

    public Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        _sent.TryAdd(id, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    private void CleanupOldMessages(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24); // Keep sent messages for 24 hours
        var keysToRemove = _sent
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _sent.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}
