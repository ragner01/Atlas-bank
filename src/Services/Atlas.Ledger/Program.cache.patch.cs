// Balance endpoint caching + precise invalidation via Redis Pub/Sub
using StackExchange.Redis;
using System.Text.Json;

// Add at top-level builder (Program.cs):
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));

// Add Redis health check
builder.Services.AddHealthChecks()
    .AddRedis(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379", name: "redis");

// In the app endpoints section, replace or wrap the existing GET balance endpoint:
app.MapGet("/ledger/accounts/{accountId}/balance/global", async (string accountId, IConnectionMultiplexer mux, ILogger<Program> logger) =>
{
    try
    {
        var db = mux.GetDatabase();
        var key = $"bal:{accountId}";
        var cached = await db.StringGetAsync(key);
        
        if (cached.HasValue)
        {
            logger.LogDebug("Cache hit for account {AccountId}", accountId);
            return Results.Content(cached!, "application/json");
        }

        logger.LogDebug("Cache miss for account {AccountId}, fetching from ledger core", accountId);

        // Call existing balance compute (assume BalanceService.GetBalanceJson(accountId))
        var http = new HttpClient 
        { 
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("LEDGER_INTERNAL") ?? "http://ledgercore:6182"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        var res = await http.GetAsync($"/_internal/balance?accountId={Uri.EscapeDataString(accountId)}");
        
        if (!res.IsSuccessStatusCode)
        {
            logger.LogError("Failed to fetch balance from ledger core for account {AccountId}: {StatusCode}", 
                accountId, res.StatusCode);
            return Results.Problem(await res.Content.ReadAsStringAsync(), statusCode: (int)res.StatusCode);
        }
        
        var body = await res.Content.ReadAsStringAsync();

        // Short TTL cache (2–5s is plenty) — invalidation will nuke cache even sooner
        await db.StringSetAsync(key, body, TimeSpan.FromSeconds(5));
        
        logger.LogDebug("Cached balance for account {AccountId}", accountId);
        return Results.Content(body, "application/json");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching balance for account {AccountId}", accountId);
        return Results.Problem("Internal server error", statusCode: 500);
    }
});

// Background subscriber to invalidate cache on posting events
_ = Task.Run(async () =>
{
    try
    {
        var mux = app.Services.GetRequiredService<IConnectionMultiplexer>();
        var sub = mux.GetSubscriber();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Starting Redis cache invalidation subscriber");
        
        await sub.SubscribeAsync("balance:invalidate", async (ch, payload) =>
        {
            try
            {
                var db = mux.GetDatabase();
                var acct = payload.ToString();
                var key = $"bal:{acct}";
                
                var deleted = await db.KeyDeleteAsync(key);
                logger.LogDebug("Cache invalidated for account {AccountId}, deleted: {Deleted}", acct, deleted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error invalidating cache for account {AccountId}", payload);
            }
        });
        
        logger.LogInformation("Redis cache invalidation subscriber started");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to start Redis cache invalidation subscriber");
    }
});

// Add cache statistics endpoint
app.MapGet("/ledger/cache/stats", async (IConnectionMultiplexer mux) =>
{
    try
    {
        var db = mux.GetDatabase();
        var server = mux.GetServer(mux.GetEndPoints().First());
        
        var info = await server.InfoAsync("memory");
        var keyspace = await server.InfoAsync("keyspace");
        
        return Results.Ok(new
        {
            memory = info.FirstOrDefault(x => x.Key == "used_memory_human")?.Value,
            keyspace = keyspace.FirstOrDefault(x => x.Key.StartsWith("db0"))?.Value,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem("Failed to get cache statistics", statusCode: 500);
    }
});

