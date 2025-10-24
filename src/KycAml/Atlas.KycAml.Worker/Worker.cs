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
    private readonly IFeatureClient _features;

    public Worker(ILogger<Worker> log, IRiskRuleEngine eng, IConfiguration cfg, CasesDbContext db, IFeatureClient features)
    {
        _log = log; _engine = eng; _features = features;
        var yamlPath = cfg["Rules:Path"] ?? "config/rules/aml-rules.phase6.yaml";
        _rules = RiskRuleEngine.LoadFromYaml(File.ReadAllText(yamlPath));

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Loop(stoppingToken);

    private async Task Loop(CancellationToken ct)
    {
        var bucket = new List<long>(); var lastFlush = DateTimeOffset.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            var cr = _consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (cr is null) { continue; }

            var messageId = cr.Message.Key ?? Guid.NewGuid().ToString();
            if (await _inbox.HasProcessedAsync("aml-worker", messageId, ct)) continue;

            var (minor, currency, source, dest, tenant) = ParseEvent(cr.Message.Value);
            bucket.Add(minor);

            // Subject selection based on rules config (default: source)
            var subject = (_rules.Features?.SubjectSelector?.ToLowerInvariant()) switch
            {
                "dest" => dest,
                _ => source
            };

            (bucket, lastFlush) = await MaybeEvalWithFeatures(bucket, lastFlush, ct, tenant, subject, currency);
            await _inbox.MarkProcessedAsync("aml-worker", messageId, ct);
        }
    }

    private async Task<(List<long> bucket, DateTimeOffset lastFlush)> MaybeEvalWithFeatures(List<long> bucket, DateTimeOffset lastFlush, CancellationToken ct, string tenant, string subject, string currency = "NGN")
    {
        if ((DateTimeOffset.UtcNow - lastFlush).TotalSeconds < 5) return (bucket, lastFlush);
        var local = _engine.EvaluateLocal(_rules, bucket, currency);
        var feature = await _engine.EvaluateWithFeaturesAsync(_rules, tenant, subject, currency, _features, ct);
        var hit = local.hit || feature.hit;
        var action = feature.hit ? feature.action : local.action;
        var reason = feature.hit ? feature.reason : local.reason;

        if (hit)
        {
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("CASES_API") ?? "http://kycamlapi:5201") };
                var payload = new AmlCase { TenantId = tenant, CustomerId = "cust_demo", Title = $"{action} AML velocity", Description = reason };
                await http.PostAsJsonAsync("/aml/cases", payload, ct);
                _log.LogWarning("AML Case created via features: {Title} {Reason}", payload.Title, reason);
            }
            catch (Exception ex) { _log.LogError(ex, "Failed to emit AML case"); }
        }
        bucket.Clear();
        return (bucket, DateTimeOffset.UtcNow);
    }

    private static (long minor, string currency, string source, string dest, string tenant) ParseEvent(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        long minor = root.GetProperty("minor").GetInt64();
        string ccy = root.GetProperty("currency").GetString() ?? "NGN";
        string source = root.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
        string dest = root.TryGetProperty("dest", out var d) ? d.GetString() ?? "" : "";
        string tenant = root.TryGetProperty("tenant", out var t) ? t.GetString() ?? "tnt_demo" : "tnt_demo";
        return (minor, ccy, source, dest, tenant);
    }
}