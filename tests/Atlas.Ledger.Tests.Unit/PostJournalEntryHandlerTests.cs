using Microsoft.Extensions.Logging;
using Moq;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.Tests.Unit;

/// <summary>
/// Unit tests for PostJournalEntryHandler
/// </summary>
public class PostJournalEntryHandlerTests
{
    private readonly Mock<ILedgerRepository> _mockRepository;
    private readonly Mock<IOutboxStore> _mockOutbox;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<PostJournalEntryHandler>> _mockLogger;
    private readonly PostJournalEntryHandler _handler;

    public PostJournalEntryHandlerTests()
    {
        _mockRepository = new Mock<ILedgerRepository>();
        _mockOutbox = new Mock<IOutboxStore>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<PostJournalEntryHandler>>();

        _mockTenantContext.Setup(tc => tc.IsValid).Returns(true);
        _mockTenantContext.Setup(tc => tc.CurrentTenant)
            .Returns(new TenantId("tnt_test"));

        _handler = new PostJournalEntryHandler(
            _mockRepository.Object,
            _mockOutbox.Object,
            _mockTenantContext.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ReturnsJournalEntry()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        var sourceAccountId = new AccountId("acc_source");
        var destAccountId = new AccountId("acc_dest");
        var amount = new Money(10000, currency, 2);

        var sourceAccount = CreateAccount(sourceAccountId, tenantId, currency, 50000);
        var destAccount = CreateAccount(destAccountId, tenantId, currency, 20000);

        _mockRepository.Setup(r => r.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceAccount, destAccount });

        _mockRepository.Setup(r => r.SaveBatchAsync(It.IsAny<IReadOnlyList<Account>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<JournalEntry>>, CancellationToken>(async (action, ct) => await action());

        var command = new PostJournalEntryCommand(
            "Test transfer",
            new[] { (sourceAccountId, amount) },
            new[] { (destAccountId, amount) }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test transfer", result.Narration);
        Assert.True(result.IsPosted);
        Assert.Equal(2, result.Lines.Count());
    }

    [Fact]
    public async Task HandleAsync_WithInvalidTenantContext_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockTenantContext.Setup(tc => tc.IsValid).Returns(false);

        var command = new PostJournalEntryCommand(
            "Test transfer",
            new[] { (new AccountId("acc_source"), new Money(10000, Currency.FromCode("NGN"), 2)) },
            new[] { (new AccountId("acc_dest"), new Money(10000, Currency.FromCode("NGN"), 2)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithEmptyNarration_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "", // Empty narration
            new[] { (new AccountId("acc_source"), new Money(10000, Currency.FromCode("NGN"), 2)) },
            new[] { (new AccountId("acc_dest"), new Money(10000, Currency.FromCode("NGN"), 2)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithUnbalancedEntries_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "Unbalanced transfer",
            new[] { (new AccountId("acc_source"), new Money(10000, Currency.FromCode("NGN"), 2)) },
            new[] { (new AccountId("acc_dest"), new Money(20000, Currency.FromCode("NGN"), 2)) } // Different amount
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithNoDebits_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "No debits",
            Array.Empty<(AccountId, Money)>(), // No debits
            new[] { (new AccountId("acc_dest"), new Money(10000, Currency.FromCode("NGN"), 2)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithNoCredits_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "No credits",
            new[] { (new AccountId("acc_source"), new Money(10000, Currency.FromCode("NGN"), 2)) },
            Array.Empty<(AccountId, Money)>() // No credits
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithMixedCurrencies_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "Mixed currencies",
            new[] { (new AccountId("acc_source"), new Money(10000, Currency.FromCode("NGN"), 2)) },
            new[] { (new AccountId("acc_dest"), new Money(10000, Currency.FromCode("USD"), 2)) } // Different currency
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithNegativeAmounts_ThrowsArgumentException()
    {
        // Arrange
        var command = new PostJournalEntryCommand(
            "Negative amounts",
            new[] { (new AccountId("acc_source"), new Money(-10000, Currency.FromCode("NGN"), 2)) }, // Negative amount
            new[] { (new AccountId("acc_dest"), new Money(-10000, Currency.FromCode("NGN"), 2)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithMissingAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        var sourceAccountId = new AccountId("acc_source");
        var destAccountId = new AccountId("acc_dest");
        var amount = new Money(10000, currency, 2);

        // Only return one account, missing the other
        var sourceAccount = CreateAccount(sourceAccountId, tenantId, currency, 50000);
        _mockRepository.Setup(r => r.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceAccount }); // Missing destAccount

        _mockUnitOfWork.Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<JournalEntry>>, CancellationToken>(async (action, ct) => await action());

        var command = new PostJournalEntryCommand(
            "Test transfer",
            new[] { (sourceAccountId, amount) },
            new[] { (destAccountId, amount) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithInsufficientFunds_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("NGN");
        var sourceAccountId = new AccountId("acc_source");
        var destAccountId = new AccountId("acc_dest");
        var amount = new Money(10000, currency, 2);

        // Create account with insufficient funds
        var sourceAccount = CreateAccount(sourceAccountId, tenantId, currency, 5000); // Less than transfer amount
        var destAccount = CreateAccount(destAccountId, tenantId, currency, 20000);

        _mockRepository.Setup(r => r.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceAccount, destAccount });

        _mockUnitOfWork.Setup(uow => uow.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<JournalEntry>>, CancellationToken>(async (action, ct) => await action());

        var command = new PostJournalEntryCommand(
            "Test transfer",
            new[] { (sourceAccountId, amount) },
            new[] { (destAccountId, amount) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
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
}
