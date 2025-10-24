#nullable disable
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Aml")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/aml-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank AML Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Redis connection
    var redisConnectionString = Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379";
    Log.Information("Connecting to Redis at {RedisConnection}", redisConnectionString);
    b.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

    b.Services.AddOpenApi();
    b.Services.AddEndpointsApiExplorer();

    var app = b.Build();
    app.MapOpenApi();

    // Request/Response logging middleware
    app.Use(async (context, next) =>
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ??
                           Guid.NewGuid().ToString();
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        Log.Information("Request context: CorrelationId={CorrelationId}, RequestPath={RequestPath}, RequestMethod={RequestMethod}, UserAgent={UserAgent}, RemoteIp={RemoteIp}",
            correlationId, context.Request.Path, context.Request.Method,
            context.Request.Headers.UserAgent.ToString(),
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

        await next();

        stopwatch.Stop();
        Log.Information("Request finished: {Method} {Path} with status {StatusCode} in {ElapsedMs}ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    });

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Aml", timestamp = DateTime.UtcNow }));

    // POST /aml/sanctions/load — bulk load sanctions IDs (Redis SET)
    app.MapPost("/aml/sanctions/load", async ([FromServices] IConnectionMultiplexer mux, [FromBody] SanctionsLoadReq req, CancellationToken ct) =>
    {
        Log.Information("Loading {Count} sanctions IDs", req.SanctionsIds.Length);
        
        var db = mux.GetDatabase();
        var added = 0;
        
        foreach (var id in req.SanctionsIds)
        {
            if (await db.SetAddAsync("aml:sanctions", id))
            {
                added++;
            }
        }
        
        Log.Information("Added {Added} new sanctions IDs to Redis", added);
        
        return Results.Ok(new { 
            loaded = req.SanctionsIds.Length, 
            added,
            total = await db.SetLengthAsync("aml:sanctions")
        });
    });

    // POST /aml/scan — scan transaction for AML flags
    app.MapPost("/aml/scan", async ([FromServices] IConnectionMultiplexer mux, [FromBody] AmlScanReq req, CancellationToken ct) =>
    {
        Log.Information("Scanning transaction {TransactionId} for AML flags", req.TransactionId);
        
        var db = mux.GetDatabase();
        var flags = new List<string>();
        var riskScore = 0.0;

        // 1. Sanctions check
        var isSanctioned = await db.SetContainsAsync("aml:sanctions", req.CustomerId);
        if (isSanctioned)
        {
            flags.Add("SANCTIONS");
            riskScore += 1.0;
            Log.Warning("Customer {CustomerId} is on sanctions list", req.CustomerId);
        }

        // 2. High-value transaction check
        if (req.AmountMinor > 5000000) // 50,000 NGN
        {
            flags.Add("HIGH_VALUE");
            riskScore += 0.3;
            Log.Information("High-value transaction detected: {AmountMinor} minor units", req.AmountMinor);
        }

        // 3. Velocity check (simplified - in production, use proper velocity tracking)
        var velocityKey = $"aml:velocity:{req.CustomerId}:{DateTime.UtcNow:yyyy-MM-dd}";
        var dailyVolume = await db.StringIncrementAsync(velocityKey, req.AmountMinor);
        await db.KeyExpireAsync(velocityKey, TimeSpan.FromDays(1));
        
        if (dailyVolume > 10000000) // 100,000 NGN per day
        {
            flags.Add("VELOCITY");
            riskScore += 0.5;
            Log.Information("Velocity threshold exceeded for customer {CustomerId}: {DailyVolume} minor units", req.CustomerId, dailyVolume);
        }

        // 4. Geographic risk (simplified)
        if (req.Lat.HasValue && req.Lng.HasValue)
        {
            // Mock geographic risk zones (in production, use proper geofencing)
            var isHighRiskZone = IsHighRiskZone(req.Lat.Value, req.Lng.Value);
            if (isHighRiskZone)
            {
                flags.Add("GEO_RISK");
                riskScore += 0.2;
                Log.Information("Transaction in high-risk geographic zone: {Lat}, {Lng}", req.Lat, req.Lng);
            }
        }

        // 5. Time-based risk (night transactions)
        if (req.Timestamp.Hour < 6 || req.Timestamp.Hour > 22)
        {
            flags.Add("NIGHT_TRANSACTION");
            riskScore += 0.1;
            Log.Information("Night transaction detected at {Hour}:00", req.Timestamp.Hour);
        }

        // Determine overall risk level
        var riskLevel = riskScore switch
        {
            >= 1.0 => "HIGH",
            >= 0.5 => "MEDIUM",
            >= 0.2 => "LOW",
            _ => "MINIMAL"
        };

        var requiresReview = flags.Count > 0 || riskLevel != "MINIMAL";

        Log.Information("AML scan completed for transaction {TransactionId}: Risk={RiskLevel}, Flags={Flags}, RequiresReview={RequiresReview}", 
            req.TransactionId, riskLevel, string.Join(",", flags), requiresReview);

        return Results.Ok(new AmlScanResult(
            req.TransactionId,
            riskLevel,
            riskScore,
            flags.ToArray(),
            requiresReview,
            DateTime.UtcNow,
            new[]
            {
                "SANCTIONS_CHECK",
                "HIGH_VALUE_CHECK", 
                "VELOCITY_CHECK",
                "GEOGRAPHIC_RISK",
                "TIME_BASED_RISK"
            }
        ));
    });

    // GET /aml/sanctions/count — get total sanctions count
    app.MapGet("/aml/sanctions/count", async ([FromServices] IConnectionMultiplexer mux) =>
    {
        var db = mux.GetDatabase();
        var count = await db.SetLengthAsync("aml:sanctions");
        
        Log.Information("Retrieved sanctions count: {Count}", count);
        
        return Results.Ok(new { count });
    });

    // GET /aml/sanctions/check/{customerId} — check if customer is sanctioned
    app.MapGet("/aml/sanctions/check/{customerId}", async (string customerId, [FromServices] IConnectionMultiplexer mux) =>
    {
        Log.Information("Checking sanctions status for customer {CustomerId}", customerId);
        
        var db = mux.GetDatabase();
        var isSanctioned = await db.SetContainsAsync("aml:sanctions", customerId);
        
        Log.Information("Customer {CustomerId} sanctions status: {IsSanctioned}", customerId, isSanctioned);
        
        return Results.Ok(new { 
            customer_id = customerId,
            is_sanctioned = isSanctioned,
            checked_at = DateTime.UtcNow
        });
    });

    // POST /aml/sanctions/add — add single sanctions ID
    app.MapPost("/aml/sanctions/add", async ([FromServices] IConnectionMultiplexer mux, [FromBody] SanctionsAddReq req, CancellationToken ct) =>
    {
        Log.Information("Adding sanctions ID: {SanctionsId}", req.SanctionsId);
        
        var db = mux.GetDatabase();
        var added = await db.SetAddAsync("aml:sanctions", req.SanctionsId);
        
        Log.Information("Sanctions ID {SanctionsId} {Status}", req.SanctionsId, added ? "added" : "already exists");
        
        return Results.Ok(new { 
            sanctions_id = req.SanctionsId,
            added,
            total = await db.SetLengthAsync("aml:sanctions")
        });
    });

    // POST /aml/sanctions/remove — remove single sanctions ID
    app.MapPost("/aml/sanctions/remove", async ([FromServices] IConnectionMultiplexer mux, [FromBody] SanctionsRemoveReq req, CancellationToken ct) =>
    {
        Log.Information("Removing sanctions ID: {SanctionsId}", req.SanctionsId);
        
        var db = mux.GetDatabase();
        var removed = await db.SetRemoveAsync("aml:sanctions", req.SanctionsId);
        
        Log.Information("Sanctions ID {SanctionsId} {Status}", req.SanctionsId, removed ? "removed" : "not found");
        
        return Results.Ok(new { 
            sanctions_id = req.SanctionsId,
            removed,
            total = await db.SetLengthAsync("aml:sanctions")
        });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Helper function to check if coordinates are in a high-risk zone
static bool IsHighRiskZone(double lat, double lng)
{
    // Mock high-risk zones (in production, use proper geofencing)
    // Example: Lagos high-risk areas
    var highRiskZones = new[]
    {
        new { lat = 6.4654, lng = 3.4064, radius = 0.01 }, // Example zone 1
        new { lat = 6.4660, lng = 3.4100, radius = 0.01 }  // Example zone 2
    };

    foreach (var zone in highRiskZones)
    {
        var distance = Math.Sqrt(Math.Pow(lat - zone.lat, 2) + Math.Pow(lng - zone.lng, 2));
        if (distance <= zone.radius)
        {
            return true;
        }
    }
    
    return false;
}

// Data models
public record SanctionsLoadReq(string[] SanctionsIds);
public record SanctionsAddReq(string SanctionsId);
public record SanctionsRemoveReq(string SanctionsId);

public record AmlScanReq(
    string TransactionId,
    string CustomerId,
    long AmountMinor,
    string Currency,
    DateTime Timestamp,
    double? Lat = null,
    double? Lng = null,
    string? MerchantId = null,
    string? DeviceId = null
);

public record AmlScanResult(
    string TransactionId,
    string RiskLevel,
    double RiskScore,
    string[] Flags,
    bool RequiresReview,
    DateTime ScannedAt,
    string[] RulesApplied
);
