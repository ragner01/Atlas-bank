using Atlas.Loans.App;
using Atlas.Loans.Domain;
using FluentAssertions;
using Xunit;

public class ScheduleTests
{
    [Fact]
    public void Generates_Correct_Number_Of_Installments()
    {
        var product = new LoanProduct { Name = "Test", AnnualRate = 0.24m, TermMonths = 12, Scale = 2, Currency="NGN" };
        var loan = new Loan { PrincipalMinor = 1_200_00, Currency="NGN", Scale=2, StartDate=DateTimeOffset.Parse("2025-01-01") };
        var svc = new ScheduleService();

        var schedule = svc.Generate(product, loan).ToList();
        schedule.Count.Should().Be(12);
        schedule.First().Sequence.Should().Be(1);
        schedule.Last().Sequence.Should().Be(12);
    }

    [Fact]
    public void Generates_Equal_Installments_For_Zero_Rate()
    {
        var product = new LoanProduct { Name = "Zero Rate", AnnualRate = 0m, TermMonths = 6, Scale = 2, Currency="NGN" };
        var loan = new Loan { PrincipalMinor = 600_00, Currency="NGN", Scale=2, StartDate=DateTimeOffset.Parse("2025-01-01") };
        var svc = new ScheduleService();

        var schedule = svc.Generate(product, loan).ToList();
        schedule.Count.Should().Be(6);
        schedule.All(i => i.InterestMinor == 0).Should().BeTrue();
        schedule.All(i => i.PrincipalMinor == 100_00).Should().BeTrue(); // 600/6 = 100
    }

    [Fact]
    public void Repayment_Allocator_Applies_Payments_Correctly()
    {
        var product = new LoanProduct { Name = "Test", AnnualRate = 0.12m, TermMonths = 3, Scale = 2, Currency="NGN" };
        var loan = new Loan { PrincipalMinor = 300_00, Currency="NGN", Scale=2, StartDate=DateTimeOffset.Parse("2025-01-01") };
        var svc = new ScheduleService();
        var allocator = new RepaymentAllocator();

        var schedule = svc.Generate(product, loan).ToList();
        var repayment = new Repayment { LoanId = loan.Id, AmountMinor = 200_00, Currency = "NGN" };

        allocator.Apply(loan, schedule, repayment);

        schedule[0].PaidMinor.Should().BeGreaterThan(0);
        loan.TotalPaidMinor.Should().Be(200_00);
    }

    [Fact]
    public void Delinquency_Engine_Updates_Status_Correctly()
    {
        var loan = new Loan { Status = LoanStatus.Active };
        var pastDueInstallment = new Installment 
        { 
            DueDate = DateTimeOffset.UtcNow.AddDays(-10), 
            PaidMinor = 0, 
            PrincipalMinor = 50_00,
            InterestMinor = 50_00
        };
        var currentInstallment = new Installment 
        { 
            DueDate = DateTimeOffset.UtcNow.AddDays(5), 
            PaidMinor = 0, 
            PrincipalMinor = 50_00,
            InterestMinor = 50_00
        };
        var schedule = new List<Installment> { pastDueInstallment, currentInstallment };
        var engine = new DelinquencyEngine();

        engine.UpdateDelinquency(loan, schedule, graceDays: 5);

        loan.Status.Should().Be(LoanStatus.Delinquent);
    }
}
