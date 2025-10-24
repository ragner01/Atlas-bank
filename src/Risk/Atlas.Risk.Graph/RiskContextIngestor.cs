using Confluent.Kafka;
using Neo4j.Driver;
using System.Text.Json;

namespace Atlas.Risk.Graph;

/// <summary>
/// Background service that consumes risk-events from Kafka and updates the Neo4j graph
/// with device, IP, and merchant context for enhanced risk scoring
/// </summary>
public sealed class RiskContextIngestor : BackgroundService
{
    private readonly ILogger<RiskContextIngestor> _log;
    private readonly IDriver _neo;
    public RiskContextIngestor(ILogger<RiskContextIngestor> log, IDriver neo) { _log = log; _neo = neo; }

    /// <summary>
    /// Executes the background service to consume risk-events from Kafka
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for stopping the service</param>
    /// <returns>A task representing the background service execution</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));

    /// <summary>
    /// Main processing loop for consuming Kafka messages
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    private void Loop(CancellationToken ct)
    {
        var cfg = new ConsumerConfig{
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "risk-context",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        using var c = new ConsumerBuilder<string,string>(cfg).Build();
        c.Subscribe(Environment.GetEnvironmentVariable("TOPIC_RISK") ?? "risk-events");

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
                var deviceId = e.TryGetProperty("deviceId", out var dv) ? dv.GetString() : null;
                var ip = e.TryGetProperty("ip", out var ipj) ? ipj.GetString() : null;
                var merchantId = e.TryGetProperty("merchantId", out var mj) ? mj.GetString() : null;
                var ts = e.TryGetProperty("ts", out var tj) ? tj.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                using var session = _neo.AsyncSession(o => o.WithDatabase("neo4j"));
                var cy = @"
MERGE (t:Tenant {id:$tenant})
MERGE (a:Account {id:$src})-[:BELONGS_TO]->(t)
MERGE (b:Account {id:$dst})-[:BELONGS_TO]->(t)
FOREACH (_ IN CASE WHEN $deviceId IS NULL THEN [] ELSE [1] END |
  MERGE (d:Device {id:$deviceId})
  MERGE (a)-[:USES_DEVICE]->(d)
)
FOREACH (_ IN CASE WHEN $ip IS NULL THEN [] ELSE [1] END |
  MERGE (i:Ip {id:$ip})
  MERGE (a)-[:USES_IP {ts:$ts}]->(i)
)
FOREACH (_ IN CASE WHEN $merchantId IS NULL THEN [] ELSE [1] END |
  MERGE (m:Merchant {id:$merchantId})-[:BELONGS_TO]->(t)
  MERGE (a)-[:PAYS_TO {ts:$ts}]->(m)
)";
                session.RunAsync(cy, new { tenant, src, dst, deviceId, ip, merchantId, ts })
                    .GetAwaiter().GetResult().ConsumeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex) { _log.LogWarning(ex, "Risk context ingest failed"); }
        }
    }
}
