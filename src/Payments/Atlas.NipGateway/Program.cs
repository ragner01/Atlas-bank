#nullable disable
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
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
    .Enrich.WithProperty("Application", "Atlas.NipGateway")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/nipgw-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank NIP Gateway Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Add services
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(Environment.GetEnvironmentVariable("LEDGER_CONN") ?? "Host=postgres;Database=atlas_bank;Username=atlas;Password=atlas123").Build());
    
    // Add Kafka producer with error handling
    try
    {
        var producer = new ProducerBuilder<string,string>(new ProducerConfig{ 
            BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092", 
            Acks=Acks.All, 
            EnableIdempotence=true 
        }).Build();
        b.Services.AddSingleton(producer);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to initialize Kafka producer, service will run in limited mode");
        // Create a mock producer that does nothing
        b.Services.AddSingleton<IProducer<string,string>>(new MockProducer());
    }
    
    b.Services.AddOpenApi(); 
    b.Services.AddEndpointsApiExplorer();

    var app = b.Build(); 
    app.MapOpenApi();

    // Add request/response logging middleware
    app.Use(async (context, next) =>
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        using (LogContext.PushProperty("RequestId", requestId))
        {
            Log.Information("Request: {Method} {Path} from {RemoteIp}", 
                context.Request.Method, 
                context.Request.Path, 
                context.Connection.RemoteIpAddress);
            await next();
            Log.Information("Response: {StatusCode} for {Method} {Path}", 
                context.Response.StatusCode, 
                context.Request.Method, 
                context.Request.Path);
        }
    });

    app.MapGet("/health", () => Results.Ok(new { 
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());
        ok = true, 
        service = "Atlas.NipGateway", 
        timestamp = DateTime.UtcNow 
    }));

    // POST /nip/credit-transfer — simulate outward transfer to switch, with 3-phase: VALIDATE → SEND → ADVISE
    app.MapPost("/nip/credit-transfer", async ([FromServices] NpgsqlDataSource ds, [FromServices] IProducer<string,string> prod, [FromBody] NipTransfer req, HttpRequest http, CancellationToken ct) =>
    {
        var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
        var key = http.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

        Log.Information("Processing NIP credit transfer: Tenant={Tenant}, Key={Key}, Source={Source}, Dest={Dest}, Amount={Amount}", 
            tenant, key, req.SourceAccountId, req.DestinationAccountId, req.Minor);

        try
        {
            // 1) VALIDATE (balance & risk already in earlier phases; here minimal)
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var c = new NpgsqlCommand("select fn_get_balance(@a)", conn);
            c.Parameters.AddWithValue("a", req.SourceAccountId);
            var bal = (long)(await c.ExecuteScalarAsync(ct) ?? 0);
            
            if (bal < req.Minor) 
            {
                Log.Warning("Insufficient funds for NIP transfer: Account={Account}, Balance={Balance}, Requested={Requested}", 
                    req.SourceAccountId, bal, req.Minor);
                return Results.Problem("insufficient funds", statusCode: 402);
            }

            // 2) LOCK idempotency for (tenant,key)
            var ok = await EnsureIdempotent(conn, tenant, key, ct);
            if (!ok) 
            {
                Log.Information("Duplicate NIP transfer request: Tenant={Tenant}, Key={Key}", tenant, key);
                return Results.Ok(new { duplicate = true, key, status = "DUPLICATE" });
            }

            // 3) SEND to switch (mock: produce to topic; in prod call bank/switch API)
            var payload = System.Text.Json.JsonSerializer.Serialize(new {
                req.SourceAccountId,
                req.DestinationAccountId,
                req.Minor,
                req.Currency,
                req.Narration,
                req.BeneficiaryBank,
                req.BeneficiaryName,
                req.Reference,
                TenantId = tenant,
                IdempotencyKey = key,
                Timestamp = DateTime.UtcNow
            });
            
            await prod.ProduceAsync("nip-out", new Message<string,string>{ Key = key, Value = payload }, ct);

            // 4) Hold amount in suspense (optional) until ADVICE/ACK
            // You can post a pending ledger entry here or use reservations table.

            Log.Information("NIP transfer sent to switch: Key={Key}, Status=PENDING_SEND", key);
            return Results.Accepted(value: new { key, status = "PENDING_SEND", tenant });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing NIP credit transfer: Tenant={Tenant}, Key={Key}", tenant, key);
            return Results.Problem("internal error processing transfer", statusCode: 500);
        }
    });

    // POST /nip/advice — incoming advice/ack from switch confirming success/fail
    app.MapPost("/nip/advice", async ([FromServices] NpgsqlDataSource ds, [FromBody] NipAdvice adv, CancellationToken ct) =>
    {
        Log.Information("Processing NIP advice: Key={Key}, Status={Status}, Reference={Reference}", 
            adv.Key, adv.Status, adv.Reference);

        try
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            
            // finalize ledger: debit source, credit dest (internal or external settlement account)
            if (adv.Status == "SUCCESS")
            {
                // minimal fast path reuse (from earlier phases): call stored proc
                await using var cmd = new NpgsqlCommand("SELECT * FROM sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn);
                cmd.Parameters.AddWithValue("k", adv.Key); 
                cmd.Parameters.AddWithValue("t", adv.TenantId);
                cmd.Parameters.AddWithValue("s", adv.SourceAccountId); 
                cmd.Parameters.AddWithValue("d", adv.DestinationAccountId);
                cmd.Parameters.AddWithValue("m", adv.Minor); 
                cmd.Parameters.AddWithValue("c", adv.Currency);
                cmd.Parameters.AddWithValue("n", $"NIP:{adv.Reference}");
                await cmd.ExecuteScalarAsync(ct);
                
                Log.Information("NIP transfer finalized successfully: Key={Key}, Reference={Reference}", 
                    adv.Key, adv.Reference);
            }
            else
            {
                Log.Warning("NIP transfer failed: Key={Key}, Status={Status}, Reference={Reference}", 
                    adv.Key, adv.Status, adv.Reference);
                // else: release reservation
            }
            
            return Results.Ok(new { ack = true, key = adv.Key, status = adv.Status });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing NIP advice: Key={Key}, Status={Status}", adv.Key, adv.Status);
            return Results.Problem("internal error processing advice", statusCode: 500);
        }
    });

    // GET /nip/status/{key} — check status of a NIP transfer
    app.MapGet("/nip/status/{key}", async ([FromServices] NpgsqlDataSource ds, string key, HttpRequest http, CancellationToken ct) =>
    {
        try
        {
            var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
            await using var conn = await ds.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT seen_at FROM request_keys WHERE key = @k", conn);
            cmd.Parameters.AddWithValue("k", $"{tenant}::{key}");
            var seenAt = await cmd.ExecuteScalarAsync(ct);
            
            if (seenAt == null)
            {
                return Results.NotFound(new { key, status = "NOT_FOUND" });
            }
            
            // In a real system, you'd check a dedicated NIP transaction status table
            // For this mock, we assume if it's seen, it's at least pending.
            // If advice was received, the ledger would be updated.
            return Results.Ok(new { key, status = "PENDING_OR_SUCCESS", seenAt });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking NIP transfer status: Key={Key}", key);
            return Results.Problem("internal error checking status", statusCode: 500);
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

static async Task<bool> EnsureIdempotent(NpgsqlConnection conn, string tenant, string key, CancellationToken ct)
{
    await using var tx = await conn.BeginTransactionAsync(ct);
    try {
        await using var cmd = new NpgsqlCommand("insert into request_keys(key, seen_at) values(@k, now())", conn, tx);
        cmd.Parameters.AddWithValue("k", $"{tenant}::{key}");
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct); 
        return true;
    } catch (PostgresException ex) when (ex.SqlState=="23505") { 
        await tx.RollbackAsync(ct); 
        return false; 
    }
}

public record NipTransfer(
    string SourceAccountId, 
    string DestinationAccountId, 
    long Minor, 
    string Currency, 
    string Narration, 
    string BeneficiaryBank, 
    string BeneficiaryName, 
    string? Reference);

public record NipAdvice(
    string TenantId, 
    string Key, 
    string SourceAccountId, 
    string DestinationAccountId, 
    long Minor, 
    string Currency, 
    string Reference, 
    string Status);

// Mock producer for when Kafka is not available
public class MockProducer : IProducer<string, string>
{
    public void Dispose() { }
    public int Flush(TimeSpan timeout) => 0;
    public void Flush(CancellationToken cancellationToken = default) { }
    public int Poll(TimeSpan timeout) => 0;
    public void InitTransactions(TimeSpan timeout) { }
    public void BeginTransaction() { }
    public void CommitTransaction(TimeSpan timeout) { }
    public void CommitTransaction() { }
    public void AbortTransaction(TimeSpan timeout) { }
    public void AbortTransaction() { }
    public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout) { }
    public int AddBrokers(string brokers) => 0;
    public void SetSaslCredentials(string username, string password) { }
    public Handle Handle => new Handle();
    public string Name => "MockProducer";
    
    public Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message, CancellationToken cancellationToken = default)
    {
        Log.Information("Mock producer: Would send message to topic {Topic} with key {Key}", topic, message.Key);
        return Task.FromResult(new DeliveryResult<string, string> { Topic = topic, Partition = 0, Offset = 0, Message = message });
    }
    public Task<DeliveryResult<string, string>> ProduceAsync(TopicPartition topicPartition, Message<string, string> message, CancellationToken cancellationToken = default)
    {
        Log.Information("Mock producer: Would send message to topic {Topic} with key {Key}", topicPartition.Topic, message.Key);
        return Task.FromResult(new DeliveryResult<string, string> { Topic = topicPartition.Topic, Partition = topicPartition.Partition, Offset = 0, Message = message });
    }
    public void Produce(string topic, Message<string, string> message, Action<DeliveryReport<string, string>> deliveryHandler = null) { }
    public void Produce(TopicPartition topicPartition, Message<string, string> message, Action<DeliveryReport<string, string>> deliveryHandler = null) { }
    public void Produce(string topic, Message<string, string> message, CancellationToken cancellationToken = default) { }
    public void Produce(TopicPartition topicPartition, Message<string, string> message, CancellationToken cancellationToken = default) { }
}
