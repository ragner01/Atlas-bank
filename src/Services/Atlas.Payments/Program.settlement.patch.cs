using System.Net.Http.Json;

// After successful approval (Phase 15) or ledger entry creation, enqueue merchant webhook & fees quote
app.MapPost("/payments/notify-merchant", async (
    IConfiguration cfg,
    HttpRequest http,
    string merchantId,
    string rrn,
    long amountMinor,
    string currency,
    CancellationToken ct) =>
{
    // Call Fees
    using var fees = new HttpClient { BaseAddress = new Uri(cfg["Services:FeesBase"] ?? "http://fees:5701") };
    var quote = await fees.GetFromJsonAsync<dynamic>($"/fees/quote?merchantId={merchantId}&minor={amountMinor}&currency={currency}&network=VISA&mcc=5999", ct);

    // Enqueue webhook
    using var wh = new HttpClient { BaseAddress = new Uri(cfg["Services:WebhooksBase"] ?? "http://webhooks:5703") };
    var body = new { type="payment.captured", merchantId, rrn, amountMinor, currency, fees=quote };
    _ = await wh.PostAsJsonAsync($"/webhooks/enqueue?endpoint={Uri.EscapeDataString(Environment.GetEnvironmentVariable("DEMO_MERCHANT_ENDPOINT") ?? "http://localhost:8080/webhooks")}", body, ct);

    return Results.Accepted();
});
