using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtlasBank.Sdk;

/// <summary>
/// Configuration for AtlasBank SDK
/// </summary>
public class AtlasClientOptions
{
    /// <summary>
    /// Base URL for the API
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Tenant ID for multi-tenancy support
    /// </summary>
    public string TenantId { get; set; } = "tnt_demo";

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Request timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Retry configuration
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Retry configuration
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int Attempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries in milliseconds
    /// </summary>
    public int DelayMs { get; set; } = 1000;

    /// <summary>
    /// Backoff multiplier for exponential backoff
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Number of failures before opening circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Duration to keep circuit open in milliseconds
    /// </summary>
    public int DurationOfBreakMs { get; set; } = 30000;
}

/// <summary>
/// Standard API response format
/// </summary>
/// <typeparam name="T">Response data type</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Response data
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error information
    /// </summary>
    public ApiError? Error { get; set; }

    /// <summary>
    /// Response metadata
    /// </summary>
    public ApiMetadata? Meta { get; set; }
}

/// <summary>
/// API error information
/// </summary>
public class ApiError
{
    /// <summary>
    /// Error code
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Additional error details
    /// </summary>
    public object? Details { get; set; }
}

/// <summary>
/// API response metadata
/// </summary>
public class ApiMetadata
{
    /// <summary>
    /// Request ID
    /// </summary>
    public string RequestId { get; set; } = "";

    /// <summary>
    /// Response timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// API version
    /// </summary>
    public string Version { get; set; } = "";
}

/// <summary>
/// Transfer request parameters
/// </summary>
public class TransferRequest
{
    /// <summary>
    /// Source account ID
    /// </summary>
    public string SourceAccountId { get; set; } = "";

    /// <summary>
    /// Destination account ID
    /// </summary>
    public string DestinationAccountId { get; set; } = "";

    /// <summary>
    /// Amount in minor units
    /// </summary>
    public long Minor { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = "";

    /// <summary>
    /// Transfer narration
    /// </summary>
    public string? Narration { get; set; }
}

/// <summary>
/// Card charge request parameters
/// </summary>
public class CardChargeRequest
{
    /// <summary>
    /// Amount in minor units
    /// </summary>
    public long AmountMinor { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = "";

    /// <summary>
    /// Card token
    /// </summary>
    public string CardToken { get; set; } = "";

    /// <summary>
    /// Merchant ID
    /// </summary>
    public string MerchantId { get; set; } = "";

    /// <summary>
    /// Merchant Category Code
    /// </summary>
    public string Mcc { get; set; } = "";

    /// <summary>
    /// Device ID
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string? Ip { get; set; }
}

/// <summary>
/// AtlasBank API exception
/// </summary>
public class AtlasApiException : Exception
{
    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Error code
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Error details
    /// </summary>
    public object? Details { get; }

    public AtlasApiException(int statusCode, string code, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }
}

/// <summary>
/// Main AtlasBank SDK client
/// </summary>
public sealed class AtlasClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AtlasClientOptions _options;
    private readonly ILogger<AtlasClient>? _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;
    private bool _disposed;

    /// <summary>
    /// Initialize AtlasClient with options
    /// </summary>
    public AtlasClient(AtlasClientOptions options, ILogger<AtlasClient>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        if (string.IsNullOrEmpty(_options.BaseUrl))
        {
            throw new ArgumentException("BaseUrl is required", nameof(options));
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs)
        };

        // Configure default headers
        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", _options.TenantId);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AtlasBank.Sdk/0.1.0");

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
        }

        // Configure resilience policies
        _resiliencePolicy = CreateResiliencePolicy();
    }

    /// <summary>
    /// Initialize AtlasClient with base URL and tenant ID
    /// </summary>
    public AtlasClient(string baseUrl, string tenantId = "tnt_demo", ILogger<AtlasClient>? logger = null)
        : this(new AtlasClientOptions { BaseUrl = baseUrl, TenantId = tenantId }, logger)
    {
    }

    /// <summary>
    /// Transfer money with risk assessment
    /// </summary>
    public async Task<ApiResponse<T>> TransferWithRisk<T>(
        TransferRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTransferRequest(request);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/payments/transfers/with-risk")
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await SendRequestAsync<ApiResponse<T>>(httpRequest, cancellationToken);
    }

    /// <summary>
    /// Charge a card with limits enforcement
    /// </summary>
    public async Task<ApiResponse<T>> ChargeCardEnforced<T>(
        CardChargeRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCardChargeRequest(request);

        var queryParams = new List<string>
        {
            $"amountMinor={request.AmountMinor}",
            $"currency={Uri.EscapeDataString(request.Currency)}",
            $"cardToken={Uri.EscapeDataString(request.CardToken)}",
            $"merchantId={Uri.EscapeDataString(request.MerchantId)}",
            $"mcc={Uri.EscapeDataString(request.Mcc)}"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/payments/cnp/charge/enforced?{string.Join("&", queryParams)}");

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (!string.IsNullOrEmpty(request.DeviceId))
        {
            httpRequest.Headers.Add("X-Device-Id", request.DeviceId);
        }

        if (!string.IsNullOrEmpty(request.Ip))
        {
            httpRequest.Headers.Add("X-IP", request.Ip);
        }

        return await SendRequestAsync<ApiResponse<T>>(httpRequest, cancellationToken);
    }

    /// <summary>
    /// Get account balance
    /// </summary>
    public async Task<ApiResponse<T>> GetBalance<T>(
        string accountId,
        string? currency = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            throw new ArgumentException("Account ID is required", nameof(accountId));
        }

        var path = $"/ledger/accounts/{Uri.EscapeDataString(accountId)}/balance/global";
        if (!string.IsNullOrEmpty(currency))
        {
            path += $"?currency={Uri.EscapeDataString(currency)}";
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendRequestAsync<ApiResponse<T>>(httpRequest, cancellationToken);
    }

    /// <summary>
    /// Get trust badge URL
    /// </summary>
    public string TrustBadgeUrl(string baseUrl, string entityId)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new ArgumentException("Base URL is required", nameof(baseUrl));
        }

        if (string.IsNullOrEmpty(entityId))
        {
            throw new ArgumentException("Entity ID is required", nameof(entityId));
        }

        return $"{baseUrl.TrimEnd('/')}/badge/{Uri.EscapeDataString(entityId)}.svg";
    }

    /// <summary>
    /// Send HTTP request with resilience policies
    /// </summary>
    private async Task<T> SendRequestAsync<T>(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Add request ID
            request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());

            _logger?.LogDebug("Sending request: {Method} {Uri}", request.Method, request.RequestUri);

            var response = await _resiliencePolicy.ExecuteAsync(async (ct) =>
            {
                return await _httpClient.SendAsync(request, ct);
            }, cancellationToken);

            _logger?.LogDebug("Received response: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                ApiError? error = null;

                try
                {
                    error = JsonSerializer.Deserialize<ApiError>(errorContent);
                }
                catch
                {
                    // If we can't deserialize the error, create a generic one
                }

                throw new AtlasApiException(
                    (int)response.StatusCode,
                    error?.Code ?? "HTTP_ERROR",
                    error?.Message ?? $"HTTP {(int)response.StatusCode}",
                    error?.Details
                );
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.Contains("application/json"))
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidOperationException("Failed to deserialize response");
            }

            var textContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return (T)(object)textContent;
        }
        catch (AtlasApiException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new AtlasApiException(408, "TIMEOUT", "Request timeout");
        }
        catch (HttpRequestException ex)
        {
            throw new AtlasApiException(0, "NETWORK_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            throw new AtlasApiException(0, "UNKNOWN_ERROR", ex.Message);
        }
    }

    /// <summary>
    /// Create resilience policy with retry and circuit breaker
    /// </summary>
    private IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy()
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode >= 500)
            .WaitAndRetryAsync(
                _options.Retry.Attempts,
                retryAttempt => TimeSpan.FromMilliseconds(
                    _options.Retry.DelayMs * Math.Pow(_options.Retry.BackoffMultiplier, retryAttempt - 1)
                ),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger?.LogWarning("Retry {RetryCount} in {Delay}ms due to {Exception}",
                        retryCount, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode >= 500)
            .CircuitBreakerAsync(
                _options.CircuitBreaker.FailureThreshold,
                TimeSpan.FromMilliseconds(_options.CircuitBreaker.DurationOfBreakMs),
                onBreak: (exception, duration) =>
                {
                    _logger?.LogWarning("Circuit breaker opened for {Duration} due to {Exception}",
                        duration, exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    _logger?.LogInformation("Circuit breaker reset - service is healthy again");
                },
                onHalfOpen: () =>
                {
                    _logger?.LogInformation("Circuit breaker half-open - testing service health");
                });

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Validate transfer request
    /// </summary>
    private static void ValidateTransferRequest(TransferRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrEmpty(request.SourceAccountId))
        {
            throw new ArgumentException("SourceAccountId is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.DestinationAccountId))
        {
            throw new ArgumentException("DestinationAccountId is required", nameof(request));
        }

        if (request.Minor <= 0)
        {
            throw new ArgumentException("Minor amount must be positive", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Currency))
        {
            throw new ArgumentException("Currency is required", nameof(request));
        }
    }

    /// <summary>
    /// Validate card charge request
    /// </summary>
    private static void ValidateCardChargeRequest(CardChargeRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.AmountMinor <= 0)
        {
            throw new ArgumentException("AmountMinor must be positive", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Currency))
        {
            throw new ArgumentException("Currency is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.CardToken))
        {
            throw new ArgumentException("CardToken is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.MerchantId))
        {
            throw new ArgumentException("MerchantId is required", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Mcc))
        {
            throw new ArgumentException("Mcc is required", nameof(request));
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
