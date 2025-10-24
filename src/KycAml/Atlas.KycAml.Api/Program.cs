using Atlas.KycAml.Domain;
using Microsoft.EntityFrameworkCore;

var b = WebApplication.CreateBuilder(args);
b.Services.AddDbContext<CasesDbContext>(o => o.UseNpgsql(b.Configuration.GetConnectionString("Cases")!));
var app = b.Build();

app.MapGet("/health", () => Results.Ok());
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());
app.MapGet("/aml/cases", async (CasesDbContext db, string? status, string? tenant) =>
{
    var q = db.Cases.AsQueryable();
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
    if (!string.IsNullOrWhiteSpace(tenant)) q = q.Where(c => c.TenantId == tenant);
    return Results.Ok(await q.OrderByDescending(c => c.OpenedAt).Take(200).ToListAsync());
});

app.MapPost("/aml/cases", async (CasesDbContext db, AmlCase input) =>
{
    db.Cases.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/aml/cases/{input.Id}", input);
});

app.MapPatch("/aml/cases/{id:guid}", async (Guid id, CasesDbContext db, CaseStatus status, string? owner) =>
{
    var c = await db.Cases.FindAsync(id);
    if (c is null) return Results.NotFound();
    c.Status = status;
    if (!string.IsNullOrWhiteSpace(owner)) c.Owner = owner;
    if (status == CaseStatus.Closed) c.ClosedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(c);
});

app.Run();
