using Atlas.Common;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Ledger.Domain;

public sealed class LedgerDbContext : DbContext
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<JournalRow> Journals => Set<JournalRow>();
    public DbSet<PostingRow> Postings => Set<PostingRow>();
    
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AccountRow>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("account_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.LedgerCents).IsRequired().HasColumnName("ledger_minor");
            e.HasIndex(x => x.TenantId);
        });
        
        b.Entity<JournalRow>(e =>
        {
            e.ToTable("journal_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("entry_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Narrative).HasMaxLength(256);
            e.Property(x => x.BookingDate).IsRequired().HasColumnName("booking_date");
        });
        
        b.Entity<PostingRow>(e =>
        {
            e.ToTable("postings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("posting_id");
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.AmountMinor).HasColumnName("amount_minor");
            e.Property(x => x.Side).HasMaxLength(1);
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
        });
    }
}

public sealed class AccountRow 
{ 
    public string Id { get; set; } = default!; 
    public string TenantId { get; set; } = default!; 
    public string Currency { get; set; } = default!; // Remove hardcoded default
    public long LedgerCents { get; set; } 
}

public sealed class JournalRow 
{ 
    public Guid Id { get; set; } = Guid.NewGuid(); 
    public string TenantId { get; set; } = default!; 
    public DateTimeOffset BookingDate { get; set; } = DateTimeOffset.UtcNow; 
    public string Narrative { get; set; } = string.Empty; 
}

public sealed class PostingRow 
{ 
    public Guid Id { get; set; } = Guid.NewGuid(); 
    public Guid EntryId { get; set; } 
    public string AccountId { get; set; } = default!; 
    public long AmountMinor { get; set; } 
    public string Side { get; set; } = default!; // 'D' for Debit, 'C' for Credit
    public string TenantId { get; set; } = default!; 
}
