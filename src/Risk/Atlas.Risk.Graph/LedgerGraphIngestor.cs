using Confluent.Kafka;
using Neo4j.Driver;
using System.Text.Json;

namespace Atlas.Risk.Graph;

public sealed class LedgerGraphIngestor : BackgroundService
{
    private readonly ILogger<LedgerGraphIngestor> _log;
    private readonly IDriver _neo;
    public LedgerGraphIngestor(ILogger<LedgerGraphIngestor> log, IDriver neo) { _log = log; _neo = neo; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));

    private void Loop(CancellationToken ct)
    {
        var cfg = new ConsumerConfig{
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "ledger-graph",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        using var c = new ConsumerBuilder<string,string>(cfg).Build();
        c.Subscribe(Environment.GetEnvironmentVariable("TOPIC_LEDGER") ?? "ledger-events");

        while(!ct.IsCancellationRequested)
        {
            var cr = c.Consume(TimeSpan.FromMilliseconds(250));
            if (cr is null) continue;
            try
            {
                var e = JsonDocument.Parse(cr.Message.Value).RootElement;
                var tenant = e.GetProperty("tenant").GetString() ?? "tnt_demo";
                var src = e.GetProperty("source").GetString() ?? "";
                var dst = e.GetProperty("dest").GetString() ?? "";
                var minor = e.GetProperty("minor").GetInt64();
                var currency = e.GetProperty("currency").GetString() ?? "NGN";
                var ts = e.TryGetProperty("ts", out var tj) ? tj.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                using var session = _neo.AsyncSession(o => o.WithDatabase("neo4j"));
                var cypher = @"
MERGE (t:Tenant {id:$tenant})
MERGE (a:Account {id:$src})-[:BELONGS_TO]->(t)
MERGE (b:Account {id:$dst})-[:BELONGS_TO]->(t)
MERGE (a)-[r:TRANSFER_TO]->(b)
SET r.ts = $ts, r.minor = $minor, r.currency = $currency
";
                session.RunAsync(cypher, new { tenant, src, dst, ts, minor, currency })
                    .GetAwaiter().GetResult().ConsumeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex) { _log.LogWarning(ex, "Ledger graph ingest failed"); }
        }
    }
}
