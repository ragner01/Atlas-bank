using Atlas.Limits;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;
using System.Globalization;
using Serilog;
using Serilog.Events;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Limits")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/limits-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Limits Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Redis connection
    b.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379";
        Log.Information("Connecting to Redis at {RedisConnection}", redisConnectionString);
        return ConnectionMultiplexer.Connect(redisConnectionString);
    });

    b.Services.AddEndpointsApiExplorer();
    b.Services.AddOpenApi();

    var app = b.Build();
    app.MapOpenApi();

    // Request/Response logging middleware
    app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                           Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Log request context
        Log.Information("Request context: CorrelationId={CorrelationId}, RequestPath={RequestPath}, RequestMethod={RequestMethod}, UserAgent={UserAgent}, RemoteIp={RemoteIp}", 
            correlationId, context.Request.Path, context.Request.Method, 
            context.Request.Headers.UserAgent.ToString(), 
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

        await next();

        stopwatch.Stop();
        Log.Information("Request completed: {Method} {Path} {StatusCode} {ElapsedMs}ms", 
            context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    });

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow, service = "Atlas.Limits" }))
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithSummary("Health check endpoint")
        .WithDescription("Returns the health status of the Limits service");

    // GET /limits/policy -> current policy
    app.MapGet("/limits/policy", async (IConnectionMultiplexer mux) =>
    {
        Log.Information("Policy retrieval requested");
        
        try
        {
            var db = mux.GetDatabase();
            var policyStr = await db.StringGetAsync("limits:policy");
            var policy = policyStr.IsNullOrEmpty ? SamplePolicyJson() : policyStr.ToString();
            
            Log.Information("Policy retrieved successfully");
            return Results.Ok(policy);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve policy");
            return Results.Problem("Failed to retrieve policy");
        }
    })
    .WithName("GetPolicy")
    .WithTags("Policy")
    .WithSummary("Get current limits policy")
    .WithDescription("Retrieves the current limits policy configuration");

    // POST /limits/policy (JSON) -> upsert policy
    app.MapPost("/limits/policy", async (IConnectionMultiplexer mux, [FromBody] JsonElement json) =>
    {
        Log.Information("Policy update requested");
        
        try
        {
            var db = mux.GetDatabase();
            await db.StringSetAsync("limits:policy", json.GetRawText());
            
            Log.Information("Policy updated successfully");
            return Results.Ok(new { saved = true, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update policy");
            return Results.Problem("Failed to update policy");
        }
    })
    .WithName("UpdatePolicy")
    .WithTags("Policy")
    .WithSummary("Update limits policy")
    .WithDescription("Updates the limits policy configuration");

    // POST /limits/check -> decision
    app.MapPost("/limits/check", async (IConnectionMultiplexer mux, [FromBody] LimitCheckRequest req) =>
    {
        Log.Information("Limit check requested for tenant {TenantId}, actor {ActorId}, amount {Amount} {Currency}", 
            req.TenantId, req.ActorId, req.Minor, req.Currency);
        
        try
        {
            var db = mux.GetDatabase();
            var policyStr = await db.StringGetAsync("limits:policy");
            var policy = policyStr.IsNullOrEmpty ? 
                JsonSerializer.Deserialize<PolicyDoc>(SamplePolicyJson())! : 
                JsonSerializer.Deserialize<PolicyDoc>(policyStr!)!;

            // 1) MCC allow/deny
            foreach (var r in policy.Mcc)
            {
                if (r.merchantId is not null && r.merchantId != req.MerchantId) continue;
                if (r.mcc.Contains(req.Mcc))
                {
                    if (!r.allow)
                    {
                        Log.Warning("MCC {Mcc} blocked by rule {RuleId} for merchant {MerchantId}", 
                            req.Mcc, r.id, req.MerchantId);
                        return Results.Ok(new LimitDecision(false, "HARD_BLOCK", $"MCC {req.Mcc} blocked by {r.id}"));
                    }
                }
            }

            // 2) Time windows (simple local hour check)
            if (!string.IsNullOrWhiteSpace(req.LocalTimeIso))
            {
                var dt = DateTime.Parse(req.LocalTimeIso, null, DateTimeStyles.RoundtripKind);
                var hour = dt.Hour;
                foreach (var t in policy.Time)
                {
                    if (t.merchantId is not null && t.merchantId != req.MerchantId) continue;
                    var parts = t.cron.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[0], out var from) && int.TryParse(parts[1], out var to))
                    {
                        var within = hour >= from && hour <= to;
                        if (t.allow == false && within)
                        {
                            Log.Warning("Time window blocked by rule {RuleId} at hour {Hour}", t.id, hour);
                            return Results.Ok(new LimitDecision(false, "HARD_BLOCK", $"Time window blocked by {t.id}"));
                        }
                    }
                }
            }

            // 3) Geofence (point-in-polygon)
            if (req.Lat is not null && req.Lng is not null)
            {
                foreach (var g in policy.Geo)
                {
                    if (g.merchantId is not null && g.merchantId != req.MerchantId) continue;
                    if (PointInPolygon(req.Lat!.Value, req.Lng!.Value, g.polygon))
                    {
                        if (!g.allow)
                        {
                            Log.Warning("Geofence blocked by rule {RuleId} at location {Lat},{Lng}", 
                                g.id, req.Lat, req.Lng);
                            return Results.Ok(new LimitDecision(false, "HARD_BLOCK", $"Geo blocked by {g.id}"));
                        }
                    }
                }
            }

            // 4) Velocity counters (Redis INCR + TTL)
            foreach (var v in policy.Velocity.Where(x => x.currency == req.Currency))
            {
                var key = v.scope switch
                {
                    "per_device" => $"vel:{v.id}:{req.TenantId}:{req.DeviceId}:{v.window}",
                    "per_merchant" => $"vel:{v.id}:{req.TenantId}:{req.MerchantId}:{v.window}",
                    _ => $"vel:{v.id}:{req.TenantId}:{req.ActorId}:{v.window}"
                };
                var ttl = ParseWindow(v.window);
                
                // Increment amount (approx with running sum)
                var amount = await db.StringIncrementAsync(key, req.Minor);
                if (await db.KeyTimeToLiveAsync(key) is null) await db.KeyExpireAsync(key, ttl);
                
                if (amount > v.maxMinor)
                {
                    Log.Warning("Velocity exceeded for rule {RuleId}, scope {Scope}, limit {Limit}, actual {Actual}", 
                        v.id, v.scope, v.maxMinor, amount);
                    return Results.Ok(new LimitDecision(false, "SOFT_REVIEW", $"Velocity exceeded by {v.id}", 
                        new Dictionary<string, string>
                        {
                            {"window", v.window},
                            {"limit", v.maxMinor.ToString()},
                            {"actual", amount.ToString()}
                        }));
                }
            }

            Log.Information("Limit check passed for tenant {TenantId}, actor {ActorId}", req.TenantId, req.ActorId);
            return Results.Ok(new LimitDecision(true, "ALLOW", "ok"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process limit check for tenant {TenantId}, actor {ActorId}", req.TenantId, req.ActorId);
            return Results.Problem("Failed to process limit check");
        }

        static TimeSpan ParseWindow(string w) => w.EndsWith("m") ? TimeSpan.FromMinutes(int.Parse(w[..^1])) :
                                                 w.EndsWith("h") ? TimeSpan.FromHours(int.Parse(w[..^1])) :
                                                 w.EndsWith("d") ? TimeSpan.FromDays(int.Parse(w[..^1])) : TimeSpan.FromHours(1);

        static bool PointInPolygon(double lat, double lng, string[] poly)
        {
            var pts = poly.Select(p => p.Split(',')).Select(a => (lat: double.Parse(a[0]), lng: double.Parse(a[1]))).ToArray();
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var xi = pts[i].lng;
                var yi = pts[i].lat;
                var xj = pts[j].lng;
                var yj = pts[j].lat;
                bool intersect = ((yi > lat) != (yj > lat)) && (lng < (xj - xi) * (lat - yi) / (yj - yi + 1e-9) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }
    })
    .WithName("CheckLimits")
    .WithTags("Limits")
    .WithSummary("Check transaction limits")
    .WithDescription("Checks if a transaction is allowed based on current policy rules");

    Log.Information("AtlasBank Limits Service configured and ready to start");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string SamplePolicyJson() => """
{
  "version":"1.0",
  "velocity":[
    {"id":"per_actor_1h_100k","scope":"per_actor","window":"1h","currency":"NGN","maxMinor":10000000},
    {"id":"per_device_10m_30k","scope":"per_device","window":"10m","currency":"NGN","maxMinor":3000000},
    {"id":"per_merchant_1d_10m","scope":"per_merchant","window":"1d","currency":"NGN","maxMinor":1000000000}
  ],
  "mcc":[
    {"id":"deny_cash_like","allow":false,"mcc":["4829","6011"]},
    {"id":"allow_grocery","allow":true,"mcc":["5411"],"merchantId":"m-123"}
  ],
  "time":[
    {"id":"night_block","allow":false,"cron":"0-6","tz":"Africa/Lagos"}
  ],
  "geo":[
    {"id":"block_hotspot","allow":false,"polygon":["6.4654,3.4064","6.4660,3.4100","6.4620,3.4105","6.4615,3.4055"],"merchantId":null}
  ]
}
""";
