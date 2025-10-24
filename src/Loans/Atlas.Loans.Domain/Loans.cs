namespace Atlas.Loans.Domain;

public enum LoanStatus { Draft, Active, Delinquent, Closed, WrittenOff }

public sealed class LoanProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "tnt_demo";
    public string Name { get; set; } = string.Empty;
    public decimal AnnualRate { get; set; } // e.g., 0.24m = 24% APR
    public int TermMonths { get; set; }     // e.g., 12
    public int Scale { get; set; } = 2;     // currency scale
    public string Currency { get; set; } = "NGN";
    public bool EqualInstallments { get; set; } = true; // annuity
}

public sealed class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "tnt_demo";
    public Guid ProductId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public long PrincipalMinor { get; set; }
    public string Currency { get; set; } = "NGN";
    public int Scale { get; set; } = 2;
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow.Date;
    public LoanStatus Status { get; set; } = LoanStatus.Draft;

    public long TotalPaidMinor { get; set; } = 0;
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class Installment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanId { get; set; }
    public int Sequence { get; set; } // 1..N
    public DateTimeOffset DueDate { get; set; }
    public long PrincipalMinor { get; set; }
    public long InterestMinor { get; set; }
    public long TotalMinor => PrincipalMinor + InterestMinor;
    public long PaidMinor { get; set; }
    public bool IsPaid => PaidMinor >= TotalMinor;
    public DateTimeOffset? PaidAt { get; set; }
}

public sealed class Repayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LoanId { get; set; }
    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Narration { get; set; } = "Repayment";
}
