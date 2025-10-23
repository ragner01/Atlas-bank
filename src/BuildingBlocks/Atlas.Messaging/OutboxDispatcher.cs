using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxStore _store; private readonly IEventPublisher _pub; private readonly ILogger<OutboxDispatcher> _log;
    public OutboxDispatcher(IOutboxStore s, IEventPublisher p, ILogger<OutboxDispatcher> l){ _store=s; _pub=p; _log=l; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await _store.DequeueAsync(100, stoppingToken);
            foreach (var m in batch)
            {
                try { await _pub.PublishAsync(m.Topic, m.Key, m.Payload, stoppingToken); await _store.MarkSentAsync(m.Id, stoppingToken); }
                catch (Exception ex) { _log.LogWarning(ex, "Outbox publish failed {Id}", m.Id); }
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
