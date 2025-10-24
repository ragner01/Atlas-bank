using System.Net.Http.Json;

// New endpoint for CNP using card token (PAN-less). Payments stays OUTSIDE the CDE.
// Calls Vault (PCI zone) for authorization; only receives decision + auth refs.
app.MapPost("/payments/cnp/charge", async (
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
    var tenant = http.Headers["X-Tenant-Id"].FirstOrDefault() ?? "tnt_demo";
    
    // 1) Call vault for auth (mTLS/intra-private network in prod)
    using var client = new HttpClient { BaseAddress = new Uri(cfg["Services:VaultBase"] ?? Environment.GetEnvironmentVariable("VAULT_BASE") ?? "http://cardsvault:5555") };
    var authRes = await client.PostAsJsonAsync("/vault/authorize", new { 
        CardToken = cardToken, 
        AmountMinor = amountMinor, 
        Currency = currency, 
        MerchantId = merchantId, 
        Mcc = mcc 
    }, ct);
    
    if (!authRes.IsSuccessStatusCode) 
        return Results.Problem(await authRes.Content.ReadAsStringAsync(ct), statusCode: (int)authRes.StatusCode);
    
    var payload = await authRes.Content.ReadFromJsonAsync<AuthDto>(cancellationToken: ct);
    if (payload is null || payload.approved == false) 
        return Results.Problem("declined", statusCode: 402);

    // 2) Post to ledger (fast path) — merchant settlement (customer → merchant)
    var res = await ledger.PostEntryAsync(new PostEntryRequest
    {
        SourceAccountId = $"card::{cardToken}", // logical card funding source or cardholder account
        DestinationAccountId = $"merchant::{merchantId}",
        Amount = new Money { Minor = amountMinor, Currency = currency, Scale = 2 },
        Narration = $"CNPAUTH {payload.auth_code}/{payload.rrn}",
        TenantId = tenant
    }, cancellationToken: ct);

    return Results.Accepted(value: new { 
        status = res.Status, 
        entryId = res.EntryId, 
        auth = payload.auth_code, 
        rrn = payload.rrn,
        network = payload.network,
        last4 = payload.last4
    });
});

record AuthDto(bool approved, string auth_code, string rrn, string network, string last4);
