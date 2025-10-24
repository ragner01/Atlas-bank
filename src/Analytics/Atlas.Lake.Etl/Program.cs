using Azure.Storage.Blobs;
using Confluent.Kafka;
using Parquet;
using Parquet.Data;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Lake.Etl")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/lake-etl-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Lake ETL Service");

    var b = Host.CreateApplicationBuilder(args);
    
    // Use Serilog
    b.Host.UseSerilog();

    string bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092";
    string container = Environment.GetEnvironmentVariable("LAKE_CONTAINER") ?? "lake";
    var blob = new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONN") ?? "UseDevelopmentStorage=true");
    
    b.Services.AddSingleton(blob);
    b.Services.AddHostedService<LedgerToParquet>();
    
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
/// Background service that consumes Kafka ledger events and writes them to Parquet files in blob storage
/// </summary>
public sealed class LedgerToParquet : BackgroundService
{
    private readonly BlobServiceClient _blob;
    private readonly ILogger<LedgerToParquet> _logger;

    public LedgerToParquet(BlobServiceClient blob, ILogger<LedgerToParquet> logger)
    {
        _blob = blob;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(() => Loop(stoppingToken), stoppingToken);
    }

    private async Task Loop(CancellationToken ct)
    {
        var containerName = Environment.GetEnvironmentVariable("LAKE_CONTAINER") ?? "lake";
        var container = _blob.GetBlobContainerClient(containerName);
        
        try
        {
            await container.CreateIfNotExistsAsync(cancellationToken: ct);
            Log.Information("Created or verified blob container: {ContainerName}", containerName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create blob container: {ContainerName}", containerName);
            throw;
        }

        var cfg = new ConsumerConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "lake-ledger-parquet",
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

        // Batching configuration
        var rows = new List<LedgerRow>();
        var tLastFlush = DateTime.UtcNow;
        var maxRows = int.TryParse(Environment.GetEnvironmentVariable("LAKE_BATCH_ROWS"), out var r) ? r : 5000;
        var maxSec = int.TryParse(Environment.GetEnvironmentVariable("LAKE_BATCH_SECONDS"), out var s) ? s : 15;

        Log.Information("Starting ETL loop with batch settings: MaxRows={MaxRows}, MaxSeconds={MaxSeconds}", maxRows, maxSec);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = consumer.Consume(TimeSpan.FromMilliseconds(250));
                if (cr is null)
                {
                    await MaybeFlush();
                    continue;
                }

                using (LogContext.PushProperty("KafkaOffset", cr.Offset.Value))
                using (LogContext.PushProperty("KafkaPartition", cr.Partition.Value))
                {
                    await ProcessMessage(cr.Message.Value, rows);
                    
                    if (rows.Count >= maxRows || (DateTime.UtcNow - tLastFlush).TotalSeconds >= maxSec)
                    {
                        await Flush();
                    }
                }
            }
            catch (ConsumeException ex)
            {
                Log.Error(ex, "Kafka consume error: {Error}", ex.Error.Reason);
                await Task.Delay(1000, ct); // Brief delay before retrying
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in ETL loop");
                await Task.Delay(1000, ct);
            }

            async Task MaybeFlush()
            {
                if (rows.Count > 0 && (DateTime.UtcNow - tLastFlush).TotalSeconds >= maxSec)
                {
                    await Flush();
                }
            }

            async Task ProcessMessage(string messageValue, List<LedgerRow> rows)
            {
                try
                {
                    var doc = JsonDocument.Parse(messageValue).RootElement;
                    var ts = doc.TryGetProperty("ts_ms", out var tsm) ? tsm.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var payload = doc.TryGetProperty("payload", out var p) ? p : doc;
                    
                    string tenant = payload.TryGetProperty("tenant", out var t) ? t.GetString() ?? "tnt_demo" : "tnt_demo";
                    string src = payload.GetProperty("source").GetString() ?? "";
                    string dst = payload.GetProperty("dest").GetString() ?? "";
                    long minor = payload.TryGetProperty("minor", out var m) ? m.GetInt64() : 0;
                    string ccy = payload.TryGetProperty("currency", out var ccyj) ? ccyj.GetString() ?? "NGN" : "NGN";
                    string entry = payload.TryGetProperty("entry_id", out var eid) ? (eid.GetString() ?? "") : (payload.TryGetProperty("entryId", out var ei2) ? ei2.GetString() ?? "" : "");
                    string narration = payload.TryGetProperty("narration", out var n) ? n.GetString() ?? "" : "";

                    rows.Add(new LedgerRow(tenant, ts, src, dst, minor, ccy, entry, narration));
                    
                    if (rows.Count % 1000 == 0)
                    {
                        Log.Debug("Processed {Count} rows in current batch", rows.Count);
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
                if (rows.Count == 0) return;

                try
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(rows.Last().Ts).UtcDateTime;
                    var path = $"ledger/dt={dt:yyyy-MM-dd}/hour={dt:HH}/part_{Guid.NewGuid():N}.parquet";
                    
                    Log.Information("Flushing {Count} rows to Parquet file: {Path}", rows.Count, path);

                    var schema = new Schema(
                        new DataField<string>("tenant"),
                        new DataField<long>("ts_ms"),
                        new DataField<string>("src"),
                        new DataField<string>("dst"),
                        new DataField<long>("minor"),
                        new DataField<string>("currency"),
                        new DataField<string>("entry_id"),
                        new DataField<string>("narration")
                    );

                    using var ms = new MemoryStream();
                    using (var writer = await ParquetWriter.CreateAsync(schema, ms))
                    {
                        using var rg = writer.CreateRowGroup();
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[0], rows.Select(x => x.Tenant).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[1], rows.Select(x => x.Ts).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[2], rows.Select(x => x.Src).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[3], rows.Select(x => x.Dst).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[4], rows.Select(x => x.Minor).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[5], rows.Select(x => x.Ccy).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[6], rows.Select(x => x.EntryId).ToArray()));
                        await rg.WriteColumnAsync(new DataColumn((DataField)schema.Fields[7], rows.Select(x => x.Narration).ToArray()));
                    }

                    ms.Position = 0;
                    await container.UploadBlobAsync(path, ms, ct);
                    
                    Log.Information("Successfully uploaded Parquet file: {Path} ({Size} bytes)", path, ms.Length);
                    
                    rows.Clear();
                    tLastFlush = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to flush {Count} rows to Parquet", rows.Count);
                    // Don't clear rows on error - they'll be retried on next flush
                }
            }
        }
    }
}

/// <summary>
/// Represents a ledger transaction row for Parquet storage
/// </summary>
public readonly record struct LedgerRow(
    string Tenant,
    long Ts,
    string Src,
    string Dst,
    long Minor,
    string Ccy,
    string EntryId,
    string Narration
);
