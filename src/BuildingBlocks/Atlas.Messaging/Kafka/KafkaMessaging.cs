using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Atlas.Common.ValueObjects;

namespace Atlas.Messaging.Kafka;

/// <summary>
/// Kafka message publisher implementation
/// </summary>
public class KafkaMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessagePublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public KafkaMessagePublisher(
        IProducer<string, string> producer,
        ILogger<KafkaMessagePublisher> logger)
    {
        _producer = producer;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task PublishAsync<T>(string topic, T message, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(message, _jsonOptions);
            
            var kafkaMessage = new Message<string, string>
            {
                Key = tenantId.Value,
                Value = payload,
                Headers = new Headers
                {
                    { "tenant-id", System.Text.Encoding.UTF8.GetBytes(tenantId.Value) },
                    { "message-type", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) }
                }
            };

            await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            _logger.LogInformation("Published message of type {MessageType} to topic {Topic}", 
                typeof(T).Name, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType} to topic {Topic}", 
                typeof(T).Name, topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}

/// <summary>
/// Kafka message consumer implementation
/// </summary>
public class KafkaMessageConsumer<T> : BackgroundService, IMessageConsumer<T>
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaMessageConsumer<T>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _topic;
    private Func<MessageEnvelope<T>, Task>? _messageHandler;

    public KafkaMessageConsumer(
        IConsumer<string, string> consumer,
        ILogger<KafkaMessageConsumer<T>> logger,
        string topic)
    {
        _consumer = consumer;
        _logger = logger;
        _topic = topic;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task StartConsumingAsync(string topic, Func<MessageEnvelope<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        _messageHandler = handler;
        _consumer.Subscribe(topic);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(cancellationToken);
                if (result?.Message != null)
                {
                    var message = JsonSerializer.Deserialize<T>(result.Message.Value, _jsonOptions);
                    if (message != null)
                    {
                        var envelope = new MessageEnvelope<T>
                        {
                            Message = message,
                            TenantId = new TenantId(result.Message.Key),
                            Timestamp = DateTime.UtcNow,
                            MessageId = Guid.NewGuid().ToString(),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        await _messageHandler(envelope);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming message from topic {Topic}", topic);
            }
        }
    }

    public async Task StopConsumingAsync()
    {
        _consumer.Close();
        await Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartConsumingAsync(_topic, async envelope => 
        {
            _logger.LogInformation("Received message of type {MessageType}", typeof(T).Name);
            await Task.CompletedTask;
        }, stoppingToken);
    }
}

/// <summary>
/// Kafka configuration and setup
/// </summary>
public static class KafkaConfiguration
{
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, string bootstrapServers)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "atlas-bank",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        services.AddSingleton<IProducer<string, string>>(provider =>
        {
            var producer = new ProducerBuilder<string, string>(producerConfig).Build();
            return producer;
        });

        services.AddSingleton<IConsumer<string, string>>(provider =>
        {
            var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            return consumer;
        });

        services.AddScoped<IMessagePublisher, KafkaMessagePublisher>();

        return services;
    }
}