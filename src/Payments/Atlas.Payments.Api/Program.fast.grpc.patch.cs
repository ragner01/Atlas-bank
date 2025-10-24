using Atlas.Contracts.Ledger.V1;
using Grpc.Net.Client;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

var highThroughputProducer = new ProducerConfig {
  Acks = Acks.All,
  EnableIdempotence = true,
  LingerMs = 2,
  BatchSize = 128 * 1024,
  CompressionType = CompressionType.Lz4
};

builder.Services.AddSingleton(sp =>
{
    var addr = sp.GetRequiredService<IConfiguration>()["Services:LedgerGrpc"] ?? "http://ledgerapi:5181";
    return new LedgerService.LedgerServiceClient(GrpcChannel.ForAddress(addr));
});
builder.Services.AddSingleton(_ => new ProducerBuilder<string, string>(highThroughputProducer).Build());

var app = builder.Build();

app.MapPost("/payments/transfers/fast", async (
    HttpRequest http,
    LedgerService.LedgerServiceClient ledger,
    TransferRequest req,
    CancellationToken ct) =>
{
    var key = http.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    var res = await ledger.PostEntryAsync(new PostEntryRequest {
        SourceAccountId = req.SourceAccountId,
        DestinationAccountId = req.DestinationAccountId,
        Amount = new Money { Minor = req.Minor, Currency = req.Currency, Scale = 2 },
        Narration = req.Narration, TenantId = "tnt_demo"
    }, cancellationToken: ct);
    return Results.Accepted(value: new { status = res.Status, entryId = res.EntryId });
});

app.Run();
