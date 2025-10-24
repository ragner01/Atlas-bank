using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

var b = WebApplication.CreateBuilder(args);
var app = b.Build();

app.MapGet("/health", () => Results.Ok(new { status="ok" }));

// GET /fees/quote?merchantId=m-123&minor=150000&currency=NGN&network=VISA&mcc=5999
app.MapGet("/fees/quote", (
  string merchantId, long minor, string currency, string network, string mcc) =>
{
    // Simple MDR rules; override via env if needed
    var mdrBp = decimal.Parse(Environment.GetEnvironmentVariable("FEES_MDR_BP") ?? "150");      // 1.50%
    var schemeBp = decimal.Parse(Environment.GetEnvironmentVariable("FEES_SCHEME_BP") ?? "20"); // 0.20%
    var fixedMinor = long.Parse(Environment.GetEnvironmentVariable("FEES_FIXED_MINOR") ?? "100"); // 1.00

    decimal amount = minor / 100m;
    long mdrMinor = (long)Math.Round(amount * (mdrBp/10000m) * 100m, MidpointRounding.AwayFromZero);
    long schemeMinor = (long)Math.Round(amount * (schemeBp/10000m) * 100m, MidpointRounding.AwayFromZero);
    long total = mdrMinor + schemeMinor + fixedMinor;

    return Results.Ok(new {
        merchantId, currency, network, mcc,
        mdrMinor, schemeMinor, fixedMinor, totalMinor = total,
        effectiveBp = (int)Math.Round(((total - fixedMinor)/amount) * 10000m) // basis points
    });
});

app.Run();
