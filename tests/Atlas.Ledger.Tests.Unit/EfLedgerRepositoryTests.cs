using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.Tests.Unit;

/// <summary>
/// Unit tests for EfLedgerRepository
/// </summary>
public class EfLedgerRepositoryTests : IDisposable
{
    private readonly LedgerDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<EfLedgerRepository>> _mockLogger;
    private readonly EfLedgerRepository _repository;

    public EfLedgerRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LedgerDbContext(options);
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<EfLedgerRepository>>();

        _mockTenantContext.Setup(tc => tc.CurrentTenant)
            .Returns(new TenantId("tnt_test"));

        _repository = new EfLedgerRepository(_dbContext, _mockTenantContext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAsync_WithExistingAccount_ReturnsAccount()
    {
        // Arrange
        var accountId = new AccountId("acc_test");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        
        var accountRow = new AccountRow
        {
            Id = accountId.Value,
            TenantId = tenantId.Value,
            Currency = currency.Code,
            LedgerCents = 10000
        };

        _dbContext.Accounts.Add(accountRow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetAsync(accountId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountId.Value, result.Id.Value);
        Assert.Equal(10000, result.Balance.LedgerCents);
    }

    [Fact]
    public async Task GetAsync_WithNonExistingAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var accountId = new AccountId("acc_nonexistent");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.GetAsync(accountId));
    }

    [Fact]
    public async Task SaveAsync_WithNewAccount_CreatesAccount()
    {
        // Arrange
        var accountId = new AccountId("acc_new");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        
        var account = new Account(
            accountId,
            tenantId,
            "acc_new",
            "New Account",
            AccountType.Asset,
            currency
        );
        account.RestoreBalance(new Money(5000, currency, 2));

        // Act
        await _repository.SaveAsync(account);

        // Assert
        var savedAccount = await _dbContext.Accounts.FindAsync(accountId.Value);
        Assert.NotNull(savedAccount);
        Assert.Equal(5000, savedAccount.LedgerCents);
    }

    [Fact]
    public async Task SaveAsync_WithExistingAccount_UpdatesAccount()
    {
        // Arrange
        var accountId = new AccountId("acc_existing");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        
        var accountRow = new AccountRow
        {
            Id = accountId.Value,
            TenantId = tenantId.Value,
            Currency = currency.Code,
            LedgerCents = 10000
        };

        _dbContext.Accounts.Add(accountRow);
        await _dbContext.SaveChangesAsync();

        var account = new Account(
            accountId,
            tenantId,
            "acc_existing",
            "Existing Account",
            AccountType.Asset,
            currency
        );
        account.RestoreBalance(new Money(15000, currency, 2));

        // Act
        await _repository.SaveAsync(account);

        // Assert
        var updatedAccount = await _dbContext.Accounts.FindAsync(accountId.Value);
        Assert.NotNull(updatedAccount);
        Assert.Equal(15000, updatedAccount.LedgerCents);
    }

    [Fact]
    public async Task GetBatchAsync_WithMultipleAccounts_ReturnsAllAccounts()
    {
        // Arrange
        var accountIds = new[]
        {
            new AccountId("acc_1"),
            new AccountId("acc_2"),
            new AccountId("acc_3")
        };

        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");

        foreach (var accountId in accountIds)
        {
            var accountRow = new AccountRow
            {
                Id = accountId.Value,
                TenantId = tenantId.Value,
                Currency = currency.Code,
                LedgerCents = 10000
            };
            _dbContext.Accounts.Add(accountRow);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetBatchAsync(accountIds);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, account => Assert.Equal(10000, account.Balance.LedgerCents));
    }

    [Fact]
    public async Task GetBatchAsync_WithEmptyArray_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetBatchAsync(Array.Empty<AccountId>());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveBatchAsync_WithMultipleAccounts_SavesAllAccounts()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        
        var accounts = new[]
        {
            CreateAccount(new AccountId("acc_batch_1"), tenantId, currency, 10000),
            CreateAccount(new AccountId("acc_batch_2"), tenantId, currency, 20000),
            CreateAccount(new AccountId("acc_batch_3"), tenantId, currency, 30000)
        };

        // Act
        await _repository.SaveBatchAsync(accounts);

        // Assert
        var savedAccounts = await _dbContext.Accounts
            .Where(a => a.Id.StartsWith("acc_batch_"))
            .ToListAsync();
        
        Assert.Equal(3, savedAccounts.Count);
        Assert.Contains(savedAccounts, a => a.LedgerCents == 10000);
        Assert.Contains(savedAccounts, a => a.LedgerCents == 20000);
        Assert.Contains(savedAccounts, a => a.LedgerCents == 30000);
    }

    [Fact]
    public async Task GetByTenantAsync_WithTenantId_ReturnsTenantAccounts()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var otherTenantId = new TenantId("tnt_other");
        var currency = Currency.FromCode("NGN");

        // Add accounts for test tenant
        var testAccounts = new[]
        {
            new AccountRow { Id = "acc_test_1", TenantId = tenantId.Value, Currency = currency.Code, LedgerCents = 10000 },
            new AccountRow { Id = "acc_test_2", TenantId = tenantId.Value, Currency = currency.Code, LedgerCents = 20000 }
        };

        // Add account for other tenant
        var otherAccount = new AccountRow { Id = "acc_other", TenantId = otherTenantId.Value, Currency = currency.Code, LedgerCents = 30000 };

        _dbContext.Accounts.AddRange(testAccounts);
        _dbContext.Accounts.Add(otherAccount);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTenantAsync(tenantId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, account => Assert.Equal(tenantId.Value, account.TenantId.Value));
    }

    private static Account CreateAccount(AccountId id, TenantId tenantId, Currency currency, long ledgerCents)
    {
        var account = new Account(
            id,
            tenantId,
            id.Value,
            $"Account {id.Value}",
            AccountType.Asset,
            currency
        );
        account.RestoreBalance(new Money(ledgerCents, currency, 2));
        return account;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
