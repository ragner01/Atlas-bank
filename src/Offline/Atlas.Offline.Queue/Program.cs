#nullable disable
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;
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
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.Offline.Queue")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/offlineq-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Offline Queue Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Add services
    b.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(Environment.GetEnvironmentVariable("LEDGER_CONN") ?? "Host=postgres;Database=atlas_bank;Username=atlas;Password=atlas123").Build());
    b.Services.AddOpenApi(); 
    b.Services.AddEndpointsApiExplorer();

    var app = b.Build(); 
    app.MapOpenApi();

    // Add request/response logging middleware
    app.Use(async (ctx, next) =>
    {
        using (LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString()))
        {
            Log.Information("Incoming request: {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await next();
            Log.Information("Outgoing response: {StatusCode}", ctx.Response.StatusCode);
        }
    });

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Offline.Queue", timestamp = DateTime.UtcNow }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

    /*
       Offline-first mobile clients:
       - POST /offline/ops: submit signed ops when offline (queued per device)
       - POST /offline/sync: deliver pending ops when online; server processes in order
       - Signature = HMAC(deviceSecret, payload) (demo). In prod: Ed25519 with device binding + nonce.
    */
    app.MapPost("/offline/ops", async ([FromServices] IConnectionMultiplexer mux, [FromBody] OfflineOp req) =>
    {
        using (LogContext.PushProperty("DeviceId", req.DeviceId))
        using (LogContext.PushProperty("TenantId", req.TenantId))
        using (LogContext.PushProperty("Kind", req.Kind))
        using (LogContext.PushProperty("Nonce", req.Nonce))
        {
            Log.Information("Received offline operation: DeviceId={DeviceId}, TenantId={TenantId}, Kind={Kind}, Nonce={Nonce}", 
                req.DeviceId, req.TenantId, req.Kind, req.Nonce);

            try
            {
                if (!Verify(req)) 
                {
                    Log.Warning("Invalid signature for offline operation: DeviceId={DeviceId}, Nonce={Nonce}", req.DeviceId, req.Nonce);
                    return Results.Problem("bad signature", statusCode: 401);
                }

                var db = mux.GetDatabase();
                await db.ListLeftPushAsync($"offq:{req.DeviceId}", JsonSerializer.Serialize(req));
                await db.KeyExpireAsync($"offq:{req.DeviceId}", TimeSpan.FromDays(2));
                
                Log.Information("Offline operation enqueued: DeviceId={DeviceId}, Nonce={Nonce}", req.DeviceId, req.Nonce);
                return Results.Accepted();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing offline operation: DeviceId={DeviceId}, Nonce={Nonce}", req.DeviceId, req.Nonce);
                return Results.Problem("Failed to process offline operation", statusCode: 500);
            }
        }
    });

    app.MapPost("/offline/sync", async ([FromServices] IConnectionMultiplexer mux, [FromServices] NpgsqlDataSource ds, string deviceId, int max = 20) =>
    {
        using (LogContext.PushProperty("DeviceId", deviceId))
        using (LogContext.PushProperty("MaxOps", max))
        {
            Log.Information("Processing offline sync: DeviceId={DeviceId}, MaxOps={MaxOps}", deviceId, max);

            try
            {
                var db = mux.GetDatabase();
                var res = new List<object>();
                
                for (int i = 0; i < max; i++)
                {
                    var item = await db.ListRightPopAsync($"offq:{deviceId}");
                    if (item.IsNullOrEmpty) break;
                    
                    var op = JsonSerializer.Deserialize<OfflineOp>(item!)!;
                    Log.Information("Processing offline operation: DeviceId={DeviceId}, Kind={Kind}, Nonce={Nonce}", 
                        deviceId, op.Kind, op.Nonce);
                    
                    // process minimal operation: transfer
                    if (op.Kind == "transfer")
                    {
                        try
                        {
                            await using var conn = await ds.OpenConnectionAsync();
                            await using var cmd = new NpgsqlCommand("SELECT * FROM sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn);
                            cmd.Parameters.AddWithValue("k", $"off::{op.Nonce}");
                            cmd.Parameters.AddWithValue("t", op.TenantId);
                            cmd.Parameters.AddWithValue("s", op.Payload.GetProperty("source").GetString()!);
                            cmd.Parameters.AddWithValue("d", op.Payload.GetProperty("dest").GetString()!);
                            cmd.Parameters.AddWithValue("m", op.Payload.GetProperty("minor").GetInt64());
                            cmd.Parameters.AddWithValue("c", op.Payload.GetProperty("currency").GetString()!);
                            cmd.Parameters.AddWithValue("n", op.Payload.GetProperty("narration").GetString() ?? "offline");
                            var entry = await cmd.ExecuteScalarAsync();
                            res.Add(new { op = op.Nonce, entryId = entry });
                            
                            Log.Information("Offline transfer processed: DeviceId={DeviceId}, Nonce={Nonce}, EntryId={EntryId}", 
                                deviceId, op.Nonce, entry);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error processing offline transfer: DeviceId={DeviceId}, Nonce={Nonce}", deviceId, op.Nonce);
                            res.Add(new { op = op.Nonce, error = "Processing failed" });
                        }
                    }
                    else
                    {
                        Log.Warning("Unsupported offline operation kind: {Kind} for DeviceId={DeviceId}", op.Kind, deviceId);
                        res.Add(new { op = op.Nonce, error = "Unsupported operation" });
                    }
                }
                
                Log.Information("Offline sync completed: DeviceId={DeviceId}, Processed={Count}", deviceId, res.Count);
                return Results.Ok(new { processed = res.Count, results = res });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing offline sync: DeviceId={DeviceId}", deviceId);
                return Results.Problem("Failed to process offline sync", statusCode: 500);
            }
        }
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

static bool Verify(OfflineOp op)
{
    var secret = Environment.GetEnvironmentVariable("OFFLINE_SECRET") ?? "dev-secret"; // per-device secret in prod
    using var h = new HMACSHA256(Encoding.UTF8.GetBytes($"{secret}:{op.DeviceId}"));
    var bytes = Encoding.UTF8.GetBytes(op.Payload.GetRawText() + op.Nonce + op.TenantId);
    var sig = Convert.ToHexString(h.ComputeHash(bytes)).ToLowerInvariant();
    return string.Equals(sig, op.Signature, StringComparison.OrdinalIgnoreCase);
}

public record OfflineOp(string TenantId, string DeviceId, string Kind, JsonElement Payload, string Nonce, string Signature);
