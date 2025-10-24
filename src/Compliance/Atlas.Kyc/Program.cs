#nullable disable
using Microsoft.AspNetCore.Mvc;
using Npgsql;
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
    .Enrich.WithProperty("Application", "Atlas.Kyc")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/kyc-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank KYC Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Database connection
    var kycDbConnectionString = Environment.GetEnvironmentVariable("KYC_DB") ?? "Host=postgres;Database=atlas;Username=postgres;Password=postgres";
    Log.Information("Connecting to KYC database");
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(kycDbConnectionString).Build());

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

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Kyc", timestamp = DateTime.UtcNow }));

    // POST /kyc/start — begins KYC flow (BVN/NIN + selfie liveness + PoA)
    app.MapPost("/kyc/start", async ([FromServices] NpgsqlDataSource ds, [FromBody] KycStart req, CancellationToken ct) =>
    {
        Log.Information("Starting KYC process for customer {CustomerId}", req.CustomerId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO kyc_applications(id, customer_id, status, created_at) 
            VALUES(@i, @c, 'PENDING', now())", conn);
        cmd.Parameters.AddWithValue("i", id);
        cmd.Parameters.AddWithValue("c", req.CustomerId);
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("KYC application {ApplicationId} created for customer {CustomerId}", id, req.CustomerId);
        
        return Results.Ok(new { 
            applicationId = id, 
            next = new[] { "bvn", "nin", "selfie", "poa" },
            status = "PENDING"
        });
    });

    // POST /kyc/bvn — mock BVN verification (replace with vendor in prod)
    app.MapPost("/kyc/bvn", async ([FromServices] NpgsqlDataSource ds, [FromBody] BvnReq req, CancellationToken ct) =>
    {
        Log.Information("Processing BVN verification for application {ApplicationId}", req.ApplicationId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Simple pass/fail heuristic for demo
        // In production, integrate with BVN verification service
        bool ok = !string.IsNullOrWhiteSpace(req.Bvn) && req.Bvn.Length == 11 && req.Bvn.All(char.IsDigit);
        
        var bvnData = new { 
            bvn = req.Bvn, 
            ok, 
            verified_at = DateTime.UtcNow,
            provider = "mock" // Replace with actual provider
        };
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO kyc_facts(application_id, key, val) 
            VALUES(@a, 'bvn', @v) 
            ON CONFLICT(application_id, key) DO UPDATE SET val = @v", conn);
        cmd.Parameters.AddWithValue("a", req.ApplicationId);
        cmd.Parameters.AddWithValue("v", JsonSerializer.Serialize(bvnData));
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("BVN verification {Status} for application {ApplicationId}", ok ? "passed" : "failed", req.ApplicationId);
        
        return Results.Ok(new { 
            ok, 
            message = ok ? "BVN verified successfully" : "BVN verification failed",
            next = ok ? "nin" : null
        });
    });

    // POST /kyc/nin — mock NIN verification
    app.MapPost("/kyc/nin", async ([FromServices] NpgsqlDataSource ds, [FromBody] NinReq req, CancellationToken ct) =>
    {
        Log.Information("Processing NIN verification for application {ApplicationId}", req.ApplicationId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Simple validation for demo
        // In production, integrate with NIN verification service
        bool ok = !string.IsNullOrWhiteSpace(req.Nin) && req.Nin.Length >= 10;
        
        var ninData = new { 
            nin = req.Nin, 
            ok, 
            verified_at = DateTime.UtcNow,
            provider = "mock" // Replace with actual provider
        };
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO kyc_facts(application_id, key, val) 
            VALUES(@a, 'nin', @v) 
            ON CONFLICT(application_id, key) DO UPDATE SET val = @v", conn);
        cmd.Parameters.AddWithValue("a", req.ApplicationId);
        cmd.Parameters.AddWithValue("v", JsonSerializer.Serialize(ninData));
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("NIN verification {Status} for application {ApplicationId}", ok ? "passed" : "failed", req.ApplicationId);
        
        return Results.Ok(new { 
            ok, 
            message = ok ? "NIN verified successfully" : "NIN verification failed",
            next = ok ? "selfie" : null
        });
    });

    // POST /kyc/selfie — liveness score (0..1) mock
    app.MapPost("/kyc/selfie", async ([FromServices] NpgsqlDataSource ds, [FromBody] LivenessReq req, CancellationToken ct) =>
    {
        Log.Information("Processing selfie liveness check for application {ApplicationId} with score {Score}", req.ApplicationId, req.Score);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        var score = Math.Clamp(req.Score, 0, 1);
        bool ok = score >= 0.6; // Threshold for liveness
        
        var livenessData = new { 
            score, 
            ok, 
            verified_at = DateTime.UtcNow,
            threshold = 0.6,
            provider = "mock" // Replace with actual liveness detection service
        };
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO kyc_facts(application_id, key, val) 
            VALUES(@a, 'liveness', @v) 
            ON CONFLICT(application_id, key) DO UPDATE SET val = @v", conn);
        cmd.Parameters.AddWithValue("a", req.ApplicationId);
        cmd.Parameters.AddWithValue("v", JsonSerializer.Serialize(livenessData));
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("Selfie liveness check {Status} for application {ApplicationId} (score: {Score})", ok ? "passed" : "failed", req.ApplicationId, score);
        
        return Results.Ok(new { 
            ok, 
            score,
            message = ok ? "Liveness check passed" : "Liveness check failed",
            next = ok ? "poa" : null
        });
    });

    // POST /kyc/poa — proof of address (OCR result hash)
    app.MapPost("/kyc/poa", async ([FromServices] NpgsqlDataSource ds, [FromBody] PoaReq req, CancellationToken ct) =>
    {
        Log.Information("Processing proof of address for application {ApplicationId}", req.ApplicationId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Simple validation for demo
        // In production, integrate with OCR and address verification service
        bool ok = !string.IsNullOrWhiteSpace(req.AddressHash) && req.AddressHash.Length >= 32;
        
        var poaData = new { 
            address_hash = req.AddressHash, 
            ok, 
            verified_at = DateTime.UtcNow,
            provider = "mock" // Replace with actual OCR service
        };
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO kyc_facts(application_id, key, val) 
            VALUES(@a, 'poa', @v) 
            ON CONFLICT(application_id, key) DO UPDATE SET val = @v", conn);
        cmd.Parameters.AddWithValue("a", req.ApplicationId);
        cmd.Parameters.AddWithValue("v", JsonSerializer.Serialize(poaData));
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("Proof of address {Status} for application {ApplicationId}", ok ? "passed" : "failed", req.ApplicationId);
        
        return Results.Ok(new { 
            ok, 
            message = ok ? "Proof of address verified successfully" : "Proof of address verification failed",
            next = ok ? "decision" : null
        });
    });

    // POST /kyc/decision — rules: all signals true + not on sanctions list => APPROVED else REVIEW/REJECT
    app.MapPost("/kyc/decision", async ([FromServices] NpgsqlDataSource ds, [FromServices] IConnectionMultiplexer mux, [FromBody] DecisionReq req, CancellationToken ct) =>
    {
        Log.Information("Processing KYC decision for application {ApplicationId}, customer {CustomerId}", req.ApplicationId, req.CustomerId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Collect all verification facts
        var facts = new Dictionary<string, bool>();
        await using (var cmd = new NpgsqlCommand("SELECT key, val FROM kyc_facts WHERE application_id = @a", conn))
        {
            cmd.Parameters.AddWithValue("a", req.ApplicationId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var key = r.GetString(0);
                var json = r.GetString(1);
                
                // Parse JSON to check if verification passed
                try
                {
                    var factData = JsonSerializer.Deserialize<JsonElement>(json);
                    var ok = factData.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                    facts[key] = ok;
                }
                catch
                {
                    facts[key] = false;
                }
            }
        }

        // Check if all required verifications passed
        var allOk = facts.TryGetValue("bvn", out var bvnOk) && bvnOk
                 && facts.TryGetValue("nin", out var ninOk) && ninOk
                 && facts.TryGetValue("liveness", out var livenessOk) && livenessOk
                 && facts.TryGetValue("poa", out var poaOk) && poaOk;

        // Sanctions check
        var db = mux.GetDatabase();
        var isSanctioned = await db.SetContainsAsync("aml:sanctions", req.CustomerId);
        
        // Decision logic
        var decision = isSanctioned ? "REJECT" : allOk ? "APPROVED" : "REVIEW";
        var reason = isSanctioned ? "Customer on sanctions list" : 
                    allOk ? "All verifications passed" : 
                    "One or more verifications failed";

        // Update application status
        await using (var up = new NpgsqlCommand(@"
            UPDATE kyc_applications 
            SET status = @s, decided_at = now() 
            WHERE id = @i", conn))
        {
            up.Parameters.AddWithValue("i", req.ApplicationId);
            up.Parameters.AddWithValue("s", decision);
            await up.ExecuteNonQueryAsync(ct);
        }

        Log.Information("KYC decision {Decision} for application {ApplicationId}, customer {CustomerId}. Reason: {Reason}", 
            decision, req.ApplicationId, req.CustomerId, reason);

        return Results.Ok(new { 
            decision, 
            reason,
            facts = facts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            sanctions_check = !isSanctioned,
            completed_at = DateTime.UtcNow
        });
    });

    // GET /kyc/status/{applicationId} — get current status
    app.MapGet("/kyc/status/{applicationId:guid}", async (Guid applicationId, [FromServices] NpgsqlDataSource ds) =>
    {
        Log.Information("Retrieving KYC status for application {ApplicationId}", applicationId);
        
        await using var conn = await ds.OpenConnectionAsync();
        
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, customer_id, status, created_at, decided_at 
            FROM kyc_applications 
            WHERE id = @i", conn);
        cmd.Parameters.AddWithValue("i", applicationId);
        
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) 
        {
            Log.Warning("KYC application {ApplicationId} not found", applicationId);
            return Results.NotFound(new { error = "Application not found" });
        }

        var result = new
        {
            id = r.GetGuid(0),
            customer_id = r.GetString(1),
            status = r.GetString(2),
            created_at = r.GetDateTime(3),
            decided_at = r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4)
        };

        Log.Information("Retrieved KYC status for application {ApplicationId}: {Status}", applicationId, result.status);
        
        return Results.Ok(result);
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

// Data models
public record KycStart(string CustomerId);
public record BvnReq(Guid ApplicationId, string Bvn);
public record NinReq(Guid ApplicationId, string Nin);
public record LivenessReq(Guid ApplicationId, double Score);
public record PoaReq(Guid ApplicationId, string AddressHash);
public record DecisionReq(Guid ApplicationId, string CustomerId);
