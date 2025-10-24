#nullable disable
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StackExchange.Redis;
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
    .Enrich.WithProperty("Application", "Atlas.Agent.Network")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/agentnet-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Agent Network Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Add services
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(Environment.GetEnvironmentVariable("LEDGER_CONN") ?? "Host=postgres;Database=atlas_bank;Username=atlas;Password=atlas123").Build());
    b.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
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

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Agent.Network", timestamp = DateTime.UtcNow }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

    /*
      Agent model:
      - Each agent has wallet account: "agent::{code}"
      - Customer MSISDN account: "msisdn::{phone}"
      - Cash-out (withdraw): debit customer, credit agent (commission on top)
      - Cash-in (deposit): debit agent, credit customer
      - Intents are short-lived tokens in Redis approved by agent POS
    */
    app.MapPost("/agent/withdraw/intent", async ([FromServices] IConnectionMultiplexer mux, string msisdn, string agent, long minor, string currency) =>
    {
        using (LogContext.PushProperty("Msisdn", msisdn))
        using (LogContext.PushProperty("Agent", agent))
        using (LogContext.PushProperty("Amount", minor))
        using (LogContext.PushProperty("Currency", currency))
        {
            Log.Information("Creating withdrawal intent: Msisdn={Msisdn}, Agent={Agent}, Amount={Amount}, Currency={Currency}", 
                msisdn, agent, minor, currency);

            try
            {
                var db = mux.GetDatabase();
                var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                await db.HashSetAsync($"intent:{code}", new HashEntry[] {
                    new("type","withdraw"), new("msisdn", msisdn), new("agent", agent), new("minor", minor), new("currency", currency)
                });
                await db.KeyExpireAsync($"intent:{code}", TimeSpan.FromMinutes(5));
                
                Log.Information("Withdrawal intent created: Code={Code}, Msisdn={Msisdn}, Agent={Agent}", code, msisdn, agent);
                return Results.Ok(new { code });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating withdrawal intent for {Msisdn}", msisdn);
                return Results.Problem("Failed to create withdrawal intent", statusCode: 500);
            }
        }
    });

    app.MapPost("/agent/cashin/intent", async ([FromServices] IConnectionMultiplexer mux, string msisdn, string agent, long minor, string currency) =>
    {
        using (LogContext.PushProperty("Msisdn", msisdn))
        using (LogContext.PushProperty("Agent", agent))
        using (LogContext.PushProperty("Amount", minor))
        using (LogContext.PushProperty("Currency", currency))
        {
            Log.Information("Creating cash-in intent: Msisdn={Msisdn}, Agent={Agent}, Amount={Amount}, Currency={Currency}", 
                msisdn, agent, minor, currency);

            try
            {
                var db = mux.GetDatabase();
                var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                await db.HashSetAsync($"intent:{code}", new HashEntry[] {
                    new("type","cashin"), new("msisdn", msisdn), new("agent", agent), new("minor", minor), new("currency", currency)
                });
                await db.KeyExpireAsync($"intent:{code}", TimeSpan.FromMinutes(5));
                
                Log.Information("Cash-in intent created: Code={Code}, Msisdn={Msisdn}, Agent={Agent}", code, msisdn, agent);
                return Results.Ok(new { code });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating cash-in intent for {Msisdn}", msisdn);
                return Results.Problem("Failed to create cash-in intent", statusCode: 500);
            }
        }
    });

    // Agent POS confirms an intent with one-time code
    app.MapPost("/agent/confirm", async ([FromServices] NpgsqlDataSource ds, [FromServices] IConnectionMultiplexer mux, string code) =>
    {
        using (LogContext.PushProperty("IntentCode", code))
        {
            Log.Information("Processing agent confirmation: Code={Code}", code);

            try
            {
                var db = mux.GetDatabase();
                var data = await db.HashGetAllAsync($"intent:{code}");
                if (data.Length == 0) 
                {
                    Log.Warning("Invalid intent code: {Code}", code);
                    return Results.NotFound("invalid code");
                }

                string type = data.First(x => x.Name == "type").Value!;
                string msisdn = data.First(x => x.Name == "msisdn").Value!;
                string agent = data.First(x => x.Name == "agent").Value!;
                long minor = (long)data.First(x => x.Name == "minor").Value;
                string currency = data.First(x => x.Name == "currency").Value!;

                string cust = $"msisdn::{msisdn}";
                string ag = $"agent::{agent}";
                long commission = (long)Math.Max(100, Math.Round(minor * 0.01)); // 1% or 1.00

                Log.Information("Processing {Type} confirmation: Msisdn={Msisdn}, Agent={Agent}, Amount={Amount}, Commission={Commission}", 
                    type, msisdn, agent, minor, commission);

                await using var conn = await ds.OpenConnectionAsync();
                
                // perform ledger postings: use fast stored proc (exists from earlier phases)
                if (type == "withdraw")
                {
                    // customer -> agent (amount + commission)
                    await using var cmd = new NpgsqlCommand("SELECT * FROM sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn);
                    cmd.Parameters.AddWithValue("k", $"wd::{code}");
                    cmd.Parameters.AddWithValue("t", "tnt_demo");
                    cmd.Parameters.AddWithValue("s", cust);
                    cmd.Parameters.AddWithValue("d", ag);
                    cmd.Parameters.AddWithValue("m", minor + commission);
                    cmd.Parameters.AddWithValue("c", currency);
                    cmd.Parameters.AddWithValue("n", $"Agent withdraw {agent}");
                    await cmd.ExecuteScalarAsync();
                    
                    Log.Information("Withdrawal processed: Customer={Customer} -> Agent={Agent}, Amount={Amount}, Commission={Commission}", 
                        cust, ag, minor, commission);
                }
                else
                {
                    // agent -> customer (amount - commission) ; commission kept by agent
                    await using var cmd = new NpgsqlCommand("SELECT * FROM sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn);
                    cmd.Parameters.AddWithValue("k", $"ci::{code}");
                    cmd.Parameters.AddWithValue("t", "tnt_demo");
                    cmd.Parameters.AddWithValue("s", ag);
                    cmd.Parameters.AddWithValue("d", cust);
                    cmd.Parameters.AddWithValue("m", minor - commission);
                    cmd.Parameters.AddWithValue("c", currency);
                    cmd.Parameters.AddWithValue("n", $"Agent cashin {agent}");
                    await cmd.ExecuteScalarAsync();
                    
                    Log.Information("Cash-in processed: Agent={Agent} -> Customer={Customer}, Amount={Amount}, Commission={Commission}", 
                        ag, cust, minor, commission);
                }

                await db.KeyDeleteAsync($"intent:{code}");
                Log.Information("Intent confirmed and processed: Code={Code}, Type={Type}", code, type);
                
                return Results.Ok(new { status = "POSTED", type, minor, commission });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing agent confirmation: Code={Code}", code);
                return Results.Problem("Failed to process confirmation", statusCode: 500);
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
