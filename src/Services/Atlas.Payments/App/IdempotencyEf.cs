using Microsoft.EntityFrameworkCore;

namespace Atlas.Payments.App;

public sealed class PaymentsDbContext : DbContext
{ 
    public DbSet<IdemRow> Idempotency => Set<IdemRow>(); 
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> o) : base(o) { } 
    protected override void OnModelCreating(ModelBuilder b){ b.Entity<IdemRow>().HasKey(x=>x.Key);} 
}
public sealed class IdemRow { public required string Key { get; set; } public DateTimeOffset SeenAt { get; set; } = DateTimeOffset.UtcNow; }

public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly PaymentsDbContext _db;
    public EfIdempotencyStore(PaymentsDbContext db) => _db = db;
    public async Task<bool> SeenAsync(string key, CancellationToken ct) => await _db.Idempotency.FindAsync([key], ct) is not null;
    public async Task MarkAsync(string key, CancellationToken ct) { _db.Idempotency.Add(new IdemRow { Key = key }); await _db.SaveChangesAsync(ct); }
}
