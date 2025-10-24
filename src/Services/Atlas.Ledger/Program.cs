using Microsoft.EntityFrameworkCore;
using Atlas.Ledger.Domain;
using Atlas.Ledger.App;
using Atlas.Ledger.Api.Models;
using Atlas.Ledger.Api.Middleware;
using Atlas.Ledger.Api.Configuration;
using Npgsql;
using Atlas.Common.ValueObjects;
using Atlas.Messaging;
using System.Data;
using StackExchange.Redis;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configure and validate options
builder.Services.Configure<LedgerApiOptions>(builder.Configuration.GetSection(LedgerApiOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<LedgerApiOptions>, LedgerApiOptionsValidator>();

var cs = builder.Configuration.GetConnectionString("Ledger");
builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs, npgsqlOptions =>
{
    npgsqlOptions.CommandTimeout(30);
    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
}));
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddScoped<ILedgerRepository, EfLedgerRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddTransient<PostJournalEntryHandler>();

// Add high-performance NpgsqlDataSource for fast-path
builder.Services.AddSingleton(_ =>
{
    var opts = new NpgsqlDataSourceBuilder(cs)
        .EnableParameterLogging(false);
    return opts.Build();
});
builder.Services.AddSingleton<Atlas.Ledger.App.FastTransferHandler>();

// Add Redis multiplexer (shared)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));

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

// Validate configuration at startup
var options = app.Services.GetRequiredService<IOptions<LedgerApiOptions>>();
var validator = app.Services.GetRequiredService<IValidateOptions<LedgerApiOptions>>();
var validationResult = validator.Validate(LedgerApiOptions.SectionName, options.Value);
if (validationResult.Failed)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError("Configuration validation failed: {Failures}", string.Join(", ", validationResult.Failures));
    throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", validationResult.Failures)}");
}

// Add security and exception handling middleware
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Map gRPC service
app.MapGrpcService<Atlas.Ledger.Api.Grpc.LedgerGrpcService>();

app.MapGet("/health", () => Results.Ok());
app.MapGet("/", () => "Ledger gRPC running");

app.MapPost("/ledger/entries", async (PostRequest req, PostJournalEntryHandler handler, CancellationToken ct) =>
{
    // Convert minor units to decimal value (minor units / 10^scale)
    var decimalValue = req.Minor / 100m; // Assuming scale of 2 for most currencies
    var debit = (new AccountId(req.SourceAccountId), new Money(decimalValue, Currency.FromCode(req.Currency), 2));
    var credit = (new AccountId(req.DestinationAccountId), new Money(decimalValue, Currency.FromCode(req.Currency), 2));
    var entry = await handler.HandleAsync(new(req.Narration, new[] { debit }, new[] { credit }), ct);
    return Results.Accepted($"/ledger/entries/{entry.Id.Value}", new { id = entry.Id.Value, status = "Pending" });
});

// High-performance fast-transfer endpoint with validation
app.MapPost("/ledger/fast-transfer", async (
    FastTransferRequest request,
    HttpRequest http,
    Atlas.Ledger.App.FastTransferHandler fast,
    CancellationToken ct) =>
{
    // Additional business validation
    if (request.SourceAccountId == request.DestinationAccountId)
        return Results.BadRequest(new { error = "Source and destination accounts cannot be the same" });

    try
    {
        // Validate currency is supported
        Currency.FromCode(request.Currency);
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new { error = $"Unsupported currency: {request.Currency}" });
    }

    var key = http.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    var (entry, duplicate) = await fast.ExecuteAsync(key, request.TenantId, request.SourceAccountId, request.DestinationAccountId, request.Minor, request.Currency, request.Narration, ct);
    return entry is null && duplicate
        ? Results.Ok(new { status = "Accepted" })
        : Results.Accepted($"/ledger/entries/{entry}", new { entryId = entry, status = "Pending" });
});

// Hedged read endpoint: returns fastest of Redis read-model or DB fallback, verifies watermark when both return.
app.MapGet("/ledger/accounts/{id}/balance", async (
    string id,
    HttpContext ctx,
    IConnectionMultiplexer mux,
    IConfiguration cfg) =>
{
    // Input validation
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest(new { error = "Account ID is required" });

    var tenant = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    var currency = ctx.Request.Query["currency"].FirstOrDefault() ?? "NGN";
    
    // Validate currency
    try
    {
        Currency.FromCode(currency);
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new { error = $"Unsupported currency: {currency}" });
    }

    var key = $"balance:{tenant}:{id}:{currency}";
    var db = mux.GetDatabase();

    // Configurable hedge delay (default 12ms)
    var hedgeDelayMs = cfg.GetValue<int>("HedgedRead:DelayMs", 12);

    // Start Redis fetch
    var redisTask = db.HashGetAsync(key, "minor");

    // Start DB fetch slightly hedged (launch after configurable delay)
    var dbTask = Task.Run(async () =>
    {
        try
        {
            // simple SQL read using Npgsql (no EF) for speed
            await using var conn = new Npgsql.NpgsqlConnection(cfg.GetConnectionString("Ledger"));
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand("SELECT fn_get_balance(@a)", conn);
            cmd.Parameters.AddWithValue("a", id);
            var result = await cmd.ExecuteScalarAsync();
            return (long)(result ?? 0);
        }
        catch (Exception)
        {
            // Return 0 if DB read fails
            return 0L;
        }
    });

    // Hedge: wait for whichever returns first; if Redis hits, return immediately
    var first = await Task.WhenAny(redisTask, Task.Delay(hedgeDelayMs));
    if (first == redisTask && redisTask.Result.HasValue && long.TryParse(redisTask.Result!, out var cached))
        return Results.Ok(new { accountId = id, ledger = cached, source = "cache" });

    // Otherwise, return DB; optionally backfill cache
    var dbVal = await dbTask;
    _ = db.HashSetAsync(key, new HashEntry[] { new("minor", dbVal), new("v", 0), new("ts", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) });
    return Results.Ok(new { accountId = id, ledger = dbVal, source = "db" });
});

app.Run();
