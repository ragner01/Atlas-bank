using StackExchange.Redis;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "redis:6379"));

builder.Services.AddHttpClient();
builder.Services.AddHostedService<ShadowTrafficService>();

builder.Build().Run();

/// <summary>
/// Service that shadows live traffic to DR environment for validation
/// </summary>
public sealed class ShadowTrafficService : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConnectionMultiplexer _redis;
    
    private readonly string _sourceEndpoint;
    private readonly string _destinationEndpoint;
    private readonly double _shadowRate;
    private readonly string _redisKeyPrefix;

    public ShadowTrafficService(
        IHttpClientFactory httpFactory,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _httpFactory = httpFactory;
        _redis = redis;
        
        _sourceEndpoint = configuration["SHADOW_SRC"] ?? "ledgerapi:6181";
        _destinationEndpoint = configuration["SHADOW_DST"] ?? "httpbin.org:80";
        _shadowRate = double.TryParse(configuration["SHADOW_RATE"], out var rate) ? rate : 0.05;
        _redisKeyPrefix = configuration["REDIS_KEY_PREFIX"] ?? "shadow";
        
        Console.WriteLine($"🌗 Shadowing {_shadowRate:P1} traffic from {_sourceEndpoint} -> {_destinationEndpoint}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var random = new Random();
        var httpClient = _httpFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (random.NextDouble() < _shadowRate)
                {
                    await PerformShadowRequest(httpClient, stoppingToken);
                }
                
                await Task.Delay(200, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in shadow traffic service: {ex.Message}");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task PerformShadowRequest(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            // Get metrics from source
            var sourceResponse = await client.GetAsync($"http://{_sourceEndpoint}/health", cancellationToken);
            sourceResponse.EnsureSuccessStatusCode();
            
            var sourceContent = await sourceResponse.Content.ReadAsStringAsync(cancellationToken);
            
            // Send to destination (simplified)
            var destinationResponse = await client.GetAsync($"http://{_destinationEndpoint}/get", cancellationToken);
            var destinationContent = await destinationResponse.Content.ReadAsStringAsync(cancellationToken);
            
            // Store comparison results in Redis
            await StoreComparisonResult(sourceContent, destinationContent, cancellationToken);
            
            Console.WriteLine("Shadow request completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Shadow request failed: {ex.Message}");
        }
    }

    private async Task StoreComparisonResult(
        string sourceContent, 
        string destinationContent, 
        CancellationToken cancellationToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            var timestamp = DateTime.UtcNow;
            var key = $"{_redisKeyPrefix}:comparison:{timestamp:yyyyMMddHHmmss}";
            
            var comparison = new
            {
                timestamp,
                source = sourceContent,
                destination = destinationContent,
                matches = sourceContent == destinationContent,
                driftDetected = sourceContent != destinationContent
            };
            
            var json = JsonSerializer.Serialize(comparison);
            await db.StringSetAsync(key, json, TimeSpan.FromHours(24));
            
            if (comparison.driftDetected)
            {
                Console.WriteLine($"🚨 Drift detected between source and destination at {timestamp}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to store comparison result: {ex.Message}");
        }
    }
}