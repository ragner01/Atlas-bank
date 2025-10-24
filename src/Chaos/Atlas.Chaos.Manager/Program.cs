using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Polly;
using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379"));

builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Health check endpoint
app.MapHealthChecks("/health");

// In-memory chaos registry
var chaos = new Dictionary<string, ChaosState>();
var lockObject = new object();

// Enable chaos for a service
app.MapPost("/chaos/enable", ([FromBody] ChaosState state) =>
{
    lock (lockObject)
    {
        chaos[state.Service] = state;
        Console.WriteLine($"Chaos enabled for service {state.Service} with mode {state.Mode}");
    }
    
    return Results.Ok(new { 
        status = "enabled", 
        service = state.Service, 
        mode = state.Mode,
        timestamp = DateTime.UtcNow
    });
});

// Disable chaos for a service
app.MapPost("/chaos/disable", ([FromBody] string service) =>
{
    lock (lockObject)
    {
        var removed = chaos.Remove(service);
        Console.WriteLine($"Chaos disabled for service {service}, removed: {removed}");
    }
    
    return Results.Ok(new { 
        status = "disabled", 
        service,
        timestamp = DateTime.UtcNow
    });
});

// List all active chaos states
app.MapGet("/chaos/list", () =>
{
    lock (lockObject)
    {
        return Results.Ok(chaos.Values.Select(s => new {
            s.Service,
            s.Mode,
            s.FailureRate,
            s.DelayMs,
            s.RetryCount,
            s.TargetUrl,
            enabledAt = DateTime.UtcNow
        }));
    }
});

// Inject chaos into a service
app.MapGet("/chaos/inject", async ([FromServices] IHttpClientFactory httpFactory, string service) =>
{
    ChaosState? state;
    lock (lockObject)
    {
        if (!chaos.TryGetValue(service, out state))
        {
            Console.WriteLine($"No chaos state found for service {service}");
            return Results.NotFound(new { error = $"No chaos state found for service {service}" });
        }
    }

    var targetUrl = state.TargetUrl ?? $"http://{service}:8080/health";
    var client = httpFactory.CreateClient();
    
    // Configure Polly policy based on chaos state
    var policy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(
            state.RetryCount, 
            retryAttempt => TimeSpan.FromMilliseconds(state.DelayMs * Math.Pow(2, retryAttempt - 1))
        );

    try
    {
        Console.WriteLine($"Injecting chaos for service {service} with mode {state.Mode}");
        
        var result = await policy.ExecuteAsync(async () =>
        {
            // Inject latency
            if (state.Mode == "latency")
            {
                await Task.Delay(state.DelayMs);
                Console.WriteLine($"Injected {state.DelayMs}ms latency for service {service}");
            }
            
            // Inject failure
            if (state.Mode == "failure" && new Random().NextDouble() < state.FailureRate)
            {
                Console.WriteLine($"Injecting failure for service {service} with rate {state.FailureRate}");
                throw new Exception($"Injected failure for service {service}");
            }
            
            // Make the actual request
            var response = await client.GetAsync(targetUrl);
            response.EnsureSuccessStatusCode();
            
            return response;
        });
        
        Console.WriteLine($"Chaos injection successful for service {service}");
        return Results.Ok(new { 
            ok = true, 
            mode = state.Mode,
            service,
            targetUrl,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Chaos injection failed for service {service}: {ex.Message}");
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: $"Chaos injection failed for {service}"
        );
    }
});

// Get chaos statistics
app.MapGet("/chaos/stats", () =>
{
    lock (lockObject)
    {
        var stats = chaos.Values.GroupBy(s => s.Mode)
            .ToDictionary(g => g.Key, g => g.Count());
            
        return Results.Ok(new {
            totalActiveChaos = chaos.Count,
            modeBreakdown = stats,
            services = chaos.Keys.ToArray(),
            timestamp = DateTime.UtcNow
        });
    }
});

// Bulk chaos operations
app.MapPost("/chaos/bulk-enable", ([FromBody] ChaosState[] states) =>
{
    lock (lockObject)
    {
        var results = new List<object>();
        
        foreach (var state in states)
        {
            chaos[state.Service] = state;
            results.Add(new { 
                service = state.Service, 
                mode = state.Mode, 
                status = "enabled" 
            });
            Console.WriteLine($"Bulk chaos enabled for service {state.Service}");
        }
        
        return Results.Ok(new { 
            enabled = results.Count,
            results,
            timestamp = DateTime.UtcNow
        });
    }
});

app.MapPost("/chaos/bulk-disable", ([FromBody] string[] services) =>
{
    lock (lockObject)
    {
        var results = new List<object>();
        
        foreach (var service in services)
        {
            var removed = chaos.Remove(service);
            results.Add(new { 
                service, 
                status = removed ? "disabled" : "not_found" 
            });
            Console.WriteLine($"Bulk chaos disabled for service {service}");
        }
        
        return Results.Ok(new { 
            processed = results.Count,
            results,
            timestamp = DateTime.UtcNow
        });
    }
});

app.Run();

/// <summary>
/// Represents a chaos state for a service
/// </summary>
/// <param name="Service">The service name to apply chaos to</param>
/// <param name="Mode">The chaos mode: 'latency' or 'failure'</param>
/// <param name="FailureRate">Failure rate (0.0 to 1.0) for failure mode</param>
/// <param name="DelayMs">Delay in milliseconds for latency mode</param>
/// <param name="RetryCount">Number of retries for the Polly policy</param>
/// <param name="TargetUrl">Optional target URL to test against</param>
public record ChaosState(
    string Service, 
    string Mode, 
    double FailureRate, 
    int DelayMs, 
    int RetryCount, 
    string? TargetUrl = null
);
