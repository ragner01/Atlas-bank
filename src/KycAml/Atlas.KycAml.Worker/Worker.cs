using Confluent.Kafka;
using Atlas.Risk.Domain.Rules;

namespace Atlas.KycAml.Worker;

public sealed class Worker : BackgroundService 
{
    private readonly ILogger<Worker> _log; 
    private readonly IRiskRuleEngine _engine;
    private readonly RuleSet _rules; 
    private readonly IConsumer<string,string> _consumer;
    
    public Worker(ILogger<Worker> log, IRiskRuleEngine eng, IConfiguration cfg) 
    {
        _log = log; 
        _engine = eng;
        
        var yaml = File.ReadAllText(cfg["Rules:Path"] ?? "config/rules/aml-rules.yaml");
        _rules = RiskRuleEngine.LoadFromYaml(yaml);
        
        var conf = new ConsumerConfig 
        { 
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092", 
            GroupId = "aml-worker", 
            AutoOffsetReset = AutoOffsetReset.Earliest 
        };
        
        _consumer = new ConsumerBuilder<string,string>(conf).Build();
        _consumer.Subscribe(cfg["Topics:LedgerEvents"] ?? "ledger-events");
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => Loop(stoppingToken));
    
    private void Loop(CancellationToken ct) 
    {
        var bucket = new List<long>(); 
        var lastFlush = DateTimeOffset.UtcNow;
        
        while (!ct.IsCancellationRequested) 
        {
            var cr = _consumer.Consume(TimeSpan.FromMilliseconds(250));
            if (cr is null) 
            { 
                MaybeEval(ref bucket, ref lastFlush); 
                continue; 
            }
            
            var (minor, currency) = ParseAmounts(cr.Message.Value);
            bucket.Add(minor);
            MaybeEval(ref bucket, ref lastFlush, currency);
        }
    }
    
    private void MaybeEval(ref List<long> bucket, ref DateTimeOffset lastFlush, string currency = "NGN") 
    {
        if ((DateTimeOffset.UtcNow - lastFlush).TotalSeconds < 5) return;
        
        var (hit, action, reason) = _engine.Evaluate(_rules, bucket, currency);
        if (hit) _log.LogWarning("AML alert: {Action} {Reason}", action, reason);
        
        bucket.Clear(); 
        lastFlush = DateTimeOffset.UtcNow;
    }
    
    private static (long,string) ParseAmounts(string json) 
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var minor = doc.RootElement.GetProperty("minor").GetInt64();
        var ccy = doc.RootElement.GetProperty("currency").GetString() ?? "NGN";
        return (minor, ccy);
    }
}
