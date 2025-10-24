using Microsoft.EntityFrameworkCore;
using Atlas.Ledger.Domain;
using Atlas.Ledger.App;
using Npgsql;
using Atlas.Common.ValueObjects;
using Atlas.Messaging;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("Ledger");
builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs));
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddScoped<ILedgerRepository, EfLedgerRepository>();
builder.Services.AddTransient<PostJournalEntryHandler>();

// Add tenant context
builder.Services.AddScoped<ITenantContext>(sp => 
{
    var context = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var tenantHeader = context?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    return TenantContext.FromHeader(tenantHeader);
});
builder.Services.AddHttpContextAccessor();

// Add messaging services
builder.Services.AddSingleton<IEventPublisher>(sp => 
{
    var publisher = new Atlas.Messaging.KafkaPublisher(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092");
    // Register for disposal when the service provider is disposed
    var lifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() => publisher.DisposeAsync().AsTask().Wait());
    return publisher;
});
builder.Services.AddSingleton<IOutboxStore, Atlas.Messaging.InMemoryOutboxStore>();
builder.Services.AddHostedService<Atlas.Messaging.OutboxDispatcher>();

// Add gRPC services
builder.Services.AddGrpc();

var app = builder.Build();

// Map gRPC service
app.MapGrpcService<Atlas.Ledger.Api.Grpc.LedgerGrpcService>();

app.MapGet("/health", () => Results.Ok());
app.MapGet("/", () => "Ledger gRPC running");

app.MapPost("/ledger/entries", async (PostRequest req, PostJournalEntryHandler handler, LedgerDbContext db, CancellationToken ct) =>
{
    return await ExecuteSerializableAsync(db, async () =>
    {
        // Convert minor units to decimal value (minor units / 10^scale)
        var decimalValue = req.Minor / 100m; // Assuming scale of 2 for most currencies
        var debit = (new AccountId(req.SourceAccountId), new Money(decimalValue, Currency.FromCode(req.Currency), 2));
        var credit = (new AccountId(req.DestinationAccountId), new Money(decimalValue, Currency.FromCode(req.Currency), 2));
        var entry = await handler.HandleAsync(new(req.Narration, new[] { debit }, new[] { credit }), ct);
        return Results.Accepted($"/ledger/entries/{entry.Id.Value}", new { id = entry.Id.Value, status = "Pending" });
    });
});

static async Task<IResult> ExecuteSerializableAsync(LedgerDbContext db, Func<Task<IResult>> action, int maxRetries = 3)
{
    for (var i = 0; i < maxRetries; i++)
    {
        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try { var res = await action(); await tx.CommitAsync(); return res; }
        catch (PostgresException ex) when (ex.SqlState == "40001") { await tx.RollbackAsync(); if (i == maxRetries - 1) throw; }
    }
    throw new InvalidOperationException("Unreachable");
}

app.Run();
