using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Atlas.FeatureStore")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/featurestore-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Feature Store Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    b.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(Environment.GetEnvironmentVariable("CLICKHOUSE_CONN") ?? "Host=clickhouse;Port=9000;Username=default;Password=").Build());
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

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.FeatureStore", timestamp = DateTime.UtcNow }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

    // GET /features/txn?tenant=tnt_demo&src=acc_A&dst=acc_B&currency=NGN
    // Returns a compact feature vector used by Risk & ML (rolling sums, velocity, recency)
    app.MapGet("/features/txn", async (string tenant, string src, string dst, string currency, IConnectionMultiplexer mux) =>
    {
        using (LogContext.PushProperty("Tenant", tenant))
        using (LogContext.PushProperty("Source", src))
        using (LogContext.PushProperty("Destination", dst))
        using (LogContext.PushProperty("Currency", currency))
        {
            Log.Information("Processing feature request for transaction");

            try
            {
                var db = mux.GetDatabase();
                
                // Rolling sums maintained via limits / risk events (Phase 11/19) or computed ad-hoc from counters:
                long v10m = (long)(await db.StringGetAsync($"vel:sum10m:{tenant}:{src}") ?? 0);
                long v1h = (long)(await db.StringGetAsync($"vel:sum1h:{tenant}:{src}") ?? 0);
                long v1d = (long)(await db.StringGetAsync($"vel:sum1d:{tenant}:{src}") ?? 0);
                long pairCount = (long)(await db.StringGetAsync($"pair:{tenant}:{src}->{dst}:count") ?? 0);
                long lastTs = (long)(await db.StringGetAsync($"pair:{tenant}:{src}->{dst}:last") ?? 0);

                double recMin = lastTs == 0 ? 9999 : Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastTs) / 60000.0);

                var features = new
                {
                    tenant,
                    src,
                    dst,
                    currency,
                    f = new
                    {
                        sum10m_minor = v10m,
                        sum1h_minor = v1h,
                        sum1d_minor = v1d,
                        pair_count = pairCount,
                        recency_min = recMin
                    }
                };

                Log.Information("Feature request completed successfully");
                return Results.Ok(features);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing feature request");
                return Results.Problem("Failed to retrieve features", statusCode: 500);
            }
        }
    });

    // POST /features/observe — optional: services publish observations to keep counters fresh
    app.MapPost("/features/observe", async (IConnectionMultiplexer mux, [FromBody] JsonElement e) =>
    {
        using (LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString()))
        {
            Log.Information("Processing feature observation");

            try
            {
                var db = mux.GetDatabase();
                string tenant = e.GetProperty("tenant").GetString()!;
                string src = e.GetProperty("src").GetString()!;
                string dst = e.GetProperty("dst").GetString()!;
                long minor = e.GetProperty("minor").GetInt64();
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                using (LogContext.PushProperty("Tenant", tenant))
                using (LogContext.PushProperty("Source", src))
                using (LogContext.PushProperty("Destination", dst))
                using (LogContext.PushProperty("Minor", minor))
                {
                    // Update velocity counters
                    await db.StringIncrementAsync($"vel:sum10m:{tenant}:{src}", minor);
                    await db.KeyExpireAsync($"vel:sum10m:{tenant}:{src}", TimeSpan.FromMinutes(10));
                    
                    await db.StringIncrementAsync($"vel:sum1h:{tenant}:{src}", minor);
                    await db.KeyExpireAsync($"vel:sum1h:{tenant}:{src}", TimeSpan.FromHours(1));
                    
                    await db.StringIncrementAsync($"vel:sum1d:{tenant}:{src}", minor);
                    await db.KeyExpireAsync($"vel:sum1d:{tenant}:{src}", TimeSpan.FromDays(1));

                    // Update pair counters
                    await db.StringIncrementAsync($"pair:{tenant}:{src}->{dst}:count", 1);
                    await db.StringSetAsync($"pair:{tenant}:{src}->{dst}:last", now, TimeSpan.FromDays(7));

                    Log.Information("Feature observation processed successfully");
                    return Results.Accepted();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing feature observation");
                return Results.Problem("Failed to process observation", statusCode: 500);
            }
        }
    });

    // GET /features/velocity?tenant=tnt_demo&account=acc_A&window=1h
    // Returns velocity metrics for a specific account and time window
    app.MapGet("/features/velocity", async (string tenant, string account, string window, IConnectionMultiplexer mux) =>
    {
        using (LogContext.PushProperty("Tenant", tenant))
        using (LogContext.PushProperty("Account", account))
        using (LogContext.PushProperty("Window", window))
        {
            Log.Information("Processing velocity feature request");

            try
            {
                var db = mux.GetDatabase();
                var key = $"vel:sum{window}:{tenant}:{account}";
                var velocity = (long)(await db.StringGetAsync(key) ?? 0);

                var result = new
                {
                    tenant,
                    account,
                    window,
                    velocity_minor = velocity,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                Log.Information("Velocity feature request completed successfully");
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing velocity feature request");
                return Results.Problem("Failed to retrieve velocity features", statusCode: 500);
            }
        }
    });

    // GET /features/pair?tenant=tnt_demo&src=acc_A&dst=acc_B
    // Returns pair-specific features (count, recency, etc.)
    app.MapGet("/features/pair", async (string tenant, string src, string dst, IConnectionMultiplexer mux) =>
    {
        using (LogContext.PushProperty("Tenant", tenant))
        using (LogContext.PushProperty("Source", src))
        using (LogContext.PushProperty("Destination", dst))
        {
            Log.Information("Processing pair feature request");

            try
            {
                var db = mux.GetDatabase();
                var countKey = $"pair:{tenant}:{src}->{dst}:count";
                var lastKey = $"pair:{tenant}:{src}->{dst}:last";
                
                var count = (long)(await db.StringGetAsync(countKey) ?? 0);
                var lastTs = (long)(await db.StringGetAsync(lastKey) ?? 0);
                
                double recencyMin = lastTs == 0 ? 9999 : Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastTs) / 60000.0);

                var result = new
                {
                    tenant,
                    src,
                    dst,
                    count,
                    last_transaction_ms = lastTs,
                    recency_minutes = recencyMin,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                Log.Information("Pair feature request completed successfully");
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing pair feature request");
                return Results.Problem("Failed to retrieve pair features", statusCode: 500);
            }
        }
    });

    // GET /features/stats
    // Returns overall feature store statistics
    app.MapGet("/features/stats", async (IConnectionMultiplexer mux) =>
    {
        Log.Information("Processing feature store stats request");

        try
        {
            var db = mux.GetDatabase();
            var server = mux.GetServer(mux.GetEndPoints().First());
            
            var info = await server.InfoAsync();
            var keyCount = await server.DatabaseSizeAsync(db.Database);

            var stats = new
            {
                redis_info = new
                {
                    connected_clients = info.FirstOrDefault(x => x.Key == "connected_clients").Value,
                    used_memory_human = info.FirstOrDefault(x => x.Key == "used_memory_human").Value,
                    uptime_in_seconds = info.FirstOrDefault(x => x.Key == "uptime_in_seconds").Value
                },
                feature_store = new
                {
                    total_keys = keyCount,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            };

            Log.Information("Feature store stats request completed successfully");
            return Results.Ok(stats);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing feature store stats request");
            return Results.Problem("Failed to retrieve feature store stats", statusCode: 500);
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
