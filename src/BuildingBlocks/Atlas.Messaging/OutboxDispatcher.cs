using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IOutboxStore _store; 
    private readonly IEventPublisher _pub; 
    private readonly ILogger<OutboxDispatcher> _log;
    
    public OutboxDispatcher(IOutboxStore s, IEventPublisher p, ILogger<OutboxDispatcher> l)
    { 
        _store = s; 
        _pub = p; 
        _log = l; 
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await _store.DequeueAsync(100, stoppingToken);
                foreach (var m in batch)
                {
                    try 
                    { 
                        // Mark as sent BEFORE publishing to prevent duplicates
                        await _store.MarkSentAsync(m.Id, stoppingToken);
                        await _pub.PublishAsync(m.Topic, m.Key, m.Payload, stoppingToken);
                        _log.LogDebug("Successfully published message {Id} to topic {Topic}", m.Id, m.Topic);
                    }
                    catch (Exception ex) 
                    { 
                        _log.LogError(ex, "Failed to publish message {Id} to topic {Topic}. Message marked as sent to prevent retry loops.", m.Id, m.Topic);
                        // Message is already marked as sent, so it won't be retried
                        // In a production system, you might want to implement a dead letter queue here
                    }
                }
                
                // If no messages were processed, wait longer
                if (batch.Length == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error in OutboxDispatcher");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
