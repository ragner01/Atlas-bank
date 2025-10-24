using Atlas.Loans.Domain;
using Atlas.Loans.App;
using Microsoft.EntityFrameworkCore;

var b = WebApplication.CreateBuilder(args);
b.Services.AddDbContext<LoansDbContext>(o => o.UseNpgsql(b.Configuration.GetConnectionString("Loans")));
b.Services.AddSingleton<ScheduleService>();
b.Services.AddSingleton<RepaymentAllocator>();
b.Services.AddSingleton<DelinquencyEngine>();

var app = b.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoansDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok());
    app.MapMethods("/health", new[] { "HEAD" }, () => Results.Ok());

app.MapPost("/loans/products", async (LoansDbContext db, LoanProduct input) =>
{
    db.Products.Add(input);
    await db.SaveChangesAsync();
    return Results.Created($"/loans/products/{input.Id}", input);
});

app.MapGet("/loans/products", async (LoansDbContext db) => Results.Ok(await db.Products.OrderBy(p => p.Name).ToListAsync()));

app.MapPost("/loans", async (LoansDbContext db, ScheduleService svc, Guid productId, string customerId, long principalMinor, DateTimeOffset? startDate) =>
{
    var product = await db.Products.FindAsync(productId);
    if (product is null) return Results.NotFound("Product not found");

    var loan = new Loan
    {
        ProductId = product.Id,
        CustomerId = customerId,
        PrincipalMinor = principalMinor,
        Currency = product.Currency,
        Scale = product.Scale,
        StartDate = (startDate ?? DateTimeOffset.UtcNow.Date)
    };
    db.Loans.Add(loan);
    await db.SaveChangesAsync();

    var schedule = svc.Generate(product, loan).ToList();
    db.Installments.AddRange(schedule);
    loan.Status = LoanStatus.Active;
    await db.SaveChangesAsync();

    return Results.Created($"/loans/{loan.Id}", new { loan, schedule });
});

app.MapGet("/loans/{id:guid}/schedule", async (LoansDbContext db, Guid id) =>
{
    var loan = await db.Loans.FindAsync(id);
    if (loan is null) return Results.NotFound();
    var sched = await db.Installments.Where(i => i.LoanId == id).OrderBy(i => i.Sequence).ToListAsync();
    return Results.Ok(new { loan, schedule = sched });
});

app.MapPost("/loans/{id:guid}/repayments", async (LoansDbContext db, RepaymentAllocator alloc, DelinquencyEngine delinq, Guid id, long amountMinor, string narration) =>
{
    var loan = await db.Loans.FindAsync(id);
    if (loan is null) return Results.NotFound();
    if (loan.Status is LoanStatus.Closed or LoanStatus.WrittenOff) return Results.Problem("Loan is closed", statusCode: 409);

    var repayment = new Repayment { LoanId = id, AmountMinor = amountMinor, Currency = loan.Currency, Narration = narration };
    db.Repayments.Add(repayment);

    var schedule = await db.Installments.Where(i => i.LoanId == id).OrderBy(i => i.Sequence).ToListAsync();
    alloc.Apply(loan, schedule, repayment);
    delinq.UpdateDelinquency(loan, schedule);

    await db.SaveChangesAsync();
    return Results.Accepted($"/loans/{id}/schedule");
});

app.MapPost("/loans/{id:guid}/writeoff", async (LoansDbContext db, Guid id, string reason) =>
{
    var loan = await db.Loans.FindAsync(id);
    if (loan is null) return Results.NotFound();
    loan.Status = LoanStatus.WrittenOff;
    await db.SaveChangesAsync();
    return Results.Ok(new { id, status = loan.Status, reason });
});

app.Run();
