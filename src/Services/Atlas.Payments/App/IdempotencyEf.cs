using Microsoft.EntityFrameworkCore;

namespace Atlas.Payments.App;

public sealed class PaymentsDbContext : DbContext
{ 
    public DbSet<IdemRow> Idempotency => Set<IdemRow>(); 
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> o) : base(o) { } 
    protected override void OnModelCreating(ModelBuilder b)
    { 
        b.Entity<IdemRow>().HasKey(x => x.Key);
        b.Entity<IdemRow>().Property(x => x.Key).HasMaxLength(255);
        b.Entity<IdemRow>().HasIndex(x => x.SeenAt);
    } 
}

public sealed class IdemRow 
{ 
    public required string Key { get; set; } 
    public DateTimeOffset SeenAt { get; set; } = DateTimeOffset.UtcNow; 
}

public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly PaymentsDbContext _db;
    public EfIdempotencyStore(PaymentsDbContext db) => _db = db;
    
    public async Task<bool> SeenAsync(string key, CancellationToken ct) 
        => await _db.Idempotency.FindAsync([key], ct) is not null;
    
    public async Task MarkAsync(string key, CancellationToken ct) 
    { 
        _db.Idempotency.Add(new IdemRow { Key = key }); 
        await _db.SaveChangesAsync(ct); 
    }

    /// <summary>
    /// Atomically check if key exists and mark it if it doesn't.
    /// Returns true if the key was already processed, false if it's new and now marked.
    /// </summary>
    public async Task<bool> CheckAndMarkAsync(string key, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Check if key already exists
            var existing = await _db.Idempotency.FindAsync([key], ct);
            if (existing is not null)
            {
                await transaction.CommitAsync(ct);
                return true; // Already processed
            }

            // Mark as processed
            _db.Idempotency.Add(new IdemRow { Key = key });
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return false; // New key, now marked
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
