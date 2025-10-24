using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Atlas.Common.ValueObjects;
using Xunit;

namespace Atlas.Ledger.Tests.Unit;

/// <summary>
/// Unit tests for EfLedgerRepository
/// </summary>
public class EfLedgerRepositoryTests
{
    private readonly DbContextOptions<LedgerDbContext> _options;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<EfLedgerRepository>> _mockLogger;

    public EfLedgerRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(x => x.CurrentTenant).Returns(new TenantId("tnt_test"));

        _mockLogger = new Mock<ILogger<EfLedgerRepository>>();
    }

    [Fact]
    public async Task GetAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var accountId = new AccountId("acc_001");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var account = new Account(
            new EntityId(accountId.Value),
            tenantId,
            accountId.Value,
            "Test Account",
            AccountType.Asset,
            currency
        );
        account.RestoreBalance(new Money(1000m, currency));

        await repository.SaveAsync(account, CancellationToken.None);

        // Act
        var result = await repository.GetAsync(accountId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountId.Value, result.Id.Value);
        Assert.Equal("Test Account", result.Name);
        Assert.Equal(1000m, result.Balance.Value);
    }

    [Fact]
    public async Task GetAsync_NonExistentAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        var accountId = new AccountId("acc_nonexistent");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            repository.GetAsync(accountId, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_NewAccount_SavesSuccessfully()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var accountId = new AccountId("acc_002");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("EUR");
        
        var account = new Account(
            new EntityId(accountId.Value),
            tenantId,
            accountId.Value,
            "New Account",
            AccountType.Liability,
            currency
        );

        // Act
        await repository.SaveAsync(account, CancellationToken.None);

        // Assert
        var savedAccount = await repository.GetAsync(accountId, CancellationToken.None);
        Assert.NotNull(savedAccount);
        Assert.Equal("New Account", savedAccount.Name);
        Assert.Equal(AccountType.Liability, savedAccount.Type);
    }

    [Fact]
    public async Task SaveAsync_ExistingAccount_UpdatesSuccessfully()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var accountId = new AccountId("acc_003");
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("GBP");
        
        var account = new Account(
            new EntityId(accountId.Value),
            tenantId,
            accountId.Value,
            "Original Name",
            AccountType.Asset,
            currency
        );
        account.RestoreBalance(new Money(500m, currency));

        await repository.SaveAsync(account, CancellationToken.None);

        // Act - Update the account
        account.RestoreBalance(new Money(750m, currency));
        await repository.SaveAsync(account, CancellationToken.None);

        // Assert
        var updatedAccount = await repository.GetAsync(accountId, CancellationToken.None);
        Assert.Equal(750m, updatedAccount.Balance.Value);
    }

    [Fact]
    public async Task GetBatchAsync_MultipleAccounts_ReturnsAllAccounts()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var accounts = new[]
        {
            new Account(new EntityId("acc_batch_1"), tenantId, "acc_batch_1", "Account 1", AccountType.Asset, currency),
            new Account(new EntityId("acc_batch_2"), tenantId, "acc_batch_2", "Account 2", AccountType.Liability, currency),
            new Account(new EntityId("acc_batch_3"), tenantId, "acc_batch_3", "Account 3", AccountType.Equity, currency)
        };

        await repository.SaveBatchAsync(accounts, CancellationToken.None);

        // Act
        var accountIds = new[] { new AccountId("acc_batch_1"), new AccountId("acc_batch_2"), new AccountId("acc_batch_3") };
        var result = await repository.GetBatchAsync(accountIds, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, a => a.Name == "Account 1");
        Assert.Contains(result, a => a.Name == "Account 2");
        Assert.Contains(result, a => a.Name == "Account 3");
    }

    [Fact]
    public async Task GetBatchAsync_PartialMatch_ReturnsOnlyExistingAccounts()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var account = new Account(new EntityId("acc_partial_1"), tenantId, "acc_partial_1", "Existing Account", AccountType.Asset, currency);
        await repository.SaveAsync(account, CancellationToken.None);

        // Act
        var accountIds = new[] { new AccountId("acc_partial_1"), new AccountId("acc_partial_nonexistent") };
        var result = await repository.GetBatchAsync(accountIds, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("Existing Account", result[0].Name);
    }

    [Fact]
    public async Task SaveBatchAsync_MultipleAccounts_SavesAllSuccessfully()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var accounts = new[]
        {
            new Account(new EntityId("acc_save_1"), tenantId, "acc_save_1", "Save Account 1", AccountType.Asset, currency),
            new Account(new EntityId("acc_save_2"), tenantId, "acc_save_2", "Save Account 2", AccountType.Liability, currency)
        };

        // Act
        await repository.SaveBatchAsync(accounts, CancellationToken.None);

        // Assert
        var account1 = await repository.GetAsync(new AccountId("acc_save_1"), CancellationToken.None);
        var account2 = await repository.GetAsync(new AccountId("acc_save_2"), CancellationToken.None);
        
        Assert.Equal("Save Account 1", account1.Name);
        Assert.Equal("Save Account 2", account2.Name);
    }

    [Fact]
    public async Task GetByTenantAsync_MultipleTenants_ReturnsOnlyMatchingTenant()
    {
        // Arrange
        using var context = new LedgerDbContext(_options);
        var repository = new EfLedgerRepository(context, _mockTenantContext.Object, _mockLogger.Object);
        
        var tenant1 = new TenantId("tnt_tenant1");
        var tenant2 = new TenantId("tnt_tenant2");
        var currency = Currency.FromCode("USD");
        
        var accounts = new[]
        {
            new Account(new EntityId("acc_tenant1_1"), tenant1, "acc_tenant1_1", "Tenant1 Account 1", AccountType.Asset, currency),
            new Account(new EntityId("acc_tenant1_2"), tenant1, "acc_tenant1_2", "Tenant1 Account 2", AccountType.Liability, currency),
            new Account(new EntityId("acc_tenant2_1"), tenant2, "acc_tenant2_1", "Tenant2 Account 1", AccountType.Asset, currency)
        };

        await repository.SaveBatchAsync(accounts, CancellationToken.None);

        // Act
        var result = await repository.GetByTenantAsync(tenant1, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, account => Assert.Equal(tenant1, account.TenantId));
        Assert.Contains(result, a => a.Name == "Tenant1 Account 1");
        Assert.Contains(result, a => a.Name == "Tenant1 Account 2");
    }
}