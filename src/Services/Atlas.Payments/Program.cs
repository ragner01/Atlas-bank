using Atlas.Payments.App;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Contracts.Ledger.V1;
using Grpc.Net.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Payments")));
builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

// Add gRPC client
builder.Services.AddSingleton(sp => {
    var cfg = sp.GetRequiredService<IConfiguration>();
    var addr = cfg["Services:LedgerGrpc"] ?? "http://ledgerapi:5181";
    var channel = GrpcChannel.ForAddress(addr);
    return new LedgerService.LedgerServiceClient(channel);
});

var app = builder.Build();

app.MapPost("/payments/transfers", async (HttpRequest http, TransferRequest req, IIdempotencyStore idem, LedgerService.LedgerServiceClient ledger, CancellationToken ct) => {
    var key = http.Headers["Idempotency-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) return Results.Problem("Missing Idempotency-Key", statusCode: 400);
    if (await idem.SeenAsync(key, ct)) return Results.Ok(new { status = "Accepted" });

    var res = await ledger.PostEntryAsync(new PostEntryRequest {
        SourceAccountId = req.SourceAccountId,
        DestinationAccountId = req.DestinationAccountId,
        Amount = new Money { Minor = req.Minor, Currency = req.Currency, Scale = 2 },
        Narration = req.Narration, 
        TenantId = "tnt_demo"
    }, cancellationToken: ct);

    await idem.MarkAsync(key, ct);
    return Results.Accepted(value: new { status = res.Status, entryId = res.EntryId });
});

app.Run();
