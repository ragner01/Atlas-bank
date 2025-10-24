// Enforcement wrapper around existing endpoints (/payments/transfers/with-risk and /cnp/charge)
using System.Net.Http.Json;

/// <summary>
/// Enforces limits on payment requests before processing
/// </summary>
/// <param name="http">HTTP request context</param>
/// <param name="cfg">Configuration</param>
/// <param name="actorId">Actor identifier</param>
/// <param name="deviceId">Device identifier</param>
/// <param name="ip">IP address</param>
/// <param name="merchantId">Merchant identifier</param>
/// <param name="currency">Currency code</param>
/// <param name="minor">Amount in minor units</param>
/// <param name="mcc">Merchant category code</param>
/// <param name="next">Next handler to execute if limits pass</param>
/// <param name="ct">Cancellation token</param>
/// <returns>Result from limits check or next handler</returns>
static async Task<IResult> EnforceAsync(HttpRequest http, IConfiguration cfg, string actorId, string? deviceId, string? ip, string? merchantId, string currency, long minor, string mcc, Func<Task<IResult>> next, CancellationToken ct)
{
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    var limitsBase = cfg["Services:LimitsBase"] ?? Environment.GetEnvironmentVariable("LIMITS_BASE") ?? "http://limits:5901";
    
    using var client = new HttpClient { BaseAddress = new Uri(limitsBase) };
    var localIso = http.Headers["X-Local-Time"].FirstOrDefault();
    var lat = http.Headers["X-Lat"].FirstOrDefault();
    var lng = http.Headers["X-Lng"].FirstOrDefault();

    var req = new
    {
        TenantId = tenant,
        ActorId = actorId,
        DeviceId = deviceId,
        Ip = ip,
        MerchantId = merchantId,
        Currency = currency,
        Minor = minor,
        Mcc = mcc,
        Lat = lat is null ? null : double.Parse(lat),
        Lng = lng is null ? null : double.Parse(lng),
        LocalTimeIso = localIso
    };

    try
    {
        var res = await client.PostAsJsonAsync("/limits/check", req, ct);
        var dec = await res.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct);

        if (dec is not null && dec.TryGetValue("allowed", out var ok) && ok is bool allowed && !allowed)
        {
            var action = dec["action"]?.ToString();
            var reason = dec["reason"]?.ToString();
            
            if (action == "HARD_BLOCK")
            {
                return Results.Problem($"blocked by limits: {reason}", statusCode: 403);
            }
            
            if (action == "SOFT_REVIEW")
            {
                http.HttpContext.Response.Headers["X-Limit-Review"] = "true";
                http.HttpContext.Response.Headers["X-Limit-Reason"] = reason ?? "velocity";
                // fallthrough to proceed but marked for review
            }
        }
    }
    catch (Exception ex)
    {
        // Log error but don't block transaction if limits service is down
        Console.WriteLine($"Limits service error: {ex.Message}");
        http.HttpContext.Response.Headers["X-Limit-Error"] = "service_unavailable";
    }

    return await next();
}

// Wrap existing routes by example (you can apply same pattern to others)
app.MapPost("/payments/cnp/charge/enforced", async (
    HttpRequest http,
    IConfiguration cfg,
    LedgerService.LedgerServiceClient ledger,
    long amountMinor,
    string currency,
    string cardToken,
    string merchantId,
    string mcc,
    CancellationToken ct) =>
{
    return await EnforceAsync(http, cfg,
        actorId: $"card::{cardToken}",
        deviceId: http.Headers["X-Device-Id"].FirstOrDefault(),
        ip: http.Headers["X-Ip"].FirstOrDefault(),
        merchantId: merchantId,
        currency: currency, 
        minor: amountMinor, 
        mcc: mcc,
        next: async () =>
        {
            // call your existing /payments/cnp/charge path (reuse implementation)
            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5191") }; // adjust if needed
            var res = await client.PostAsync($"/payments/cnp/charge?amountMinor={amountMinor}&currency={currency}&cardToken={cardToken}&merchantId={merchantId}&mcc={mcc}", null, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return Results.Content(body, res.Content.Headers.ContentType?.ToString() ?? "application/json", res.StatusCode);
        }, ct);
})
.WithName("ChargeCardEnforced")
.WithTags("Payments")
.WithSummary("Charge card with limits enforcement")
.WithDescription("Processes a card charge with limits enforcement applied");

// Additional enforced endpoint for transfers
app.MapPost("/payments/transfers/enforced", async (
    HttpRequest http,
    IConfiguration cfg,
    LedgerService.LedgerServiceClient ledger,
    long amountMinor,
    string currency,
    string fromAccount,
    string toAccount,
    string merchantId,
    string mcc,
    CancellationToken ct) =>
{
    return await EnforceAsync(http, cfg,
        actorId: $"account::{fromAccount}",
        deviceId: http.Headers["X-Device-Id"].FirstOrDefault(),
        ip: http.Headers["X-Ip"].FirstOrDefault(),
        merchantId: merchantId,
        currency: currency,
        minor: amountMinor,
        mcc: mcc,
        next: async () =>
        {
            // call your existing transfer implementation
            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5191") };
            var res = await client.PostAsync($"/payments/transfers?amountMinor={amountMinor}&currency={currency}&fromAccount={fromAccount}&toAccount={toAccount}&merchantId={merchantId}&mcc={mcc}", null, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return Results.Content(body, res.Content.Headers.ContentType?.ToString() ?? "application/json", res.StatusCode);
        }, ct);
})
.WithName("TransferEnforced")
.WithTags("Payments")
.WithSummary("Transfer with limits enforcement")
.WithDescription("Processes a transfer with limits enforcement applied");

