using Confluent.Kafka;
using Atlas.Risk.Domain.Rules;
using Atlas.KycAml.Domain;
using Atlas.Messaging;
using System.Net.Http.Json;

namespace Atlas.KycAml.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly IRiskRuleEngine _engine;
    private readonly RuleSet _rules;
    private readonly IConsumer<string, string> _consumer;
    private readonly IInbox _inbox;

    public Worker(ILogger<Worker> log, IRiskRuleEngine eng, IConfiguration cfg, CasesDbContext db)
    {
        _log = log; _engine = eng;
        var yaml = File.ReadAllText(cfg["Rules:Path"] ?? "config/rules/aml-rules.yaml");
        _rules = RiskRuleEngine.LoadFromYaml(yaml);

        var conf = new ConsumerConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
            GroupId = "aml-worker",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        _consumer = new ConsumerBuilder<string, string>(conf).Build();
        _consumer.Subscribe(cfg["Topics:LedgerEvents"] ?? "ledger-events");

        _inbox = new EfInbox(db);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));

    private void Loop(CancellationToken ct)
    {
        var bucket = new List<long>(); var lastFlush = DateTimeOffset.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            var cr = _consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (cr is null) { MaybeEval(ref bucket, ref lastFlush, ct); continue; }

            var messageId = cr.Message.Key ?? Guid.NewGuid().ToString();
            if (_inbox.HasProcessedAsync("aml-worker", messageId, ct).Result) continue;

            var (minor, currency) = ParseAmounts(cr.Message.Value);
            bucket.Add(minor);

            MaybeEval(ref bucket, ref lastFlush, ct, currency);
            _inbox.MarkProcessedAsync("aml-worker", messageId, ct).GetAwaiter().GetResult();
        }
    }

    private void MaybeEval(ref List<long> bucket, ref DateTimeOffset lastFlush, CancellationToken ct, string currency = "NGN")
    {
        if ((DateTimeOffset.UtcNow - lastFlush).TotalSeconds < 5) return;
        var (hit, action, reason) = _engine.Evaluate(_rules, bucket, currency);
        if (hit)
        {
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("CASES_API") ?? "http://kycamlapi:5201") };
                var payload = new AmlCase { TenantId = "tnt_demo", CustomerId = "cust_demo", Title = $"{action} AML velocity", Description = reason };
                http.PostAsJsonAsync("/aml/cases", payload, ct).GetAwaiter().GetResult();
                _log.LogWarning("AML Case created: {Title}", payload.Title);
            }
            catch (Exception ex) { _log.LogError(ex, "Failed to emit AML case"); }
        }
        bucket.Clear(); lastFlush = DateTimeOffset.UtcNow;
    }

    private static (long, string) ParseAmounts(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var minor = doc.RootElement.GetProperty("minor").GetInt64();
        var ccy = doc.RootElement.GetProperty("currency").GetString() ?? "NGN";
        return (minor, ccy);
    }
}