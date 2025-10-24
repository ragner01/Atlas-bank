using Atlas.Ledger.App;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// High-performance NpgsqlDataSource with pooling + prepared statements
builder.Services.AddSingleton(_ =>
{
    var cs = builder.Configuration.GetConnectionString("Ledger")!;
    var opts = new NpgsqlDataSourceBuilder(cs)
        .UseNodaTime()
        .EnableParameterLogging(false);
    var ds = opts.Build();
    return ds;
});
builder.Services.AddSingleton<FastTransferHandler>();

var app = builder.Build();

// gRPC stays as is; add a dedicated fast HTTP endpoint (or wire via Payments)
app.MapPost("/ledger/fast-transfer", async (
    HttpRequest http,
    FastTransferHandler fast,
    string sourceAccountId,
    string destinationAccountId,
    long minor,
    string currency,
    string narration,
    string tenantId,
    CancellationToken ct) =>
{
    var key = http.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    var (entry, duplicate) = await fast.ExecuteAsync(key, tenantId, sourceAccountId, destinationAccountId, minor, currency, narration, ct);
    return entry is null && duplicate
        ? Results.Ok(new { status = "Accepted" })
        : Results.Accepted($"/ledger/entries/{entry}", new { entryId = entry, status = "Pending" });
});

app.Run();
