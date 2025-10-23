using Atlas.Common.ValueObjects;

namespace Atlas.Messaging;

/// <summary>
/// Interface for publishing messages to a message broker
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the specified topic
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    /// <param name="topic">Topic name</param>
    /// <param name="message">Message to publish</param>
    /// <param name="tenantId">Tenant ID for multi-tenancy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the publish operation</returns>
    Task PublishAsync<T>(string topic, T message, TenantId tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for consuming messages from a message broker
/// </summary>
/// <typeparam name="T">Message type</typeparam>
public interface IMessageConsumer<T>
{
    /// <summary>
    /// Starts consuming messages from the specified topic
    /// </summary>
    /// <param name="topic">Topic name</param>
    /// <param name="handler">Message handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the consume operation</returns>
    Task StartConsumingAsync(string topic, Func<MessageEnvelope<T>, Task> handler, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops consuming messages
    /// </summary>
    /// <returns>Task representing the stop operation</returns>
    Task StopConsumingAsync();
}

/// <summary>
/// Message envelope containing metadata
/// </summary>
/// <typeparam name="T">Message type</typeparam>
public class MessageEnvelope<T>
{
    public T Message { get; set; } = default!;
    public TenantId TenantId { get; set; } = default!;
    public DateTime Timestamp { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
}
