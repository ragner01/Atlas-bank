using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Atlas.Common.ValueObjects;

namespace Atlas.Testing;

/// <summary>
/// Base class for integration tests with test containers
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgreSqlContainer PostgresContainer { get; private set; } = null!;
    protected KafkaContainer KafkaContainer { get; private set; } = null!;
    protected RedisContainer RedisContainer { get; private set; } = null!;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected IServiceScope ServiceScope { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        // Start containers
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("atlas_test")
            .WithUsername("test")
            .WithPassword("test")
            .WithPortBinding(5432, true)
            .Build();

        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:latest")
            .WithPortBinding(9092, true)
            .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", "zookeeper:2181")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://localhost:9092")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .Build();

        RedisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .Build();

        await Task.WhenAll(
            PostgresContainer.StartAsync(),
            KafkaContainer.StartAsync(),
            RedisContainer.StartAsync()
        );

        // Setup services
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        ServiceScope = ServiceProvider.CreateScope();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Override in derived classes
    }

    public virtual async Task DisposeAsync()
    {
        ServiceScope?.Dispose();
        ServiceProvider?.Dispose();

        await Task.WhenAll(
            PostgresContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            KafkaContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            RedisContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask
        );
    }
}

/// <summary>
/// Test data generators using Bogus
/// </summary>
public static class TestDataGenerator
{
    private static readonly Faker _faker = new();

    public static TenantId GenerateTenantId() => new(_faker.Company.CompanyName().ToLowerInvariant().Replace(" ", "-"));

    public static EntityId GenerateEntityId() => EntityId.NewId();

    public static Money GenerateMoney(Currency? currency = null)
    {
        currency ??= Currency.NGN;
        var amount = _faker.Random.Decimal(1, 1000000);
        return new Money(amount, currency);
    }

    public static string GenerateEmail() => _faker.Internet.Email();

    public static string GenerateName() => _faker.Name.FullName();

    public static string GeneratePhoneNumber() => _faker.Phone.PhoneNumber();

    public static string GenerateAccountNumber() => _faker.Random.Replace("##########");

    public static string GenerateTransactionId() => $"trn_{_faker.Random.AlphaNumeric(12)}";

    public static string GenerateCardNumber() => _faker.Finance.CreditCardNumber();

    public static DateTimeOffset GenerateDateOfBirth() => _faker.Date.Between(DateTimeOffset.Now.AddYears(-80), DateTimeOffset.Now.AddYears(-18));

    public static string GenerateAddress() => _faker.Address.FullAddress();

    public static string GenerateNarration() => _faker.Lorem.Sentence();
}

/// <summary>
/// Test assertions extensions
/// </summary>
public static class TestAssertions
{
    public static void ShouldBeSuccessful<T>(this Result<T> result)
    {
        result.IsSuccess.Should().BeTrue($"Expected success but got error: {result.Error}");
    }

    public static void ShouldBeSuccessful(this Result result)
    {
        result.IsSuccess.Should().BeTrue($"Expected success but got error: {result.Error}");
    }

    public static void ShouldBeFailure<T>(this Result<T> result, string? expectedError = null)
    {
        result.IsSuccess.Should().BeFalse("Expected failure but got success");
        if (expectedError != null)
        {
            result.Error.Should().Contain(expectedError);
        }
    }

    public static void ShouldBeFailure(this Result result, string? expectedError = null)
    {
        result.IsSuccess.Should().BeFalse("Expected failure but got success");
        if (expectedError != null)
        {
            result.Error.Should().Contain(expectedError);
        }
    }

    public static void ShouldHaveValue<T>(this Result<T> result, T expectedValue)
    {
        result.ShouldBeSuccessful();
        result.Value.Should().Be(expectedValue);
    }

    public static void ShouldHaveValue<T>(this Result<T> result, Func<T, bool> predicate)
    {
        result.ShouldBeSuccessful();
        result.Value.Should().NotBeNull();
        predicate(result.Value!).Should().BeTrue();
    }
}

/// <summary>
/// Mock service builder for testing
/// </summary>
public class MockServiceBuilder
{
    private readonly IServiceCollection _services = new ServiceCollection();

    public MockServiceBuilder AddMock<T>() where T : class
    {
        _services.AddSingleton(Mock.Of<T>());
        return this;
    }

    public MockServiceBuilder AddMock<T>(Action<Mock<T>> configure) where T : class
    {
        var mock = new Mock<T>();
        configure(mock);
        _services.AddSingleton(mock.Object);
        return this;
    }

    public MockServiceBuilder AddScopedMock<T>() where T : class
    {
        _services.AddScoped(Mock.Of<T>);
        return this;
    }

    public MockServiceBuilder AddScopedMock<T>(Action<Mock<T>> configure) where T : class
    {
        var mock = new Mock<T>();
        configure(mock);
        _services.AddScoped(_ => mock.Object);
        return this;
    }

    public IServiceProvider Build()
    {
        return _services.BuildServiceProvider();
    }
}

/// <summary>
/// Test context for sharing data between test methods
/// </summary>
public class TestContext
{
    public TenantId TenantId { get; set; } = TestDataGenerator.GenerateTenantId();
    public Dictionary<string, object> Data { get; } = new();
    public List<IDomainEvent> Events { get; } = new();

    public T GetData<T>(string key) where T : class
    {
        return Data.TryGetValue(key, out var value) ? (T)value : throw new KeyNotFoundException($"Key '{key}' not found");
    }

    public void SetData<T>(string key, T value) where T : class
    {
        Data[key] = value;
    }

    public void AddEvent(IDomainEvent domainEvent)
    {
        Events.Add(domainEvent);
    }

    public void ClearEvents()
    {
        Events.Clear();
    }
}
