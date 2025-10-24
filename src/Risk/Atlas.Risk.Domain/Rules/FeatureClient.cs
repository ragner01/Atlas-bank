namespace Atlas.Risk.Domain.Rules;

public interface IFeatureClient
{
    Task<long> GetVelocityAsync(string tenant, string subject, int seconds, string currency, CancellationToken ct);
}

public sealed class HttpFeatureClient : IFeatureClient
{
    private readonly HttpClient _http;
    public HttpFeatureClient(HttpClient http) => _http = http;

    public async Task<long> GetVelocityAsync(string tenant, string subject, int seconds, string currency, CancellationToken ct)
    {
        var uri = $"/features/velocity?tenant={Uri.EscapeDataString(tenant)}&subject={Uri.EscapeDataString(subject)}&seconds={seconds}&currency={Uri.EscapeDataString(currency)}";
        using var res = await _http.GetAsync(uri, ct);
        res.EnsureSuccessStatusCode();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("totalMinor").GetInt64();
    }
}
