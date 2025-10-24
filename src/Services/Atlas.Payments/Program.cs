using Atlas.Payments.App;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Contracts.Ledger.V1;
using Grpc.Net.Client;
using System.Net.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.HttpOverrides;
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Atlas.Payments.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure and validate options
builder.Services.Configure<PaymentsApiOptions>(builder.Configuration.GetSection(PaymentsApiOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

// Add validators
builder.Services.AddSingleton<IValidateOptions<PaymentsApiOptions>, PaymentsApiOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
});

// Configure compression levels
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

// Configure CORS - RESTRICTIVE CONFIGURATION
builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>();
    
    if (corsOptions?.AllowedOrigins == null || corsOptions.AllowedOrigins.Length == 0)
    {
        throw new InvalidOperationException("CORS allowed origins must be configured via Cors:AllowedOrigins");
    }
    
    options.AddPolicy("AtlasBankPolicy", policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS") // Restrict methods
              .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id", "X-Request-ID") // Restrict headers
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Cache preflight requests
    });
});

// Configure database
var connectionString = builder.Configuration.GetConnectionString("Payments") ?? 
                     throw new InvalidOperationException("Payments connection string is required");
builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(connectionString, npgsqlOptions =>
{
    npgsqlOptions.CommandTimeout(builder.Configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30));
    npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: builder.Configuration.GetValue<int>("Database:MaxRetryCount", 3), 
        maxRetryDelay: TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 5)), 
        errorCodesToAdd: null);
}));

builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

// Add Redis multiplexer with proper configuration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisOptions = sp.GetRequiredService<RedisOptions>();
    
    var redisConnectionString = config.GetConnectionString("Redis") ?? 
                               Environment.GetEnvironmentVariable("REDIS") ?? 
                               throw new InvalidOperationException("Redis connection string is required");
    
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectTimeout = redisOptions.ConnectTimeoutSeconds;
    configuration.SyncTimeout = redisOptions.SyncTimeoutSeconds;
    configuration.AsyncTimeout = redisOptions.AsyncTimeoutSeconds;
    configuration.KeepAlive = redisOptions.KeepAliveSeconds;
    configuration.ReconnectRetryPolicy = new ExponentialRetry(redisOptions.ReconnectRetryDelayMs);
    
    return ConnectionMultiplexer.Connect(configuration);
});

// Add gRPC client with proper configuration
builder.Services.AddSingleton(sp => {
    var cfg = sp.GetRequiredService<IConfiguration>();
    var addr = cfg["Services:LedgerGrpc"] ?? 
               Environment.GetEnvironmentVariable("LEDGER_GRPC_URL") ?? 
               "http://docker-ledgerapi-1:5181";
    
    var channelOptions = new GrpcChannelOptions
    {
        MaxReceiveMessageSize = cfg.GetValue<int>("Grpc:MaxReceiveMessageSize", 4 * 1024 * 1024), // 4MB
        MaxSendMessageSize = cfg.GetValue<int>("Grpc:MaxSendMessageSize", 4 * 1024 * 1024), // 4MB
        Credentials = Grpc.Core.ChannelCredentials.Insecure
    };
    
    var channel = GrpcChannel.ForAddress(addr, channelOptions);
    return new LedgerService.LedgerServiceClient(channel);
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string is required"), name: "redis");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Atlas Bank Payments API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Add security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Add request correlation ID middleware
app.UseMiddleware<RequestCorrelationMiddleware>();

// Add global exception handling middleware
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Enable response compression
app.UseResponseCompression();

// Enable CORS
app.UseCors("AtlasBankPolicy");

// Add forwarded headers for reverse proxy scenarios
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

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

// New endpoint for CNP using card token (PAN-less). Payments stays OUTSIDE the CDE.
// Calls Vault (PCI zone) for authorization; only receives decision + auth refs.
app.MapPost("/payments/cnp/charge", async (
    HttpRequest http,
    IConfiguration cfg,
    LedgerService.LedgerServiceClient ledger,
    long amountMinor,
    string currency,
    string cardToken,
    string merchantId,
    string mcc,
    CancellationToken ct) =>
{
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    
    // 1) Call vault for auth (mTLS/intra-private network in prod)
    var vaultBaseUrl = cfg["Services:VaultBase"] ?? 
                      Environment.GetEnvironmentVariable("VAULT_BASE") ?? 
                      "http://docker-cardsvault-1:5600";
    
    using var client = new HttpClient { BaseAddress = new Uri(vaultBaseUrl) };
    var authRes = await client.PostAsJsonAsync("/vault/authorize", new { 
        CardToken = cardToken, 
        AmountMinor = amountMinor, 
        Currency = currency, 
        MerchantId = merchantId, 
        Mcc = mcc 
    }, ct);
    
    if (!authRes.IsSuccessStatusCode) 
        return Results.Problem(await authRes.Content.ReadAsStringAsync(ct), statusCode: (int)authRes.StatusCode);
    
    var payload = await authRes.Content.ReadFromJsonAsync<AuthDto>(cancellationToken: ct);
    if (payload is null || payload.approved == false) 
        return Results.Problem("declined", statusCode: 402);

    // 2) Post to ledger (fast path) — merchant settlement (customer → merchant)
    var res = await ledger.PostEntryAsync(new PostEntryRequest
    {
        SourceAccountId = $"card::{cardToken}", // logical card funding source or cardholder account
        DestinationAccountId = $"merchant::{merchantId}",
        Amount = new Money { Minor = amountMinor, Currency = currency, Scale = 2 },
        Narration = $"CNPAUTH {payload.auth_code}/{payload.rrn}",
        TenantId = tenant
    }, cancellationToken: ct);

    return Results.Accepted(value: new { 
        status = res.Status, 
        entryId = res.EntryId, 
        auth = payload.auth_code, 
        rrn = payload.rrn,
        network = payload.network,
        last4 = payload.last4
    });
});

app.Run();

// Configuration classes
public class PaymentsApiOptions
{
    public int MaxRequestSizeBytes { get; set; } = 1048576; // 1MB
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableDetailedErrors { get; set; } = false;
}

public class RedisOptions
{
    public int ConnectTimeoutSeconds { get; set; } = 5;
    public int SyncTimeoutSeconds { get; set; } = 5;
    public int AsyncTimeoutSeconds { get; set; } = 5;
    public int KeepAliveSeconds { get; set; } = 60;
    public int ReconnectRetryDelayMs { get; set; } = 1000;
}

public class JwtOptions
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 60;
}

public class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
    public int PreflightMaxAgeMinutes { get; set; } = 10;
}

record TransferRequest(string SourceAccountId, string DestinationAccountId, long Minor, string Currency, string Narration);
record AuthDto(bool approved, string auth_code, string rrn, string network, string last4);

// Middleware classes (same as Ledger API)
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        
        // Add Content Security Policy
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none';";
        
        // Add Strict-Transport-Security for HTTPS
        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }
}

public class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                           Activity.Current?.Id ?? 
                           Guid.NewGuid().ToString();
        
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        
        // Add to logging scope
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method,
            ["UserAgent"] = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown",
            ["ClientIP"] = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        });

        await _next(context);
    }
}

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred with correlation ID {CorrelationId}: {Message}", 
                correlationId, ex.Message);
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentNullException => new { error = "Required parameter is missing", details = exception.Message, correlationId },
            ArgumentException => new { error = "Invalid request parameters", details = exception.Message, correlationId },
            InvalidOperationException => new { error = "Invalid operation", details = exception.Message, correlationId },
            UnauthorizedAccessException => new { error = "Access denied", details = _environment.IsDevelopment() ? exception.Message : null, correlationId },
            NotImplementedException => new { error = "Feature not implemented", details = _environment.IsDevelopment() ? exception.Message : null, correlationId },
            TimeoutException => new { error = "Request timeout", details = _environment.IsDevelopment() ? exception.Message : null, correlationId },
            _ => new { error = "An internal server error occurred", details = _environment.IsDevelopment() ? exception.Message : null, correlationId }
        };

        context.Response.StatusCode = exception switch
        {
            ArgumentNullException or ArgumentException => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            NotImplementedException => (int)HttpStatusCode.NotImplemented,
            TimeoutException => (int)HttpStatusCode.RequestTimeout,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}