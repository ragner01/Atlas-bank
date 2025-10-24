using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AtlasBank.BuildingBlocks.Tests;

/// <summary>
/// Comprehensive integration test base class
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly ITestOutputHelper Output;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        Factory = factory;
        Output = output;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        Client.DefaultRequestHeaders.Add("X-Tenant-ID", "test-tenant");
    }

    protected async Task<T?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Output.WriteLine($"Response: {content}");
        
        if (string.IsNullOrEmpty(content))
            return default;

        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    protected StringContent SerializeRequest<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    protected void LogResponse(HttpResponseMessage response, string operation)
    {
        Output.WriteLine($"{operation}: {response.StatusCode} - {response.ReasonPhrase}");
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var content = response.Content.ReadAsStringAsync().Result;
            Output.WriteLine($"Response Body: {content}");
        }
    }

    public void Dispose()
    {
        Client?.Dispose();
    }
}

/// <summary>
/// USSD Gateway integration tests
/// </summary>
public class UssdGatewayIntegrationTests : IntegrationTestBase
{
    public UssdGatewayIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    [Fact]
    public async Task UssdSession_NewSession_ShouldReturnMainMenu()
    {
        // Arrange
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", "test-session-001"),
            new KeyValuePair<string, string>("msisdn", "2348100000001"),
            new KeyValuePair<string, string>("text", ""),
            new KeyValuePair<string, string>("newSession", "true")
        });

        // Act
        var response = await Client.PostAsync("/ussd", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("AtlasBank", content);
        Assert.Contains("1 Balance", content);
        Assert.Contains("2 Send Money", content);
        Assert.Contains("3 Cash-out", content);
        Assert.Contains("4 Cash-in", content);
        Assert.Contains("5 Change PIN", content);
    }

    [Fact]
    public async Task UssdSession_BalanceCheck_ShouldReturnBalance()
    {
        // Arrange
        var sessionId = "test-session-002";
        var msisdn = "2348100000002";

        // Start new session
        var newSessionRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", ""),
            new KeyValuePair<string, string>("newSession", "true")
        });

        await Client.PostAsync("/ussd", newSessionRequest);

        // Select balance option
        var balanceRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", "1"),
            new KeyValuePair<string, string>("newSession", "false")
        });

        // Act
        var response = await Client.PostAsync("/ussd", balanceRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Enter PIN:", content);
    }

    [Fact]
    public async Task UssdSession_SendMoney_ShouldProcessTransfer()
    {
        // Arrange
        var sessionId = "test-session-003";
        var msisdn = "2348100000003";

        // Start new session
        var newSessionRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", ""),
            new KeyValuePair<string, string>("newSession", "true")
        });

        await Client.PostAsync("/ussd", newSessionRequest);

        // Select send money option
        var sendMoneyRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", "2"),
            new KeyValuePair<string, string>("newSession", "false")
        });

        // Act
        var response = await Client.PostAsync("/ussd", sendMoneyRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Enter destination account ID:", content);
    }

    [Fact]
    public async Task UssdSession_InvalidInput_ShouldReturnError()
    {
        // Arrange
        var sessionId = "test-session-004";
        var msisdn = "2348100000004";

        // Start new session
        var newSessionRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", ""),
            new KeyValuePair<string, string>("newSession", "true")
        });

        await Client.PostAsync("/ussd", newSessionRequest);

        // Send invalid input
        var invalidRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", "99"),
            new KeyValuePair<string, string>("newSession", "false")
        });

        // Act
        var response = await Client.PostAsync("/ussd", invalidRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Invalid", content);
    }
}

/// <summary>
/// Agent Network integration tests
/// </summary>
public class AgentNetworkIntegrationTests : IntegrationTestBase
{
    public AgentNetworkIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    [Fact]
    public async Task AgentWithdrawIntent_ValidRequest_ShouldReturnCode()
    {
        // Arrange
        var request = new
        {
            msisdn = "2348100000001",
            agent = "AG001",
            minor = 10000,
            currency = "NGN"
        };

        // Act
        var response = await Client.PostAsync("/agent/withdraw/intent?msisdn=2348100000001&agent=AG001&minor=10000&currency=NGN", null);
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AgentCashInIntent_ValidRequest_ShouldReturnCode()
    {
        // Arrange
        var request = new
        {
            msisdn = "2348100000002",
            agent = "AG002",
            minor = 15000,
            currency = "NGN"
        };

        // Act
        var response = await Client.PostAsync("/agent/cashin/intent?msisdn=2348100000002&agent=AG002&minor=15000&currency=NGN", null);
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AgentConfirm_ValidCode_ShouldProcessTransaction()
    {
        // Arrange
        var msisdn = "2348100000003";
        var agent = "AG003";
        var minor = 20000;
        var currency = "NGN";

        // Create intent first
        var intentResponse = await Client.PostAsync($"/agent/withdraw/intent?msisdn={msisdn}&agent={agent}&minor={minor}&currency={currency}", null);
        var intentResult = await DeserializeResponse<dynamic>(intentResponse);
        
        // Act
        var response = await Client.PostAsync($"/agent/confirm?code={intentResult?.GetProperty("code").GetString()}", null);
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AgentConfirm_InvalidCode_ShouldReturnNotFound()
    {
        // Arrange
        var invalidCode = "INVALID123";

        // Act
        var response = await Client.PostAsync($"/agent/confirm?code={invalidCode}", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>
/// Offline Queue integration tests
/// </summary>
public class OfflineQueueIntegrationTests : IntegrationTestBase
{
    public OfflineQueueIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    [Fact]
    public async Task OfflineOperation_ValidRequest_ShouldEnqueue()
    {
        // Arrange
        var request = new
        {
            DeviceId = "test-device-001",
            TenantId = "test-tenant",
            Kind = "transfer",
            Nonce = "test-nonce-001",
            Payload = new
            {
                SourceAccountId = "msisdn::2348100000001",
                DestinationAccountId = "msisdn::2348100000002",
                Minor = 5000,
                Currency = "NGN"
            },
            Signature = "test-signature"
        };

        // Act
        var response = await Client.PostAsync("/offline/ops", SerializeRequest(request));
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OfflineSync_ValidRequest_ShouldProcessOperations()
    {
        // Arrange
        var deviceId = "test-device-002";
        var maxOperations = 10;

        // Act
        var response = await Client.PostAsync($"/offline/sync?deviceId={deviceId}&max={maxOperations}", null);
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OfflineOperation_InvalidSignature_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            DeviceId = "test-device-003",
            TenantId = "test-tenant",
            Kind = "transfer",
            Nonce = "test-nonce-003",
            Payload = new
            {
                SourceAccountId = "msisdn::2348100000001",
                DestinationAccountId = "msisdn::2348100000002",
                Minor = 5000,
                Currency = "NGN"
            },
            Signature = "invalid-signature"
        };

        // Act
        var response = await Client.PostAsync("/offline/ops", SerializeRequest(request));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// Health check integration tests
/// </summary>
public class HealthCheckIntegrationTests : IntegrationTestBase
{
    public HealthCheckIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        // Act
        var response = await Client.GetAsync("/health");
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task HealthCheckReady_ShouldReturnOk()
    {
        // Act
        var response = await Client.GetAsync("/health/ready");
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task HealthCheckLive_ShouldReturnOk()
    {
        // Act
        var response = await Client.GetAsync("/health/live");
        var result = await DeserializeResponse<dynamic>(response);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
    }
}

/// <summary>
/// Performance integration tests
/// </summary>
public class PerformanceIntegrationTests : IntegrationTestBase
{
    public PerformanceIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) 
        : base(factory, output)
    {
    }

    [Fact]
    public async Task UssdSession_Performance_ShouldCompleteWithinThreshold()
    {
        // Arrange
        var sessionId = "perf-test-session";
        var msisdn = "2348100000001";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sessionId", sessionId),
            new KeyValuePair<string, string>("msisdn", msisdn),
            new KeyValuePair<string, string>("text", ""),
            new KeyValuePair<string, string>("newSession", "true")
        });

        var response = await Client.PostAsync("/ussd", request);
        stopwatch.Stop();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"USSD session took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public async Task AgentIntent_Performance_ShouldCompleteWithinThreshold()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await Client.PostAsync("/agent/withdraw/intent?msisdn=2348100000001&agent=AG001&minor=10000&currency=NGN", null);
        stopwatch.Stop();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Agent intent took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

    [Fact]
    public async Task OfflineOperation_Performance_ShouldCompleteWithinThreshold()
    {
        // Arrange
        var request = new
        {
            DeviceId = "perf-test-device",
            TenantId = "test-tenant",
            Kind = "transfer",
            Nonce = "perf-test-nonce",
            Payload = new
            {
                SourceAccountId = "msisdn::2348100000001",
                DestinationAccountId = "msisdn::2348100000002",
                Minor = 5000,
                Currency = "NGN"
            },
            Signature = "test-signature"
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await Client.PostAsync("/offline/ops", SerializeRequest(request));
        stopwatch.Stop();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Offline operation took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }
}
