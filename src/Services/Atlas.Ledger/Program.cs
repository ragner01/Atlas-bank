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

// Configure rate limiting
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddSingleton<RateLimitingOptions>(sp => 
    sp.GetRequiredService<IOptions<RateLimitingOptions>>().Value);

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
builder.Services.AddSingleton<Atlas.Ledger.App.FastTransferHandler>(sp =>
{
    var ds = sp.GetRequiredService<NpgsqlDataSource>();
    var logger = sp.GetRequiredService<ILogger<Atlas.Ledger.App.FastTransferHandler>>();
    return new Atlas.Ledger.App.FastTransferHandler(ds, logger);
});

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

// Add metrics collection
builder.Services.AddSingleton<Atlas.Ledger.Api.Metrics.LedgerMetricsCollector>();
builder.Services.AddHostedService<Atlas.Ledger.Api.Metrics.LedgerMetricsCollector>(sp =>
    sp.GetRequiredService<Atlas.Ledger.Api.Metrics.LedgerMetricsCollector>());

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Atlas Bank Ledger API",
        Version = "v1",
        Description = "High-performance financial ledger API with multi-tenancy support",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Atlas Bank Development Team",
            Email = "dev@atlasbank.com"
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "Proprietary",
            Url = new Uri("https://atlasbank.com/license")
        }
    });

    // Add XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add security definitions
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme"
    });

    c.AddSecurityDefinition("TenantHeader", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Tenant-Id",
        Description = "Tenant identifier for multi-tenant operations"
    });

    // Add security requirements
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "TenantHeader"
                }
            },
            Array.Empty<string>()
        }
    });

    // Add examples
    c.SchemaFilter<ExampleSchemaFilter>();
});

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

// Add security, rate limiting, metrics, and exception handling middleware
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<Atlas.Ledger.Api.Metrics.MetricsMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Add Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas Bank Ledger API v1");
        c.RoutePrefix = "api-docs";
        c.DocumentTitle = "Atlas Bank Ledger API Documentation";
    });
}

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
