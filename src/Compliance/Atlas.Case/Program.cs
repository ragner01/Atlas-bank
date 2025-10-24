#nullable disable
using Microsoft.AspNetCore.Mvc;
using Npgsql;
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
    .Enrich.WithProperty("Application", "Atlas.Case")
    .Enrich.WithProperty("Version", "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/case-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AtlasBank Case Management Service");

    var b = WebApplication.CreateBuilder(args);

    // Use Serilog
    b.Host.UseSerilog();

    // Database connection
    var caseDbConnectionString = Environment.GetEnvironmentVariable("CASE_DB") ?? "Host=postgres;Database=atlas;Username=postgres;Password=postgres";
    Log.Information("Connecting to Case Management database");
    b.Services.AddSingleton(new NpgsqlDataSourceBuilder(caseDbConnectionString).Build());

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

    app.MapGet("/health", () => Results.Ok(new { ok = true, service = "Atlas.Case", timestamp = DateTime.UtcNow }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

    // POST /cases — create new AML case
    app.MapPost("/cases", async ([FromServices] NpgsqlDataSource ds, [FromBody] CreateCaseReq req, CancellationToken ct) =>
    {
        Log.Information("Creating new AML case for customer {CustomerId}", req.CustomerId);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO aml_cases(id, customer_id, case_type, priority, status, description, created_at, created_by) 
            VALUES(@i, @c, @t, @p, 'OPEN', @d, now(), @cb)", conn);
        cmd.Parameters.AddWithValue("i", id);
        cmd.Parameters.AddWithValue("c", req.CustomerId);
        cmd.Parameters.AddWithValue("t", req.CaseType);
        cmd.Parameters.AddWithValue("p", req.Priority);
        cmd.Parameters.AddWithValue("d", req.Description);
        cmd.Parameters.AddWithValue("cb", req.CreatedBy ?? "system");
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("AML case {CaseId} created for customer {CustomerId}", id, req.CustomerId);
        
        return Results.Ok(new { 
            case_id = id, 
            status = "OPEN",
            created_at = DateTime.UtcNow
        });
    });

    // GET /cases — list cases with optional filters
    app.MapGet("/cases", async (
        [FromServices] NpgsqlDataSource ds,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? case_type = null,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20) =>
    {
        Log.Information("Retrieving AML cases with filters: Status={Status}, Priority={Priority}, Type={Type}, Page={Page}, PageSize={PageSize}", 
            status, priority, case_type, page, page_size);
        
        await using var conn = await ds.OpenConnectionAsync();
        
        var whereClause = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        
        if (!string.IsNullOrEmpty(status))
        {
            whereClause.Add("status = @status");
            parameters.Add(new NpgsqlParameter("status", status));
        }
        
        if (!string.IsNullOrEmpty(priority))
        {
            whereClause.Add("priority = @priority");
            parameters.Add(new NpgsqlParameter("priority", priority));
        }
        
        if (!string.IsNullOrEmpty(case_type))
        {
            whereClause.Add("case_type = @case_type");
            parameters.Add(new NpgsqlParameter("case_type", case_type));
        }
        
        var whereSql = whereClause.Count > 0 ? "WHERE " + string.Join(" AND ", whereClause) : "";
        var offset = (page - 1) * page_size;
        
        await using var cmd = new NpgsqlCommand($@"
            SELECT id, customer_id, case_type, priority, status, description, created_at, created_by, updated_at, updated_by
            FROM aml_cases 
            {whereSql}
            ORDER BY created_at DESC 
            LIMIT @limit OFFSET @offset", conn);
        
        cmd.Parameters.AddWithValue("limit", page_size);
        cmd.Parameters.AddWithValue("offset", offset);
        cmd.Parameters.AddRange(parameters.ToArray());
        
        var cases = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        
        while (await r.ReadAsync())
        {
            cases.Add(new
            {
                id = r.GetGuid(0),
                customer_id = r.GetString(1),
                case_type = r.GetString(2),
                priority = r.GetString(3),
                status = r.GetString(4),
                description = r.GetString(5),
                created_at = r.GetDateTime(6),
                created_by = r.GetString(7),
                updated_at = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                updated_by = r.IsDBNull(9) ? (string?)null : r.GetString(9)
            });
        }
        
        Log.Information("Retrieved {Count} AML cases", cases.Count);
        
        return Results.Ok(new { 
            cases,
            page,
            page_size,
            total_returned = cases.Count
        });
    });

    // GET /cases/{id} — get specific case
    app.MapGet("/cases/{id:guid}", async (Guid id, [FromServices] NpgsqlDataSource ds) =>
    {
        Log.Information("Retrieving AML case {CaseId}", id);
        
        await using var conn = await ds.OpenConnectionAsync();
        
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, customer_id, case_type, priority, status, description, created_at, created_by, updated_at, updated_by
            FROM aml_cases 
            WHERE id = @i", conn);
        cmd.Parameters.AddWithValue("i", id);
        
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) 
        {
            Log.Warning("AML case {CaseId} not found", id);
            return Results.NotFound(new { error = "Case not found" });
        }

        var result = new
        {
            id = r.GetGuid(0),
            customer_id = r.GetString(1),
            case_type = r.GetString(2),
            priority = r.GetString(3),
            status = r.GetString(4),
            description = r.GetString(5),
            created_at = r.GetDateTime(6),
            created_by = r.GetString(7),
            updated_at = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
            updated_by = r.IsDBNull(9) ? (string?)null : r.GetString(9)
        };

        Log.Information("Retrieved AML case {CaseId}: {Status}", id, result.status);
        
        return Results.Ok(result);
    });

    // PUT /cases/{id}/status — update case status
    app.MapPut("/cases/{id:guid}/status", async (Guid id, [FromServices] NpgsqlDataSource ds, [FromBody] UpdateCaseStatusReq req, CancellationToken ct) =>
    {
        Log.Information("Updating AML case {CaseId} status to {Status}", id, req.Status);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Validate status transition
        var validTransitions = new Dictionary<string, string[]>
        {
            ["OPEN"] = new[] { "INVESTIGATING", "CLOSED" },
            ["INVESTIGATING"] = new[] { "RESOLVED", "ESCALATED", "CLOSED" },
            ["ESCALATED"] = new[] { "INVESTIGATING", "CLOSED" },
            ["RESOLVED"] = new[] { "CLOSED" },
            ["CLOSED"] = new string[0] // Terminal state
        };

        // Get current status
        await using (var getCmd = new NpgsqlCommand("SELECT status FROM aml_cases WHERE id = @i", conn))
        {
            getCmd.Parameters.AddWithValue("i", id);
            var currentStatus = await getCmd.ExecuteScalarAsync(ct) as string;
            
            if (currentStatus == null)
            {
                Log.Warning("AML case {CaseId} not found for status update", id);
                return Results.NotFound(new { error = "Case not found" });
            }
            
            if (!validTransitions.TryGetValue(currentStatus, out var allowedTransitions) || 
                !allowedTransitions.Contains(req.Status))
            {
                Log.Warning("Invalid status transition from {CurrentStatus} to {NewStatus} for case {CaseId}", 
                    currentStatus, req.Status, id);
                return Results.BadRequest(new { 
                    error = $"Invalid status transition from {currentStatus} to {req.Status}",
                    current_status = currentStatus,
                    allowed_transitions = allowedTransitions
                });
            }
        }
        
        await using var cmd = new NpgsqlCommand(@"
            UPDATE aml_cases 
            SET status = @s, updated_at = now(), updated_by = @ub
            WHERE id = @i", conn);
        cmd.Parameters.AddWithValue("i", id);
        cmd.Parameters.AddWithValue("s", req.Status);
        cmd.Parameters.AddWithValue("ub", req.UpdatedBy ?? "system");
        
        var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
        
        if (rowsAffected == 0)
        {
            Log.Warning("No rows affected when updating AML case {CaseId} status", id);
            return Results.NotFound(new { error = "Case not found" });
        }
        
        Log.Information("AML case {CaseId} status updated to {Status}", id, req.Status);
        
        return Results.Ok(new { 
            case_id = id,
            status = req.Status,
            updated_at = DateTime.UtcNow,
            updated_by = req.UpdatedBy ?? "system"
        });
    });

    // POST /cases/{id}/notes — add note to case
    app.MapPost("/cases/{id:guid}/notes", async (Guid id, [FromServices] NpgsqlDataSource ds, [FromBody] AddNoteReq req, CancellationToken ct) =>
    {
        Log.Information("Adding note to AML case {CaseId}", id);
        
        await using var conn = await ds.OpenConnectionAsync(ct);
        
        // Verify case exists
        await using (var checkCmd = new NpgsqlCommand("SELECT id FROM aml_cases WHERE id = @i", conn))
        {
            checkCmd.Parameters.AddWithValue("i", id);
            var caseExists = await checkCmd.ExecuteScalarAsync(ct);
            
            if (caseExists == null)
            {
                Log.Warning("AML case {CaseId} not found when adding note", id);
                return Results.NotFound(new { error = "Case not found" });
            }
        }
        
        var noteId = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO aml_case_notes(id, case_id, note, created_at, created_by) 
            VALUES(@ni, @ci, @n, now(), @cb)", conn);
        cmd.Parameters.AddWithValue("ni", noteId);
        cmd.Parameters.AddWithValue("ci", id);
        cmd.Parameters.AddWithValue("n", req.Note);
        cmd.Parameters.AddWithValue("cb", req.CreatedBy ?? "system");
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        Log.Information("Note {NoteId} added to AML case {CaseId}", noteId, id);
        
        return Results.Ok(new { 
            note_id = noteId,
            case_id = id,
            note = req.Note,
            created_at = DateTime.UtcNow,
            created_by = req.CreatedBy ?? "system"
        });
    });

    // GET /cases/{id}/notes — get case notes
    app.MapGet("/cases/{id:guid}/notes", async (Guid id, [FromServices] NpgsqlDataSource ds) =>
    {
        Log.Information("Retrieving notes for AML case {CaseId}", id);
        
        await using var conn = await ds.OpenConnectionAsync();
        
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, note, created_at, created_by
            FROM aml_case_notes 
            WHERE case_id = @i
            ORDER BY created_at DESC", conn);
        cmd.Parameters.AddWithValue("i", id);
        
        var notes = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        
        while (await r.ReadAsync())
        {
            notes.Add(new
            {
                id = r.GetGuid(0),
                note = r.GetString(1),
                created_at = r.GetDateTime(2),
                created_by = r.GetString(3)
            });
        }
        
        Log.Information("Retrieved {Count} notes for AML case {CaseId}", notes.Count, id);
        
        return Results.Ok(new { 
            case_id = id,
            notes,
            total_notes = notes.Count
        });
    });

    // GET /cases/stats — get case statistics
    app.MapGet("/cases/stats", async ([FromServices] NpgsqlDataSource ds) =>
    {
        Log.Information("Retrieving AML case statistics");
        
        await using var conn = await ds.OpenConnectionAsync();
        
        await using var cmd = new NpgsqlCommand(@"
            SELECT 
                status,
                COUNT(*) as count
            FROM aml_cases 
            GROUP BY status
            ORDER BY count DESC", conn);
        
        var stats = new Dictionary<string, int>();
        await using var r = await cmd.ExecuteReaderAsync();
        
        while (await r.ReadAsync())
        {
            stats[r.GetString(0)] = r.GetInt32(1);
        }
        
        var totalCases = stats.Values.Sum();
        
        Log.Information("Retrieved AML case statistics: Total={Total}, Stats={Stats}", totalCases, string.Join(", ", stats.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
        
        return Results.Ok(new { 
            total_cases = totalCases,
            by_status = stats,
            generated_at = DateTime.UtcNow
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

// Data models
public record CreateCaseReq(
    string CustomerId,
    string CaseType,
    string Priority,
    string Description,
    string? CreatedBy = null
);

public record UpdateCaseStatusReq(
    string Status,
    string? UpdatedBy = null
);

public record AddNoteReq(
    string Note,
    string? CreatedBy = null
);
