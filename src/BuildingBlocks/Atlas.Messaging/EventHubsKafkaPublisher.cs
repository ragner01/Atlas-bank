using Confluent.Kafka;

namespace Atlas.Messaging;

public sealed class KafkaPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    public KafkaPublisher(string bootstrap)
    {
        var cfg = new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = true };
        _producer = new ProducerBuilder<string, string>(cfg).Build();
    }
    public async Task PublishAsync(string topic, string key, string payload, CancellationToken ct)
        => await _producer.ProduceAsync(topic, new Message<string,string>{ Key=key, Value=payload }, ct);
    public ValueTask DisposeAsync() { _producer.Flush(); _producer.Dispose(); return ValueTask.CompletedTask; }
}
