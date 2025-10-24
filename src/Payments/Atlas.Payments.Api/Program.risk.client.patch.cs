using Atlas.Contracts.Risk.V1;
using Confluent.Kafka;
using Grpc.Net.Client;

var producerCfg = new ProducerConfig {
  BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092",
  Acks = Acks.All, EnableIdempotence = true, CompressionType = CompressionType.Lz4, LingerMs = 5, BatchSize = 128*1024
};

builder.Services.AddSingleton(sp =>
{
    var addr = sp.GetRequiredService<IConfiguration>()["Services:RiskGrpc"] ?? "http://riskgraph:5401";
    return new RiskService.RiskServiceClient(GrpcChannel.ForAddress(addr));
});
builder.Services.AddSingleton(_ => new ProducerBuilder<string,string>(producerCfg).Build());

app.MapPost("/payments/transfers/with-risk", async (
    HttpRequest http,
    RiskService.RiskServiceClient risk,
    IProducer<string,string> prod,
    TransferRequest req,
    CancellationToken ct) =>
{
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    var deviceId = http.Headers["X-Device-Id"].FirstOrDefault();
    var ip = http.Headers["X-Ip"].FirstOrDefault();
    var merchantId = http.Headers["X-Merchant-Id"].FirstOrDefault();
    var customerId = http.Headers["X-Customer-Id"].FirstOrDefault();

    // 1) Score
    var score = await risk.ScoreAsync(new ScoreRequest {
        TenantId = tenant, SourceAccountId = req.SourceAccountId, DestinationAccountId = req.DestinationAccountId,
        Minor = req.Minor, Currency = req.Currency, DeviceId = deviceId ?? "", Ip = ip ?? "",
        CustomerId = customerId ?? "", MerchantId = merchantId ?? ""
    }, cancellationToken: ct);

    // 2) Emit context event (asynchronously) so graph has observations even if txn later rejected
    _ = prod.ProduceAsync("risk-events", new Message<string,string>{
        Key = req.SourceAccountId,
        Value = System.Text.Json.JsonSerializer.Serialize(new {
            tenant, source = req.SourceAccountId, dest = req.DestinationAccountId,
            minor = req.Minor, currency = req.Currency,
            deviceId, ip, merchantId, customerId, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        })
    }, ct);

    if (score.Decision == "BLOCK") return Results.Problem($"Blocked by risk engine: {score.Reason}", statusCode: 403);
    if (score.Decision == "REVIEW") http.HttpContext.Response.Headers["X-Risk-Review"] = "true";

    // (You may call the fast ledger path here to actually debit/credit.)
    return Results.Ok(new { decision = score.Decision, risk = score.RiskScore, reason = score.Reason });
});
