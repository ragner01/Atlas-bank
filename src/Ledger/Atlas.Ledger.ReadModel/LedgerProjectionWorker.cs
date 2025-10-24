using Confluent.Kafka;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Atlas.Ledger.ReadModel;

public sealed class LedgerProjectionWorker : BackgroundService
{
    private readonly ILogger<LedgerProjectionWorker> _log;
    private readonly IConnectionMultiplexer _redis;

    public LedgerProjectionWorker(ILogger<LedgerProjectionWorker> log, IConnectionMultiplexer redis)
    {
        _log = log; _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) => await Task.Run(() => Loop(stoppingToken));

    private void Loop(CancellationToken ct)
    {
        var cfg = new ConsumerConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "ledger-projection",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        using var consumer = new ConsumerBuilder<string, string>(cfg).Build();
        consumer.Subscribe(Environment.GetEnvironmentVariable("TOPIC_LEDGER") ?? "ledger-events");

        var db = _redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            var cr = consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (cr is null) continue;

            try
            {
                var root = JsonDocument.Parse(cr.Message.Value).RootElement;
                long minor = root.GetProperty("minor").GetInt64();
                string ccy = root.GetProperty("currency").GetString() ?? "NGN";
                string src = root.GetProperty("source").GetString() ?? "";
                string dst = root.GetProperty("dest").GetString() ?? "";
                string tenant = root.TryGetProperty("tenant", out var t) ? t.GetString() ?? "tnt_demo" : "tnt_demo";

                // version/watermark taken from Kafka offset to ensure monotonic progress per partition
                long version = cr.Offset.Value;

                // apply -minor to src, +minor to dst
                if (!string.IsNullOrEmpty(src)) UpdateBalance(db, tenant, src, ccy, -minor, version);
                if (!string.IsNullOrEmpty(dst)) UpdateBalance(db, tenant, dst, ccy, +minor, version);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Projection failed; skipping");
            }
        }
    }

        private static void UpdateBalance(IDatabase db, string tenant, string account, string ccy, long deltaMinor, long version)
        {
            var key = $"balance:{tenant}:{account}:{ccy}";
            
            // Simple approach: read current, update, set TTL
            var current = db.HashGet(key, "minor");
            long currentVal = 0;
            if (current.HasValue && long.TryParse(current!, out var parsed)) 
                currentVal = parsed;

            var newVal = currentVal + deltaMinor;
            var fields = new HashEntry[]
            {
                new("minor", newVal),
                new("v", version),
                new("ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            };
            db.HashSet(key, fields);
            // 5 minutes TTL to allow passive cleanup; optional
            db.KeyExpire(key, TimeSpan.FromMinutes(5));
        }
}
