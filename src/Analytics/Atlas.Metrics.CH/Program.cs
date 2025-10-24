using Confluent.Kafka;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Metrics.CH")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/metrics-ch-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank ClickHouse Metrics Service");

    var b = Host.CreateApplicationBuilder(args);
    
    // Use Serilog
    b.Host.UseSerilog();
    
    b.Services.AddHostedService<ClickHouseIngestor>();
    
    b.Build().Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Background service that consumes Kafka ledger events and ingests them into ClickHouse
/// </summary>
public sealed class ClickHouseIngestor : BackgroundService
{
    private readonly HttpClient _http;
    private readonly ILogger<ClickHouseIngestor> _logger;

    public ClickHouseIngestor(ILogger<ClickHouseIngestor> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(() => Loop(stoppingToken), stoppingToken);
    }

    private async Task Loop(CancellationToken ct)
    {
        var chBase = Environment.GetEnvironmentVariable("CLICKHOUSE_HTTP") ?? "http://clickhouse:8123";
        
        try
        {
            // Create table if not exists
            await CreateTableIfNotExists(chBase, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create ClickHouse table");
            throw;
        }

        var cfg = new ConsumerConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "ch-ingest",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            SessionTimeoutMs = 30000,
            HeartbeatIntervalMs = 10000
        };

        using var consumer = new ConsumerBuilder<string, string>(cfg).Build();
        var topic = Environment.GetEnvironmentVariable("TOPIC_LEDGER") ?? "ledger-events";
        
        try
        {
            consumer.Subscribe(topic);
            Log.Information("Subscribed to Kafka topic: {Topic}", topic);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to subscribe to Kafka topic: {Topic}", topic);
            throw;
        }

        var batch = new List<TxEvent>();
        var last = DateTime.UtcNow;
        var maxRows = int.TryParse(Environment.GetEnvironmentVariable("CH_BATCH_ROWS"), out var r) ? r : 5000;
        var maxSec = int.TryParse(Environment.GetEnvironmentVariable("CH_BATCH_SECONDS"), out var s) ? s : 5;

        Log.Information("Starting ClickHouse ingestion with batch settings: MaxRows={MaxRows}, MaxSeconds={MaxSeconds}", maxRows, maxSec);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = consumer.Consume(TimeSpan.FromMilliseconds(200));
                if (cr is null)
                {
                    if (batch.Count > 0 && (DateTime.UtcNow - last).TotalSeconds >= maxSec)
                    {
                        await Flush();
                    }
                    continue;
                }

                using (LogContext.PushProperty("KafkaOffset", cr.Offset.Value))
                using (LogContext.PushProperty("KafkaPartition", cr.Partition.Value))
                {
                    await ProcessMessage(cr.Message.Value, batch);
                    
                    if (batch.Count >= maxRows)
                    {
                        await Flush();
                    }
                }
            }
            catch (ConsumeException ex)
            {
                Log.Error(ex, "Kafka consume error: {Error}", ex.Error.Reason);
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in ClickHouse ingestion loop");
                await Task.Delay(1000, ct);
            }

            async Task ProcessMessage(string messageValue, List<TxEvent> batch)
            {
                try
                {
                    var e = JsonDocument.Parse(messageValue).RootElement;
                    var p = e.TryGetProperty("payload", out var payload) ? payload : e;
                    
                    ulong ts = (ulong)(p.TryGetProperty("ts_ms", out var tsm) ? tsm.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    string tenant = p.TryGetProperty("tenant", out var t) ? t.GetString() ?? "tnt_demo" : "tnt_demo";
                    string src = p.TryGetProperty("source", out var s1) ? s1.GetString() ?? "" : "";
                    string dst = p.TryGetProperty("dest", out var d1) ? d1.GetString() ?? "" : "";
                    long minor = p.TryGetProperty("minor", out var m1) ? m1.GetInt64() : 0;
                    string ccy = p.TryGetProperty("currency", out var c1) ? c1.GetString() ?? "NGN" : "NGN";
                    string entry = p.TryGetProperty("entry_id", out var e1) ? e1.GetString() ?? "" : (p.TryGetProperty("entryId", out var e2) ? e2.GetString() ?? "" : "");
                    
                    batch.Add(new TxEvent(ts, tenant, src, dst, minor, ccy, entry));
                    
                    if (batch.Count % 1000 == 0)
                    {
                        Log.Debug("Processed {Count} events in current batch", batch.Count);
                    }
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Failed to parse JSON message, skipping: {Message}", messageValue[..Math.Min(200, messageValue.Length)]);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to process message, skipping: {Message}", messageValue[..Math.Min(200, messageValue.Length)]);
                }
            }

            async Task Flush()
            {
                if (batch.Count == 0) return;

                try
                {
                    Log.Information("Flushing {Count} events to ClickHouse", batch.Count);
                    
                    var content = string.Join("\n", batch.Select(e => JsonSerializer.Serialize(e)));
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{chBase}/?query=" + Uri.EscapeDataString("INSERT INTO tx_events FORMAT JSONEachRow"));
                    req.Content = new StringContent(content, Encoding.UTF8, "application/json");
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    var res = await _http.SendAsync(req, ct);
                    res.EnsureSuccessStatusCode();
                    
                    Log.Information("Successfully inserted {Count} events into ClickHouse", batch.Count);
                    
                    batch.Clear();
                    last = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to flush {Count} events to ClickHouse", batch.Count);
                    // Don't clear batch on error - they'll be retried on next flush
                }
            }
        }
    }

    private async Task CreateTableIfNotExists(string chBase, CancellationToken ct)
    {
        var createTableQuery = @"
CREATE TABLE IF NOT EXISTS tx_events (
  ts_ms UInt64,
  tenant LowCardinality(String),
  src String,
  dst String,
  minor Int64,
  currency LowCardinality(String),
  entry_id String
) ENGINE = MergeTree() 
PARTITION BY toDate(fromUnixTimestamp64Milli(ts_ms)) 
ORDER BY (tenant, ts_ms)
SETTINGS index_granularity = 8192";

        var response = await _http.PostAsync($"{chBase}/?query=" + Uri.EscapeDataString(createTableQuery), null, ct);
        response.EnsureSuccessStatusCode();
        
        Log.Information("Created or verified ClickHouse table: tx_events");
    }

    public override void Dispose()
    {
        _http?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Represents a transaction event for ClickHouse storage
/// </summary>
public readonly record struct TxEvent(
    ulong ts_ms,
    string tenant,
    string src,
    string dst,
    long minor,
    string currency,
    string entry_id
);
