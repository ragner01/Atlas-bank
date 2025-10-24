using Microsoft.AspNetCore.Mvc;

var b = WebApplication.CreateBuilder(args);
var app = b.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

// Simulate network auth with simple rules: decline high amounts or expired
app.MapPost("/net/auth", ([FromBody] NetAuth req) =>
{
    if (string.IsNullOrWhiteSpace(req.pan) || req.exp_m is null || req.exp_y is null) 
        return Results.BadRequest("bad request");

    // Check if card is expired
    bool expired = DateTime.UtcNow > new DateTime(int.Parse(req.exp_y), int.Parse(req.exp_m), 1).AddMonths(1).AddDays(-1);
    if (expired) 
        return Results.Ok(new { approved = false, auth_code = "", rrn = "", reason = "expired" });

    // Decline high amounts (over 2M in minor units = 20,000.00)
    if (req.amount_minor > 2_000_000_00) 
        return Results.Ok(new { approved = false, auth_code = "", rrn = "", reason = "amount_exceeded" });

    // Decline certain MCCs (gambling, etc.)
    if (req.mcc == "7995") // Gambling
        return Results.Ok(new { approved = false, auth_code = "", rrn = "", reason = "mcc_restricted" });

    // Random auth code & RRN
    var rrn = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    var ac = "A" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
    
    return Results.Ok(new { 
        approved = true, 
        auth_code = ac, 
        rrn = rrn,
        reason = "approved"
    });
});

app.Run();

record NetAuth(string pan, string exp_m, string exp_y, long amount_minor, string currency, string merchant_id, string mcc, string? cvv);
