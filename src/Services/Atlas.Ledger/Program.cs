using Microsoft.EntityFrameworkCore;
using Atlas.Ledger.Domain;
using Atlas.Ledger.App;
using Npgsql;
using Atlas.Common.ValueObjects;
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure database
var cs = builder.Configuration.GetConnectionString("Ledger") ?? 
         Environment.GetEnvironmentVariable("LEDGER_DB") ?? 
         "Host=atlas-postgres;Port=5432;Database=atlas_bank;Username=atlas;Password=atlas123";

builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs));
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

builder.Services.AddSingleton<Atlas.Ledger.App.FastTransferHandler>(sp =>
{
    var ds = sp.GetRequiredService<NpgsqlDataSource>();
    var logger = sp.GetRequiredService<ILogger<Atlas.Ledger.App.FastTransferHandler>>();
    return new Atlas.Ledger.App.FastTransferHandler(ds, logger);
});

// Add Redis multiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? 
                          Environment.GetEnvironmentVariable("REDIS") ?? 
                          "atlas-redis:6379";
    
    var configuration = ConfigurationOptions.Parse(connectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    configuration.AsyncTimeout = 5000;
    
    return ConnectionMultiplexer.Connect(configuration);
});

// Add tenant context
builder.Services.AddScoped<ITenantContext>(sp => 
{
    var context = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var tenantHeader = context?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    return TenantContext.FromHeader(tenantHeader);
});
builder.Services.AddHttpContextAccessor();

// Add gRPC services
builder.Services.AddGrpc();

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(cs, name: "postgresql")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "atlas-redis:6379", name: "redis");

var app = builder.Build();

// Map health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description
            }),
            duration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// Map gRPC services
app.MapGrpcService<Atlas.Ledger.Api.Grpc.LedgerGrpcService>();

// Map API endpoints
app.MapPost("/ledger/entries", async (HttpRequest http, PostJournalEntryCommand cmd, PostJournalEntryHandler handler, CancellationToken ct) =>
{
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    var result = await handler.HandleAsync(cmd with { TenantId = tenant }, ct);
    return result.IsSuccess ? Results.Ok(new SuccessResponse<object>(result.Value)) : Results.Problem(result.Error);
});

app.MapGet("/ledger/accounts/{id}", async (string id, ILedgerRepository repo, ITenantContext tenant, CancellationToken ct) =>
{
    var account = await repo.GetAsync(id, ct);
    return account is not null ? Results.Ok(account) : Results.NotFound();
});

app.MapPost("/ledger/fast-transfer", async (HttpRequest http, Atlas.Ledger.App.FastTransferHandler handler, CancellationToken ct) =>
{
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    
    // Read JSON manually to debug
    var body = await http.ReadFromJsonAsync<FastTransferRequest>();
    Console.WriteLine($"DEBUG: Received body: {System.Text.Json.JsonSerializer.Serialize(body)}");
    
    if (body == null)
    {
        return Results.BadRequest("Invalid request body");
    }
    
    try
    {
        var result = await handler.ExecuteAsync(body.IdempotencyKey, tenant, body.SourceAccountId, body.DestinationAccountId, body.Minor, body.Currency, body.Narration, ct);
        return Results.Ok(new { status = "Success", entryId = result });
    }
    catch (ArgumentException ex) when (ex.Message.Contains("Currency"))
    {
        return Results.BadRequest(new { error = "Invalid currency code", details = ex.Message });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DEBUG: Exception caught: {ex.Message}");
        return Results.Problem($"Transfer failed: {ex.Message}", statusCode: 500);
    }
});

// Add hedged read endpoint for balance queries
app.MapGet("/ledger/accounts/{id}/balance", async (string id, ILedgerRepository repo, ITenantContext tenant, IConnectionMultiplexer redis, CancellationToken ct) =>
{
    // Try Redis cache first
    var db = redis.GetDatabase();
    var cachedBalance = await db.StringGetAsync($"balance:{tenant.CurrentTenant}:{id}");
    
    if (cachedBalance.HasValue)
    {
        return Results.Ok(new { balance = cachedBalance.ToString(), source = "cache" });
    }
    
    // Fallback to database
    var account = await repo.GetAsync(id, ct);
    if (account is null)
    {
        return Results.NotFound();
    }
    
    // Cache the result for 5 minutes
    await db.StringSetAsync($"balance:{tenant.CurrentTenant}:{id}", account.Balance.LedgerCents.ToString(), TimeSpan.FromMinutes(5));
    
    return Results.Ok(new { balance = account.Balance.LedgerCents, source = "database" });
});

app.Run();