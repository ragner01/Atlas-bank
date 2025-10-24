using Microsoft.EntityFrameworkCore;

namespace Atlas.Loans.Domain;

public sealed class LoansDbContext : DbContext
{
    public LoansDbContext(DbContextOptions<LoansDbContext> o) : base(o) { }
    public DbSet<LoanProduct> Products => Set<LoanProduct>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Repayment> Repayments => Set<Repayment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LoanProduct>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TenantId); });
        b.Entity<Loan>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.CustomerId, x.Status }); });
        b.Entity<Installment>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.LoanId, x.Sequence }).IsUnique(); });
        b.Entity<Repayment>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.LoanId); });
    }
}
