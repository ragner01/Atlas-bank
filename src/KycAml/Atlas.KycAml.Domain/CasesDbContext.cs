using Microsoft.EntityFrameworkCore;

namespace Atlas.KycAml.Domain;

public sealed class CasesDbContext : DbContext
{
    public DbSet<AmlCase> Cases => Set<AmlCase>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public CasesDbContext(DbContextOptions<CasesDbContext> o) : base(o) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AmlCase>(e => {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.TenantId).HasMaxLength(50).IsRequired();
            e.Property(x => x.CustomerId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Owner).HasMaxLength(100);
        });
        b.Entity<InboxMessage>(e => {
            e.HasKey(x => new { x.Consumer, x.MessageId });
            e.Property(x => x.Consumer).HasMaxLength(100).IsRequired();
            e.Property(x => x.MessageId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ProcessedAt).IsRequired();
        });
    }
}

public sealed class InboxMessage
{
    public required string Consumer { get; set; }
    public required string MessageId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
