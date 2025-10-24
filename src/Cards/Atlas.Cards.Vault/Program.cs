using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var b = WebApplication.CreateBuilder(args);

// IMPORTANT: This service is PCI-scoped (CDE). It must run in an isolated network/namespace.
string cs = Environment.GetEnvironmentVariable("CARDS_DB") ?? "Host=postgres;Port=5432;Database=atlas_bank;Username=atlas;Password=atlas123";
var ds = new NpgsqlDataSourceBuilder(cs).Build();
b.Services.AddSingleton(ds);
b.Services.AddSingleton<IHsmClient, MockHsm>(); // Swap with real HSM/AKV MHSM in prod
b.Services.AddSingleton<CardCrypto>();
b.Services.AddEndpointsApiExplorer();
b.Services.AddOpenApi();

var app = b.Build();
app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

// Tokenize PAN (PCI zone). Returns PAN-less token to be used outside CDE.
app.MapPost("/vault/tokenize", async ([FromServices] NpgsqlDataSource ds, [FromServices] CardCrypto crypto, [FromBody] TokenizeRequest req, CancellationToken ct) =>
{
    // Basic validation
    if (!Luhn.IsValid(req.Pan) || string.IsNullOrWhiteSpace(req.ExpiryMonth) || string.IsNullOrWhiteSpace(req.ExpiryYear))
        return Results.BadRequest("invalid card");

    var bin = req.Pan[..6];
    var last4 = req.Pan[^4..];
    var network = DetectNetwork(req.Pan);
    var token = Tokenizer.Fpt(req.Pan); // format-preserving (non-reversible) token for external use
    var dekId = Guid.NewGuid().ToString("N");
    var enc = await crypto.EncryptAsync(dekId, req.Pan, $"{req.ExpiryMonth}/{req.ExpiryYear}", ct);

    await using var conn = await ds.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand(@"
        INSERT INTO cards(card_token, dek_id, pan_ct, aad, bin, last4, network, exp_m, exp_y, status, created_at)
        VALUES (@t,@d,@c,@a,@bin,@l4,@n,@m,@y,'Active', now())
        ON CONFLICT (card_token) DO NOTHING;", conn);
    cmd.Parameters.AddWithValue("t", token);
    cmd.Parameters.AddWithValue("d", dekId);
    cmd.Parameters.AddWithValue("c", enc.Ciphertext);
    cmd.Parameters.AddWithValue("a", enc.Aad);
    cmd.Parameters.AddWithValue("bin", bin);
    cmd.Parameters.AddWithValue("l4", last4);
    cmd.Parameters.AddWithValue("n", network);
    cmd.Parameters.AddWithValue("m", req.ExpiryMonth);
    cmd.Parameters.AddWithValue("y", req.ExpiryYear);
    await cmd.ExecuteNonQueryAsync(ct);

    return Results.Ok(new { card_token = token, network, last4, bin, exp_m = req.ExpiryMonth, exp_y = req.ExpiryYear, status = "Active" });
});

// Limited detokenize endpoint: NEVER returns PAN; only network token artifacts and last4 for receipts.
// For true PAN (e.g., outbound to networks), keep detokenize internal-only (mTLS + allowlist).
app.MapPost("/vault/authorize", async ([FromServices] NpgsqlDataSource ds, [FromServices] CardCrypto crypto, [FromBody] AuthRequest req, CancellationToken ct) =>
{
    await using var conn = await ds.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand(@"SELECT dek_id, pan_ct, aad, network, last4, exp_m, exp_y FROM cards WHERE card_token=@t AND status='Active'", conn);
    cmd.Parameters.AddWithValue("t", req.CardToken);
    await using var r = await cmd.ExecuteReaderAsync(ct);
    if (!await r.ReadAsync(ct)) return Results.NotFound("card not found");
    var dekId = r.GetString(0);
    var panCt = (byte[])r.GetValue(1);
    var aad = (byte[])r.GetValue(2);
    var network = r.GetString(3);
    var last4 = r.GetString(4);
    var exp_m = r.GetString(5);
    var exp_y = r.GetString(6);

    // Decrypt PAN only inside PCI zone to call the network
    var pan = await crypto.DecryptAsync(dekId, panCt, aad, ct);
    // Simulate network auth via internal network-sim (still inside PCI zone)
    using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("NETWORK_SIM") ?? "http://networksim:5601") };
    var res = await http.PostAsJsonAsync("/net/auth", new
    {
        pan, exp_m, exp_y,
        amount_minor = req.AmountMinor,
        currency = req.Currency,
        merchant_id = req.MerchantId,
        mcc = req.Mcc,
        cvv = req.Cvv // in prod, store/handle CVV transiently only (not at rest)
    }, ct);
    var payload = await res.Content.ReadAsStringAsync(ct);
    if (!res.IsSuccessStatusCode) return Results.Problem(payload, statusCode: (int)res.StatusCode);

    // Return network-agnostic auth result with reference; never PAN
    var net = JsonSerializer.Deserialize<NetAuthResponse>(payload)!;
    return Results.Ok(new
    {
        approved = net.approved,
        auth_code = net.auth_code,
        rrn = net.rrn,
        network,
        last4
    });
});

app.Run();

static string DetectNetwork(string pan)
{
    if (pan.StartsWith("4")) return "VISA";
    var i = int.Parse(pan[..2]);
    if (i is >= 51 and <= 55) return "MASTERCARD";
    return "CARD";
}

record TokenizeRequest(string Pan, string ExpiryMonth, string ExpiryYear);
record AuthRequest(string CardToken, long AmountMinor, string Currency, string MerchantId, string Mcc, string? Cvv);
record NetAuthResponse(bool approved, string auth_code, string rrn);

// ——— helpers ———
static class Luhn
{
    public static bool IsValid(string pan)
    {
        int sum = 0; bool alt = false;
        for (int i = pan.Length - 1; i >= 0; i--)
        {
            int n = pan[i] - '0'; if (n < 0 || n > 9) return false;
            if (alt) { n *= 2; if (n > 9) n -= 9; }
            sum += n; alt = !alt;
        }
        return sum % 10 == 0;
    }
}
static class Tokenizer
{
    // Simple FPT: replace PAN with same-length numeric token using Argon2 hash => deterministic digits; non-reversible
    public static string Fpt(string pan)
    {
        // WARNING: For demo only. In prod, use PCI-compliant tokenization service with vault/tables.
        var hash = Isopoh.Cryptography.Argon2.Argon2.Hash(pan + "|atlas");
        var digits = new string(hash.Where(char.IsDigit).ToArray());
        if (digits.Length < pan.Length) digits = digits.PadRight(pan.Length, '0');
        return digits[..pan.Length];
    }
}

public interface IHsmClient
{
    Task<byte[]> WrapAsync(string kekLabel, byte[] dek, CancellationToken ct);
    Task<byte[]> UnwrapAsync(string kekLabel, byte[] wrapped, CancellationToken ct);
    Task<byte[]> GetKekAsync(string label, CancellationToken ct);
}
public sealed class MockHsm : IHsmClient
{
    public Task<byte[]> WrapAsync(string kekLabel, byte[] dek, CancellationToken ct) => Task.FromResult(SimpleXor(dek, Kek(kekLabel)));
    public Task<byte[]> UnwrapAsync(string kekLabel, byte[] wrapped, CancellationToken ct) => Task.FromResult(SimpleXor(wrapped, Kek(kekLabel)));
    public Task<byte[]> GetKekAsync(string label, CancellationToken ct) => Task.FromResult(Kek(label));
    static byte[] Kek(string s) => SHA256.HashData(Encoding.UTF8.GetBytes($"KEK::{s}::atlas"));
    static byte[] SimpleXor(byte[] a, byte[] b) { var r = new byte[a.Length]; for (int i = 0; i < a.Length; i++) r[i] = (byte)(a[i] ^ b[i % b.Length]); return r; }
}
public sealed class CardCrypto
{
    private readonly IHsmClient _hsm;
    public CardCrypto(IHsmClient hsm) => _hsm = hsm;

    public async Task<(byte[] Ciphertext, byte[] Aad)> EncryptAsync(string dekId, string pan, string aad, CancellationToken ct)
    {
        // DEK per-card, wrapped by KEK from HSM
        using var rng = RandomNumberGenerator.Create();
        var dek = new byte[32]; rng.GetBytes(dek);
        var kekLabel = Environment.GetEnvironmentVariable("KEK_LABEL") ?? "cards-kek-1";
        var wrapped = await _hsm.WrapAsync(kekLabel, dek, ct);

        // store wrapped dek alongside ciphertext (AAD = expiry)
        using var aes = new AesGcm(dek);
        var nonce = new byte[12]; rng.GetBytes(nonce);
        var pt = Encoding.UTF8.GetBytes(pan);
        var ctbuf = new byte[pt.Length];
        var tag = new byte[16];
        var aadBytes = Encoding.UTF8.GetBytes(aad);
        aes.Encrypt(nonce, pt, ctbuf, tag, aadBytes);
        var blob = new byte[1 + 12 + ctbuf.Length + 16 + wrapped.Length];
        blob[0] = 1; // version
        Buffer.BlockCopy(nonce, 0, blob, 1, 12);
        Buffer.BlockCopy(ctbuf, 0, blob, 13, ctbuf.Length);
        Buffer.BlockCopy(tag, 0, blob, 13 + ctbuf.Length, 16);
        Buffer.BlockCopy(wrapped, 0, blob, 29 + ctbuf.Length, wrapped.Length);
        return (blob, aadBytes);
    }

    public async Task<string> DecryptAsync(string dekId, byte[] blob, byte[] aad, CancellationToken ct)
    {
        int version = blob[0];
        var nonce = blob.AsSpan(1, 12).ToArray();
        int ctLen = blob.Length - 29 - 32; // naive: assuming wrapped ~32 bytes via MockHsm.XOR
        var ctbuf = blob.AsSpan(13, ctLen).ToArray();
        var tag = blob.AsSpan(13 + ctLen, 16).ToArray();
        var wrapped = blob.AsSpan(29 + ctLen).ToArray();

        var kekLabel = Environment.GetEnvironmentVariable("KEK_LABEL") ?? "cards-kek-1";
        var dek = await _hsm.UnwrapAsync(kekLabel, wrapped, ct);

        using var aes = new AesGcm(dek);
        var pt = new byte[ctbuf.Length];
        aes.Decrypt(nonce, ctbuf, tag, pt, aad);
        return Encoding.UTF8.GetString(pt);
    }
}