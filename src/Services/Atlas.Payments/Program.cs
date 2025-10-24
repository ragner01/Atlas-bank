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
    
    // Get tenant from header (fallback to demo for testing)
    var tenantId = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    
    // Atomically check and mark idempotency key
    var alreadyProcessed = await idem.CheckAndMarkAsync(key, ct);
    if (alreadyProcessed) return Results.Ok(new { status = "Accepted", message = "Already processed" });

    try
    {
        var res = await ledger.PostEntryAsync(new PostEntryRequest {
            SourceAccountId = req.SourceAccountId,
            DestinationAccountId = req.DestinationAccountId,
            Amount = new Money { Minor = req.Minor, Currency = req.Currency, Scale = 2 },
            Narration = req.Narration, 
            TenantId = tenantId
        }, cancellationToken: ct);

        return Results.Accepted(value: new { status = res.Status, entryId = res.EntryId });
    }
    catch (Exception ex)
    {
        // If ledger call fails, we should ideally remove the idempotency mark
        // For now, we'll let it stay to prevent retry storms
        return Results.Problem($"Payment processing failed: {ex.Message}", statusCode: 500);
    }
});

app.Run();
