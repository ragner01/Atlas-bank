using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy;
using Atlas.ApiGateway.Middleware;
using Atlas.ApiGateway.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure and validate options
builder.Services.Configure<ApiGatewayOptions>(builder.Configuration.GetSection(ApiGatewayOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<ApiGatewayOptions>, ApiGatewayOptionsValidator>();

// Enable Kestrel performance configuration
var kestrelConfig = new ConfigurationBuilder()
    .AddJsonFile("kestrel.fast.json", optional: true)
    .Build();
builder.WebHost.UseKestrel().UseConfiguration(kestrelConfig);

// Add Redis for rate limiting - NO HARDCODED FALLBACKS
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? 
                               Environment.GetEnvironmentVariable("REDIS");
    
    if (string.IsNullOrEmpty(redisConnectionString))
    {
        throw new InvalidOperationException("Redis connection string must be configured via ConnectionStrings:Redis or REDIS environment variable");
    }
    
    options.Configuration = redisConnectionString;
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = int.Parse(builder.Configuration["RateLimiting:PermitPerMinute"] ?? "60"), // Conservative default
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    options.AddPolicy("Strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
});

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authOptions = builder.Configuration.GetSection("ApiGateway:Authentication");
        options.Authority = authOptions["Authority"];
        options.Audience = authOptions["Audience"];
        options.RequireHttpsMetadata = bool.Parse(authOptions["RequireHttpsMetadata"] ?? "true");
        options.TokenValidationParameters.ValidTypes = new[] { "at+jwt", "JWT" };
        
        // Enhanced security options
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ScopeAccountsRead", policy => 
        policy.RequireClaim("scope", "accounts.read"));
    options.AddPolicy("ScopePaymentsWrite", policy => 
        policy.RequireClaim("scope", "payments.write"));
    options.AddPolicy("ScopeAmlRead", policy => 
        policy.RequireClaim("scope", "aml.read"));
    options.AddPolicy("ScopeLoansRead", policy => 
        policy.RequireClaim("scope", "loans.read"));
    options.AddPolicy("ScopeLoansWrite", policy => 
        policy.RequireClaim("scope", "loans.write"));
});

// Add reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

// Add CORS - RESTRICTIVE CONFIGURATION
builder.Services.AddCors(options =>
{
    options.AddPolicy("AtlasBank", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("ApiGateway:Cors:AllowedOrigins").Get<string[]>();
        
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("CORS allowed origins must be configured via ApiGateway:Cors:AllowedOrigins");
        }
        
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS") // Restrict methods
              .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id", "X-Request-ID") // Restrict headers
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Cache preflight requests
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Add global exception handling
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    
    // Content Security Policy
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none';";
    
    await next();
});

// Remove server header
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Server");
    await next();
});

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AtlasBank");

// Enable rate limiting
app.UseRateLimiter();

// Authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
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
                duration = entry.Value.Duration.TotalMilliseconds
            }),
            duration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Error handling endpoint
app.MapGet("/error", () => Results.Problem(
    title: "An error occurred",
    detail: "Please contact support if this problem persists",
    statusCode: 500
));

// Reverse proxy
app.MapReverseProxy();

// Graceful shutdown
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("API Gateway is shutting down gracefully...");
});

app.Run();