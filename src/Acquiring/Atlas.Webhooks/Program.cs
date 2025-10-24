using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

var b = WebApplication.CreateBuilder(args);
b.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
var app = b.Build();

app.MapGet("/health", () => Results.Ok(new { status="ok" }));

// Set secret per endpoint
app.MapPost("/webhooks/secret", async (IConnectionMultiplexer mux, string endpoint, string secret) =>
{
    await mux.GetDatabase().StringSetAsync($"wh:secret:{endpoint}", secret);
    return Results.Ok();
});

// Enqueue webhook (used by Payments/Settlement/etc.)
app.MapPost("/webhooks/enqueue", async (IConnectionMultiplexer mux, [FromBody] object body, string endpoint) =>
{
    var db = mux.GetDatabase();
    await db.ListLeftPushAsync("wh:q", new RedisValue(System.Text.Json.JsonSerializer.Serialize(new { endpoint, body })));
    return Results.Accepted();
});

// Worker loop (simple inline endpoint for dev — in prod run as BackgroundService)
app.MapPost("/webhooks/dispatch-once", async (IConnectionMultiplexer mux, CancellationToken ct) =>
{
    var db = mux.GetDatabase();
    var item = await db.ListRightPopAsync("wh:q");
    if (item.IsNullOrEmpty) return Results.Ok(new { dispatched = 0 });

    var payload = item.ToString();
    var doc = System.Text.Json.JsonDocument.Parse(payload).RootElement;
    var endpoint = doc.GetProperty("endpoint").GetString()!;
    var body = doc.GetProperty("body").GetRawText();

    var secret = await db.StringGetAsync($"wh:secret:{endpoint}");
    var sig = Sign(body, secret.HasValue ? secret.ToString()! : "dev");
    using var http = new HttpClient();
    var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
    req.Headers.Add("X-Webhook-Signature", sig);
    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
    var res = await http.SendAsync(req, ct);
    if (!res.IsSuccessStatusCode)
    {
        // push to retry list with backoff tag
        await db.ListLeftPushAsync("wh:retry", item);
    }
    return Results.Ok(new { dispatched = 1, status = (int)res.StatusCode });

    static string Sign(string body, string secret)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
});

app.Run();
