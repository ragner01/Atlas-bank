using Confluent.Kafka;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Consistency;

/// <summary>
/// Background service that tracks Debezium watermarks from Kafka outbox topics
/// to maintain per-region and global consistency watermarks in Redis
/// </summary>
public sealed class DebeziumWatermarkTracker : BackgroundService
{
    private readonly ILogger<DebeziumWatermarkTracker> _log;
    private readonly IConnectionMultiplexer _redis;
    
    /// <summary>
    /// Initializes a new instance of the DebeziumWatermarkTracker
    /// </summary>
    /// <param name="log">Logger instance</param>
    /// <param name="redis">Redis connection multiplexer</param>
    public DebeziumWatermarkTracker(ILogger<DebeziumWatermarkTracker> log, IConnectionMultiplexer redis)
    { _log = log; _redis = redis; }

    /// <summary>
    /// Executes the background service to consume Debezium events and update watermarks
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service</param>
    /// <returns>A task representing the background service execution</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));

    /// <summary>
    /// Main processing loop for consuming Kafka messages and updating watermarks
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    private void Loop(CancellationToken ct)
    {
        var cfg = new ConsumerConfig{
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "wm-tracker",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        using var c = new ConsumerBuilder<string,string>(cfg).Build();
        // Track Debezium outbox topics (regionX.public.outbox)
        c.Subscribe(new[] { "regionA.public.outbox", "regionB.public.outbox" });

        var db = _redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            var cr = c.Consume(TimeSpan.FromMilliseconds(500));
            if (cr is null) continue;

            try {
                var topic = cr.Topic; // regionX.public.outbox
                var region = topic.StartsWith("regionA") ? "regionA" : topic.StartsWith("regionB") ? "regionB" : "unknown";

                using var doc = JsonDocument.Parse(cr.Message.Value);
                if (!doc.RootElement.TryGetProperty("payload", out var payload)) continue;
                if (!payload.TryGetProperty("after", out var after) || after.ValueKind == JsonValueKind.Null) continue;

                var ts_ms = payload.TryGetProperty("ts_ms", out var ts) ? ts.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var p = after.GetProperty("payload"); // our outbox JSON
                var tenant = p.TryGetProperty("tenant", out var t) ? (t.GetString() ?? "tnt_demo") : "tnt_demo";

                // wm:{tenant}:{region} = last ts_ms seen; also keep global min across regions
                var key = $"wm:{tenant}:{region}";
                db.StringSet(key, ts_ms);
                // compute global watermark: min(regionA, regionB)
                var a = (long?)db.StringGet($"wm:{tenant}:regionA") ?? 0L;
                var b2 = (long?)db.StringGet($"wm:{tenant}:regionB") ?? 0L;
                var g = Math.Min(a, b2);
                db.StringSet($"wm:{tenant}:global", g);
            }
            catch (Exception ex) { _log.LogWarning(ex, "wm tracker error"); }
        }
    }
}
