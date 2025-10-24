using Atlas.Loans.Domain;

namespace Atlas.Loans.App;

public sealed class ScheduleService
{
    /// <summary>Generate equal-installment amortization schedule (annuity, monthly compounding).</summary>
    public IEnumerable<Installment> Generate(LoanProduct product, Loan loan)
    {
        var P = loan.PrincipalMinor / (decimal)Math.Pow(10, product.Scale);
        var r = product.AnnualRate / 12m;
        var n = product.TermMonths;
        var factor = (r == 0) ? (P / n) : (P * (r * Pow(1 + r, n)) / (Pow(1 + r, n) - 1));
        var monthlyPayment = RoundMinor(factor, product.Scale);

        var balanceMinor = loan.PrincipalMinor;
        for (int i = 1; i <= n; i++)
        {
            var interestMinor = RoundMinor((balanceMinor / (decimal)Math.Pow(10, product.Scale)) * r, product.Scale);
            var principalMinor = Math.Clamp(monthlyPayment - interestMinor, 0, balanceMinor);
            if (i == n) principalMinor = balanceMinor; // final adjustment
            var due = loan.StartDate.AddMonths(i);
            yield return new Installment
            {
                LoanId = loan.Id,
                Sequence = i,
                DueDate = due,
                PrincipalMinor = principalMinor,
                InterestMinor = interestMinor
            };
            balanceMinor -= principalMinor;
        }
    }

    static long RoundMinor(decimal amount, int scale) => (long)Math.Round(amount * (decimal)Math.Pow(10, scale), MidpointRounding.AwayFromZero);
    static decimal Pow(decimal a, int n) => (decimal)Math.Pow((double)a, n);
}

public sealed class RepaymentAllocator
{
    /// <summary>Allocates a repayment across due installments: first interest, then principal (classic order).</summary>
    public void Apply(Loan loan, List<Installment> schedule, Repayment repayment)
    {
        var remaining = repayment.AmountMinor;
        foreach (var inst in schedule.OrderBy(i => i.Sequence))
        {
            if (inst.IsPaid) continue;

            var dueInterest = Math.Max(inst.InterestMinor - Math.Min(inst.PaidMinor, inst.InterestMinor), 0);
            var payInterest = Math.Min(remaining, dueInterest);
            inst.PaidMinor += payInterest;
            remaining -= payInterest;

            var paidInterest = Math.Min(inst.PaidMinor, inst.InterestMinor);
            var duePrincipal = inst.TotalMinor - paidInterest - Math.Min(inst.PaidMinor - paidInterest, inst.PrincipalMinor);
            var payPrincipal = Math.Min(remaining, duePrincipal);
            inst.PaidMinor += payPrincipal;
            remaining -= payPrincipal;

            if (inst.IsPaid) inst.PaidAt = DateTimeOffset.UtcNow;
            if (remaining <= 0) break;
        }

        loan.TotalPaidMinor += (repayment.AmountMinor - remaining);
        if (schedule.All(i => i.IsPaid))
        {
            loan.Status = LoanStatus.Closed;
            loan.ClosedAt = DateTimeOffset.UtcNow;
        }
    }
}

public sealed class DelinquencyEngine
{
    public void UpdateDelinquency(Loan loan, IEnumerable<Installment> schedule, int graceDays = 5)
    {
        var anyPastDue = schedule.Any(i => !i.IsPaid && i.DueDate.AddDays(graceDays) < DateTimeOffset.UtcNow);
        if (loan.Status == LoanStatus.Active && anyPastDue) loan.Status = LoanStatus.Delinquent;
        if (loan.Status == LoanStatus.Delinquent && !anyPastDue) loan.Status = LoanStatus.Active;
    }
}
