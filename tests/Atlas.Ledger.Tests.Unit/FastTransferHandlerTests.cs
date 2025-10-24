using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using System.Data;
using Atlas.Ledger.App;
using Atlas.Common.ValueObjects;
using Xunit;

namespace Atlas.Ledger.Tests.Unit;

/// <summary>
/// Unit tests for FastTransferHandler
/// </summary>
public class FastTransferHandlerTests
{
    private readonly Mock<NpgsqlDataSource> _mockDataSource;
    private readonly Mock<NpgsqlConnection> _mockConnection;
    private readonly Mock<NpgsqlTransaction> _mockTransaction;
    private readonly Mock<NpgsqlCommand> _mockCommand;
    private readonly Mock<ILogger<FastTransferHandler>> _mockLogger;
    private readonly FastTransferHandler _handler;

    public FastTransferHandlerTests()
    {
        _mockDataSource = new Mock<NpgsqlDataSource>();
        _mockConnection = new Mock<NpgsqlConnection>();
        _mockTransaction = new Mock<NpgsqlTransaction>();
        _mockCommand = new Mock<NpgsqlCommand>();
        _mockLogger = new Mock<ILogger<FastTransferHandler>>();

        _mockDataSource.Setup(ds => ds.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockConnection.Object);
        
        _mockConnection.Setup(c => c.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockTransaction.Object);

        _handler = new FastTransferHandler(_mockDataSource.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInputs_ReturnsEntryId()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = 10000L;
        var currency = "NGN";
        var narration = "Test transfer";
        var expectedEntryId = Guid.NewGuid();

        _mockCommand.Setup(cmd => cmd.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntryId);

        // Act
        var result = await _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None);

        // Assert
        Assert.Equal(expectedEntryId, result.entryId);
        Assert.False(result.duplicate);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTenantId_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "invalid-tenant"; // Invalid format
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = 10000L;
        var currency = "NGN";
        var narration = "Test transfer";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCurrency_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = 10000L;
        var currency = "INVALID"; // Not in supported currencies
        var narration = "Test transfer";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithSameSourceAndDestination_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_same";
        var dst = "acc_same"; // Same as source
        var amount = 10000L;
        var currency = "NGN";
        var narration = "Test transfer";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = -1000L; // Negative amount
        var currency = "NGN";
        var narration = "Test transfer";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyNarration_ThrowsArgumentException()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = 10000L;
        var currency = "NGN";
        var narration = ""; // Empty narration

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateKey_ReturnsNullEntryId()
    {
        // Arrange
        var key = "test-key-123";
        var tenant = "tnt_demo";
        var src = "acc_source";
        var dst = "acc_dest";
        var amount = 10000L;
        var currency = "NGN";
        var narration = "Test transfer";

        _mockCommand.Setup(cmd => cmd.ExecuteScalarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null); // Simulate duplicate

        // Act
        var result = await _handler.ExecuteAsync(key, tenant, src, dst, amount, currency, narration, CancellationToken.None);

        // Assert
        Assert.Null(result.entryId);
        Assert.True(result.duplicate);
    }
}
