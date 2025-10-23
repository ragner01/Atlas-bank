using Atlas.Common;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Ledger.Domain;

public sealed class LedgerDbContext : DbContext
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<JournalRow> Journals => Set<JournalRow>();
    
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AccountRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.LedgerCents).IsRequired();
            e.HasIndex(x => x.TenantId);
        });
        
        b.Entity<JournalRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Narrative).HasMaxLength(256);
            e.Property(x => x.BookingDate).IsRequired();
        });
    }
}

public sealed class AccountRow 
{ 
    public string Id { get; set; } = default!; 
    public string TenantId { get; set; } = default!; 
    public string Currency { get; set; } = "NGN"; 
    public long LedgerCents { get; set; } 
}

public sealed class JournalRow 
{ 
    public Guid Id { get; set; } = Guid.NewGuid(); 
    public string TenantId { get; set; } = default!; 
    public DateTimeOffset BookingDate { get; set; } = DateTimeOffset.UtcNow; 
    public string Narrative { get; set; } = string.Empty; 
    public string DebitsJson { get; set; } = string.Empty; 
    public string CreditsJson { get; set; } = string.Empty; 
}
