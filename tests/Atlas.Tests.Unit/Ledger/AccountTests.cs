using Atlas.Common.ValueObjects;
using Atlas.Common.Results;
using Atlas.Ledger.Domain;
using FluentAssertions;
using Xunit;

namespace Atlas.Tests.Unit.Ledger;

public class AccountTests
{
    [Fact]
    public void CreateAccount_WithValidData_ShouldSucceed()
    {
        // Arrange
        var id = EntityId.NewId();
        var tenantId = new TenantId("tenant-1");
        var accountNumber = "1234567890";
        var name = "Test Account";
        var type = AccountType.Asset;
        var currency = Currency.NGN;

        // Act
        var account = new Account(id, tenantId, accountNumber, name, type, currency);

        // Assert
        account.Id.Should().Be(id);
        account.TenantId.Should().Be(tenantId);
        account.AccountNumber.Should().Be(accountNumber);
        account.Name.Should().Be(name);
        account.Type.Should().Be(type);
        account.Currency.Should().Be(currency);
        account.Balance.Should().Be(Money.Zero(currency));
        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Debit_WithValidAmount_ShouldUpdateBalance()
    {
        // Arrange
        var account = CreateTestAccount();
        var debitAmount = new Money(1000, Currency.NGN);

        // Act
        var result = account.Debit(debitAmount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(debitAmount);
        account.Balance.Should().Be(new Money(-1000, Currency.NGN));
    }

    [Fact]
    public void Credit_WithValidAmount_ShouldUpdateBalance()
    {
        // Arrange
        var account = CreateTestAccount();
        var creditAmount = new Money(1000, Currency.NGN);

        // Act
        var result = account.Credit(creditAmount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(creditAmount);
        account.Balance.Should().Be(new Money(1000, Currency.NGN));
    }

    [Fact]
    public void Debit_WithInactiveAccount_ShouldFail()
    {
        // Arrange
        var account = CreateTestAccount();
        account.Deactivate();
        var debitAmount = new Money(1000, Currency.NGN);

        // Act
        var result = account.Debit(debitAmount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Account is not active");
    }

    [Fact]
    public void Debit_WithWrongCurrency_ShouldFail()
    {
        // Arrange
        var account = CreateTestAccount();
        var debitAmount = new Money(1000, Currency.USD);

        // Act
        var result = account.Debit(debitAmount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Currency mismatch");
    }

    [Fact]
    public void Debit_WithNegativeAmount_ShouldFail()
    {
        // Arrange
        var account = CreateTestAccount();
        var debitAmount = new Money(-1000, Currency.NGN);

        // Act
        var result = account.Debit(debitAmount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Debit amount must be positive");
    }

    [Fact]
    public void Debit_WithZeroAmount_ShouldFail()
    {
        // Arrange
        var account = CreateTestAccount();
        var debitAmount = Money.Zero(Currency.NGN);

        // Act
        var result = account.Debit(debitAmount);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Debit amount must be positive");
    }

    private static Account CreateTestAccount()
    {
        return new Account(
            EntityId.NewId(),
            new TenantId("tenant-1"),
            "1234567890",
            "Test Account",
            AccountType.Asset,
            Currency.NGN
        );
    }
}

public class JournalEntryTests
{
    [Fact]
    public void CreateJournalEntry_WithValidData_ShouldSucceed()
    {
        // Arrange
        var id = EntityId.NewId();
        var tenantId = new TenantId("tenant-1");
        var reference = "REF-001";
        var description = "Test journal entry";
        var entryDate = Timestamp.Now;

        // Act
        var journalEntry = new JournalEntry(id, tenantId, reference, description, entryDate);

        // Assert
        journalEntry.Id.Should().Be(id);
        journalEntry.TenantId.Should().Be(tenantId);
        journalEntry.Reference.Should().Be(reference);
        journalEntry.Description.Should().Be(description);
        journalEntry.EntryDate.Should().Be(entryDate);
        journalEntry.Status.Should().Be(JournalEntryStatus.Draft);
        journalEntry.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AddLine_WithValidData_ShouldSucceed()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var accountId = EntityId.NewId();
        var amount = new Money(1000, Currency.NGN);
        var type = JournalEntryLineType.Debit;

        // Act
        var result = journalEntry.AddLine(accountId, amount, type);

        // Assert
        result.IsSuccess.Should().BeTrue();
        journalEntry.Lines.Should().HaveCount(1);
        journalEntry.Lines.First().AccountId.Should().Be(accountId);
        journalEntry.Lines.First().Amount.Should().Be(amount);
        journalEntry.Lines.First().Type.Should().Be(type);
    }

    [Fact]
    public void AddLine_WithZeroAmount_ShouldFail()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var accountId = EntityId.NewId();
        var amount = Money.Zero(Currency.NGN);
        var type = JournalEntryLineType.Debit;

        // Act
        var result = journalEntry.AddLine(accountId, amount, type);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Line amount cannot be zero");
    }

    [Fact]
    public void Post_WithBalancedEntry_ShouldSucceed()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var account1 = EntityId.NewId();
        var account2 = EntityId.NewId();
        var amount = new Money(1000, Currency.NGN);

        journalEntry.AddLine(account1, amount, JournalEntryLineType.Debit);
        journalEntry.AddLine(account2, amount, JournalEntryLineType.Credit);

        // Act
        var result = journalEntry.Post();

        // Assert
        result.IsSuccess.Should().BeTrue();
        journalEntry.Status.Should().Be(JournalEntryStatus.Posted);
    }

    [Fact]
    public void Post_WithUnbalancedEntry_ShouldFail()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var account1 = EntityId.NewId();
        var account2 = EntityId.NewId();
        var amount1 = new Money(1000, Currency.NGN);
        var amount2 = new Money(2000, Currency.NGN);

        journalEntry.AddLine(account1, amount1, JournalEntryLineType.Debit);
        journalEntry.AddLine(account2, amount2, JournalEntryLineType.Credit);

        // Act
        var result = journalEntry.Post();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Journal entry is not balanced");
    }

    [Fact]
    public void Post_WithLessThanTwoLines_ShouldFail()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var account = EntityId.NewId();
        var amount = new Money(1000, Currency.NGN);

        journalEntry.AddLine(account, amount, JournalEntryLineType.Debit);

        // Act
        var result = journalEntry.Post();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Journal entry must have at least two lines");
    }

    [Fact]
    public void Post_WithPostedEntry_ShouldFail()
    {
        // Arrange
        var journalEntry = CreateTestJournalEntry();
        var account1 = EntityId.NewId();
        var account2 = EntityId.NewId();
        var amount = new Money(1000, Currency.NGN);

        journalEntry.AddLine(account1, amount, JournalEntryLineType.Debit);
        journalEntry.AddLine(account2, amount, JournalEntryLineType.Credit);
        journalEntry.Post();

        // Act
        var result = journalEntry.Post();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Journal entry is not in draft status");
    }

    private static JournalEntry CreateTestJournalEntry()
    {
        return new JournalEntry(
            EntityId.NewId(),
            new TenantId("tenant-1"),
            "REF-001",
            "Test journal entry",
            Timestamp.Now
        );
    }
}

public class MoneyTests
{
    [Fact]
    public void CreateMoney_WithValidData_ShouldSucceed()
    {
        // Arrange & Act
        var money = new Money(1000.50m, Currency.NGN, 2);

        // Assert
        money.Value.Should().Be(1000.50m);
        money.Currency.Should().Be(Currency.NGN);
        money.Scale.Should().Be(2);
    }

    [Fact]
    public void AddMoney_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var money1 = new Money(1000, Currency.NGN);
        var money2 = new Money(500, Currency.NGN);

        // Act
        var result = money1 + money2;

        // Assert
        result.Value.Should().Be(1500);
        result.Currency.Should().Be(Currency.NGN);
    }

    [Fact]
    public void AddMoney_WithDifferentCurrency_ShouldFail()
    {
        // Arrange
        var money1 = new Money(1000, Currency.NGN);
        var money2 = new Money(500, Currency.USD);

        // Act & Assert
        var action = () => money1 + money2;
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add money with different currencies");
    }

    [Fact]
    public void SubtractMoney_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var money1 = new Money(1000, Currency.NGN);
        var money2 = new Money(300, Currency.NGN);

        // Act
        var result = money1 - money2;

        // Assert
        result.Value.Should().Be(700);
        result.Currency.Should().Be(Currency.NGN);
    }

    [Fact]
    public void MultiplyMoney_ShouldSucceed()
    {
        // Arrange
        var money = new Money(1000, Currency.NGN);

        // Act
        var result = money * 1.5m;

        // Assert
        result.Value.Should().Be(1500);
        result.Currency.Should().Be(Currency.NGN);
    }

    [Fact]
    public void DivideMoney_ShouldSucceed()
    {
        // Arrange
        var money = new Money(1000, Currency.NGN);

        // Act
        var result = money / 2m;

        // Assert
        result.Value.Should().Be(500);
        result.Currency.Should().Be(Currency.NGN);
    }

    [Fact]
    public void CompareMoney_WithSameCurrency_ShouldSucceed()
    {
        // Arrange
        var money1 = new Money(1000, Currency.NGN);
        var money2 = new Money(500, Currency.NGN);

        // Act & Assert
        (money1 > money2).Should().BeTrue();
        (money1 < money2).Should().BeFalse();
        (money1 >= money2).Should().BeTrue();
        (money1 <= money2).Should().BeFalse();
    }

    [Fact]
    public void CompareMoney_WithDifferentCurrency_ShouldFail()
    {
        // Arrange
        var money1 = new Money(1000, Currency.NGN);
        var money2 = new Money(500, Currency.USD);

        // Act & Assert
        var action = () => money1 > money2;
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot compare money with different currencies");
    }

    [Fact]
    public void IsZero_WithZeroValue_ShouldReturnTrue()
    {
        // Arrange
        var money = Money.Zero(Currency.NGN);

        // Act & Assert
        money.IsZero.Should().BeTrue();
        money.IsPositive.Should().BeFalse();
        money.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void IsPositive_WithPositiveValue_ShouldReturnTrue()
    {
        // Arrange
        var money = new Money(1000, Currency.NGN);

        // Act & Assert
        money.IsPositive.Should().BeTrue();
        money.IsZero.Should().BeFalse();
        money.IsNegative.Should().BeFalse();
    }

    [Fact]
    public void IsNegative_WithNegativeValue_ShouldReturnTrue()
    {
        // Arrange
        var money = new Money(-1000, Currency.NGN);

        // Act & Assert
        money.IsNegative.Should().BeTrue();
        money.IsZero.Should().BeFalse();
        money.IsPositive.Should().BeFalse();
    }

    [Fact]
    public void Abs_WithNegativeValue_ShouldReturnPositive()
    {
        // Arrange
        var money = new Money(-1000, Currency.NGN);

        // Act
        var result = money.Abs();

        // Assert
        result.Value.Should().Be(1000);
        result.Currency.Should().Be(Currency.NGN);
    }

    [Fact]
    public void Negate_WithPositiveValue_ShouldReturnNegative()
    {
        // Arrange
        var money = new Money(1000, Currency.NGN);

        // Act
        var result = money.Negate();

        // Assert
        result.Value.Should().Be(-1000);
        result.Currency.Should().Be(Currency.NGN);
    }
}
