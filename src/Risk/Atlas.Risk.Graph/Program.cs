using Atlas.Risk.Graph;
using Atlas.Risk.Graph.Runtime;
using Grpc.Core;
using StackExchange.Redis;
using Neo4j.Driver;

var b = WebApplication.CreateBuilder(args);

// Neo4j driver
b.Services.AddSingleton<IDriver>(sp =>
{
    var uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://neo4j:7687";
    var user = Environment.GetEnvironmentVariable("NEO4J_USER") ?? "neo4j";
    var password = Environment.GetEnvironmentVariable("NEO4J_PASS") ?? "neo4j";
    return GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
});

// Redis (blacklists / threat intel, optional)
b.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));

// Kafka ingestors
b.Services.AddHostedService<LedgerGraphIngestor>();
b.Services.AddHostedService<RiskContextIngestor>(); // NEW

// Hybrid scorer (ONNX + rules)
b.Services.AddSingleton<IScorer>(sp =>
{
    var modelPath = Environment.GetEnvironmentVariable("RISK_ONNX") ?? "models/fraud-mini.onnx";
    var redis = sp.GetRequiredService<IConnectionMultiplexer>();
    return new HybridScorer(modelPath, redis);
});

b.Services.AddGrpc();
var app = b.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGrpcService<RiskGrpcService>();
app.Run();
