using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Add Kafka consumer
builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
        GroupId = "realtime-balance",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true,
        AutoCommitIntervalMs = 1000,
        SessionTimeoutMs = 30000,
        HeartbeatIntervalMs = 10000
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

// Add background service
builder.Services.AddHostedService<BalanceStreamer>();

// Configure CORS for WebSocket connections
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure CORS
app.UseCors("AllowAll");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    ok = true, 
    timestamp = DateTime.UtcNow,
    service = "Atlas.Realtime",
    version = "1.0.0"
}));

// SignalR hub endpoint
app.MapHub<BalanceHub>("/ws");

// Add connection info endpoint
app.MapGet("/ws/info", () => Results.Ok(new {
    endpoint = "/ws",
    protocols = new[] { "websocket" },
    features = new[] { "balance-updates", "real-time-notifications" }
}));

app.Run();

/// <summary>
/// SignalR hub for real-time balance updates
/// </summary>
public class BalanceHub : Hub
{
    private readonly ILogger<BalanceHub> _logger;

    public BalanceHub(ILogger<BalanceHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to balance updates for a specific account
    /// </summary>
    /// <param name="accountId">Account ID (e.g., "msisdn::2348100000001")</param>
    public async Task SubscribeBalance(string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogWarning("Empty account ID provided for subscription");
            return;
        }

        var groupName = $"acct::{accountId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} subscribed to balance updates for account {AccountId}", 
            Context.ConnectionId, accountId);
        
        // Send confirmation
        await Clients.Caller.SendAsync("subscriptionConfirmed", new { 
            accountId, 
            groupName,
            timestamp = DateTime.UtcNow 
        });
    }

    /// <summary>
    /// Unsubscribe from balance updates for a specific account
    /// </summary>
    /// <param name="accountId">Account ID</param>
    public async Task UnsubscribeBalance(string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogWarning("Empty account ID provided for unsubscription");
            return;
        }

        var groupName = $"acct::{accountId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} unsubscribed from balance updates for account {AccountId}", 
            Context.ConnectionId, accountId);
    }

    /// <summary>
    /// Subscribe to multiple accounts at once
    /// </summary>
    /// <param name="accountIds">Array of account IDs</param>
    public async Task SubscribeMultipleBalances(string[] accountIds)
    {
        if (accountIds == null || accountIds.Length == 0)
        {
            _logger.LogWarning("Empty account IDs provided for multiple subscription");
            return;
        }

        var tasks = accountIds.Select(accountId => SubscribeBalance(accountId));
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Client {ConnectionId} subscribed to {Count} accounts", 
            Context.ConnectionId, accountIds.Length);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to BalanceHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from BalanceHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Background service that streams balance updates from Kafka to SignalR clients
/// </summary>
public sealed class BalanceStreamer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IHubContext<BalanceHub> _hubContext;
    private readonly ILogger<BalanceStreamer> _logger;
    private readonly string _topic;

    public BalanceStreamer(
        IConsumer<string, string> consumer, 
        IHubContext<BalanceHub> hubContext,
        ILogger<BalanceStreamer> logger)
    {
        _consumer = consumer;
        _hubContext = hubContext;
        _logger = logger;
        _topic = Environment.GetEnvironmentVariable("TOPIC_BALANCE_UPDATES") ?? "balance-updates";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting BalanceStreamer for topic: {Topic}", _topic);
        
        try
        {
            _consumer.Subscribe(_topic);
            _logger.LogInformation("Subscribed to Kafka topic: {Topic}", _topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(250));
                    
                    if (consumeResult == null)
                        continue;

                    await ProcessBalanceUpdate(consumeResult, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Error}", ex.Error.Reason);
                    
                    // Handle specific Kafka errors
                    if (ex.Error.Code == ErrorCode.PartitionEOF)
                    {
                        _logger.LogDebug("Reached end of partition");
                        continue;
                    }
                    
                    // For other errors, wait a bit before retrying
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in BalanceStreamer");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in BalanceStreamer");
        }
        finally
        {
            _consumer.Close();
            _logger.LogInformation("BalanceStreamer stopped");
        }
    }

    private async Task ProcessBalanceUpdate(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        try
        {
            var messageValue = consumeResult.Message.Value;
            _logger.LogDebug("Received balance update: {Message}", messageValue);

            // Parse the balance update message
            using var document = JsonDocument.Parse(messageValue);
            var root = document.RootElement;

            if (!root.TryGetProperty("accountId", out var accountIdElement))
            {
                _logger.LogWarning("Balance update message missing accountId: {Message}", messageValue);
                return;
            }

            var accountId = accountIdElement.GetString();
            if (string.IsNullOrEmpty(accountId))
            {
                _logger.LogWarning("Balance update message has empty accountId");
                return;
            }

            // Send to all clients subscribed to this account
            var groupName = $"acct::{accountId}";
            await _hubContext.Clients.Group(groupName)
                .SendAsync("balanceUpdate", messageValue, cancellationToken);

            _logger.LogDebug("Sent balance update to group {GroupName} for account {AccountId}", 
                groupName, accountId);

            // Commit the offset
            _consumer.Commit(consumeResult);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse balance update message: {Message}", consumeResult.Message.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing balance update");
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
