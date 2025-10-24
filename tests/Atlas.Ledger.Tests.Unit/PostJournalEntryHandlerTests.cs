using Microsoft.Extensions.Logging;
using Moq;
using Atlas.Ledger.App;
using Atlas.Ledger.Domain;
using Atlas.Common.ValueObjects;
using Atlas.Messaging;
using Xunit;

namespace Atlas.Ledger.Tests.Unit;

/// <summary>
/// Unit tests for PostJournalEntryHandler
/// </summary>
public class PostJournalEntryHandlerTests
{
    private readonly Mock<ILedgerRepository> _mockRepository;
    private readonly Mock<IOutboxStore> _mockOutboxStore;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<PostJournalEntryHandler>> _mockLogger;
    private readonly PostJournalEntryHandler _handler;

    public PostJournalEntryHandlerTests()
    {
        _mockRepository = new Mock<ILedgerRepository>();
        _mockOutboxStore = new Mock<IOutboxStore>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<PostJournalEntryHandler>>();

        _mockTenantContext.Setup(x => x.CurrentTenant).Returns(new TenantId("tnt_test"));

        _handler = new PostJournalEntryHandler(
            _mockRepository.Object,
            _mockOutboxStore.Object,
            _mockTenantContext.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_ValidJournalEntry_PostsSuccessfully()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var sourceAccount = new Account(
            new EntityId("acc_source"),
            tenantId,
            "acc_source",
            "Source Account",
            AccountType.Asset,
            currency
        );
        sourceAccount.RestoreBalance(new Money(1000m, currency));

        var destAccount = new Account(
            new EntityId("acc_dest"),
            tenantId,
            "acc_dest",
            "Destination Account",
            AccountType.Asset,
            currency
        );
        destAccount.RestoreBalance(new Money(500m, currency));

        var command = new PostJournalEntryCommand(
            "Test transfer",
            new[] { (new AccountId("acc_source"), new Money(100m, currency)) },
            new[] { (new AccountId("acc_dest"), new Money(100m, currency)) }
        );

        _mockRepository.Setup(x => x.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceAccount, destAccount });

        _mockRepository.Setup(x => x.SaveBatchAsync(It.IsAny<IReadOnlyList<Account>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockOutboxStore.Setup(x => x.EnqueueAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JournalEntry(
                new JournalEntryId(Guid.NewGuid()),
                tenantId,
                DateTimeOffset.UtcNow,
                "Test transfer",
                new[] { new JournalEntryLine(new AccountId("acc_source"), new Money(100m, currency), JournalEntryLineType.Debit) },
                new[] { new JournalEntryLine(new AccountId("acc_dest"), new Money(100m, currency), JournalEntryLineType.Credit) }
            ));

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test transfer", result.Narrative);
        _mockRepository.Verify(x => x.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.SaveBatchAsync(It.IsAny<IReadOnlyList<Account>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockOutboxStore.Verify(x => x.EnqueueAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnbalancedJournalEntry_ThrowsArgumentException()
    {
        // Arrange
        var currency = Currency.FromCode("USD");
        
        var command = new PostJournalEntryCommand(
            "Unbalanced transfer",
            new[] { (new AccountId("acc_source"), new Money(100m, currency)) },
            new[] { (new AccountId("acc_dest"), new Money(200m, currency)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_NegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var currency = Currency.FromCode("USD");
        
        var command = new PostJournalEntryCommand(
            "Negative amount transfer",
            new[] { (new AccountId("acc_source"), new Money(-100m, currency)) },
            new[] { (new AccountId("acc_dest"), new Money(-100m, currency)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_CurrencyMismatch_ThrowsArgumentException()
    {
        // Arrange
        var usdCurrency = Currency.FromCode("USD");
        var eurCurrency = Currency.FromCode("EUR");
        
        var command = new PostJournalEntryCommand(
            "Currency mismatch transfer",
            new[] { (new AccountId("acc_source"), new Money(100m, usdCurrency)) },
            new[] { (new AccountId("acc_dest"), new Money(100m, eurCurrency)) }
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_MissingAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var currency = Currency.FromCode("USD");
        
        var command = new PostJournalEntryCommand(
            "Missing account transfer",
            new[] { (new AccountId("acc_source"), new Money(100m, currency)) },
            new[] { (new AccountId("acc_dest"), new Money(100m, currency)) }
        );

        _mockRepository.Setup(x => x.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Account[] { }); // Empty result

        _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Account not found"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_InsufficientBalance_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = new TenantId("tnt_test");
        var currency = Currency.FromCode("USD");
        
        var sourceAccount = new Account(
            new EntityId("acc_source"),
            tenantId,
            "acc_source",
            "Source Account",
            AccountType.Asset,
            currency
        );
        sourceAccount.RestoreBalance(new Money(50m, currency)); // Insufficient balance

        var destAccount = new Account(
            new EntityId("acc_dest"),
            tenantId,
            "acc_dest",
            "Destination Account",
            AccountType.Asset,
            currency
        );
        destAccount.RestoreBalance(new Money(500m, currency));

        var command = new PostJournalEntryCommand(
            "Insufficient balance transfer",
            new[] { (new AccountId("acc_source"), new Money(100m, currency)) },
            new[] { (new AccountId("acc_dest"), new Money(100m, currency)) }
        );

        _mockRepository.Setup(x => x.GetBatchAsync(It.IsAny<AccountId[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sourceAccount, destAccount });

        _mockUnitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<JournalEntry>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Insufficient balance"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.HandleAsync(command, CancellationToken.None));
    }
}