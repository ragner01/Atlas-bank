// Load shedding + balance invalidation + realtime push
using StackExchange.Redis;
using Confluent.Kafka;
using System.Text.Json;
using System.Diagnostics;

// At startup:
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var cfg = new ProducerConfig 
    { 
        BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP") ?? "redpanda:9092", 
        Acks = Acks.Leader,
        Retries = 3,
        RetryBackoffMs = 100,
        RequestTimeoutMs = 30000
    };
    return new ProducerBuilder<string, string>(cfg).Build();
});

// Simple token-bucket load-shedding middleware (protect hot path)
var bucket = new TokenBucket(
    ratePerSec: int.Parse(Environment.GetEnvironmentVariable("LS_RATE_PER_SEC") ?? "400"),
    burst: int.Parse(Environment.GetEnvironmentVariable("LS_BURST") ?? "800")
);

app.Use(async (ctx, next) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    
    if (!bucket.TryTake())
    {
        logger.LogWarning("Load shedding triggered for {Path} from {RemoteIp}", 
            ctx.Request.Path, ctx.Connection.RemoteIpAddress);
        
        ctx.Response.Headers["Retry-After"] = "1";
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsync("Service temporarily unavailable - please retry");
        return;
    }
    
    await next();
});

// Add load shedding statistics endpoint
app.MapGet("/payments/load-shedding/stats", () =>
{
    return Results.Ok(new
    {
        tokensAvailable = bucket.GetAvailableTokens(),
        ratePerSecond = bucket.RatePerSecond,
        burstCapacity = bucket.BurstCapacity,
        timestamp = DateTime.UtcNow
    });
});

// Helper function to invalidate cache and push realtime updates
async Task InvalidateAndPushAsync(string src, string dst, long minor, string currency, IServiceProvider services)
{
    try
    {
        var mux = services.GetRequiredService<IConnectionMultiplexer>();
        var db = mux.GetDatabase();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        // Invalidate cache for both accounts
        await db.PublishAsync("balance:invalidate", src);
        await db.PublishAsync("balance:invalidate", dst);
        
        logger.LogDebug("Cache invalidated for accounts {SrcAccount} and {DstAccount}", src, dst);

        // Push realtime updates
        var prod = services.GetRequiredService<IProducer<string, string>>();
        var topic = Environment.GetEnvironmentVariable("TOPIC_BALANCE_UPDATES") ?? "balance-updates";
        
        var msgSrc = JsonSerializer.Serialize(new 
        { 
            accountId = src, 
            minor = await GetBalance(src, services), 
            currency, 
            pendingMinor = 0L 
        });
        
        var msgDst = JsonSerializer.Serialize(new 
        { 
            accountId = dst, 
            minor = await GetBalance(dst, services), 
            currency, 
            pendingMinor = 0L 
        });
        
        await prod.ProduceAsync(topic, new Message<string, string> { Key = src, Value = msgSrc });
        await prod.ProduceAsync(topic, new Message<string, string> { Key = dst, Value = msgDst });
        
        logger.LogDebug("Realtime balance updates pushed for accounts {SrcAccount} and {DstAccount}", src, dst);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error invalidating cache and pushing realtime updates");
    }
}

async Task<long> GetBalance(string acct, IServiceProvider services)
{
    try
    {
        // Query Ledger API (cached will be invalidated, but we want fresh-ish number for push)
        using var http = new HttpClient 
        { 
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("LEDGER_BASE") ?? "http://ledgerapi:6181"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        var response = await http.GetStringAsync($"/ledger/accounts/{Uri.EscapeDataString(acct)}/balance/global?currency=NGN");
        
        using var document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("availableMinor").GetInt64();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error fetching balance for account {AccountId}", acct);
        return 0;
    }
}

// Token bucket helper
sealed class TokenBucket
{
    private readonly double _rate;
    private readonly double _burst;
    private double _tokens;
    private long _lastTick;
    private readonly object _lock = new object();

    public TokenBucket(double ratePerSec, double burst)
    {
        _rate = ratePerSec;
        _burst = burst;
        _tokens = burst;
        _lastTick = Stopwatch.GetTimestamp();
    }

    public double RatePerSecond => _rate;
    public double BurstCapacity => _burst;

    public bool TryTake()
    {
        lock (_lock)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _lastTick) / (double)Stopwatch.Frequency;
            _lastTick = now;
            _tokens = Math.Min(_burst, _tokens + elapsed * _rate);
            
            if (_tokens >= 1)
            {
                _tokens -= 1;
                return true;
            }
            return false;
        }
    }

    public double GetAvailableTokens()
    {
        lock (_lock)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _lastTick) / (double)Stopwatch.Frequency;
            return Math.Min(_burst, _tokens + elapsed * _rate);
        }
    }
}
