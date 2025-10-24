using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Atlas.Ussd.Gateway;

/// <summary>
/// Configuration options for USSD Gateway
/// </summary>
public class UssdGatewayOptions
{
    public const string SectionName = "UssdGateway";

    /// <summary>
    /// Redis connection string
    /// </summary>
    [Required]
    public string RedisConnectionString { get; set; } = "redis:6379";

    /// <summary>
    /// Ledger service base URL
    /// </summary>
    [Required]
    [Url]
    public string LedgerBaseUrl { get; set; } = "http://ledgerapi:6181";

    /// <summary>
    /// Payments service base URL
    /// </summary>
    [Required]
    [Url]
    public string PaymentsBaseUrl { get; set; } = "http://paymentsapi:5191";

    /// <summary>
    /// Session timeout in minutes
    /// </summary>
    [Range(1, 60)]
    public int SessionTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Maximum PIN attempts per session
    /// </summary>
    [Range(1, 10)]
    public int MaxPinAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum transfer amount in minor units
    /// </summary>
    [Range(100, 1000000000)] // $1 to $10M
    public long MaxTransferAmountMinor { get; set; } = 10000000; // $100K

    /// <summary>
    /// Enable audit logging
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Rate limiting requests per minute per MSISDN
    /// </summary>
    [Range(1, 100)]
    public int RateLimitPerMinute { get; set; } = 10;
}

/// <summary>
/// USSD request model with validation
/// </summary>
public class UssdRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string SessionId { get; set; } = "";

    [Required]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Invalid MSISDN format")]
    [StringLength(20)]
    public string Msisdn { get; set; } = "";

    [StringLength(200)]
    public string Text { get; set; } = "";

    public bool NewSession { get; set; }
}

/// <summary>
/// USSD session state
/// </summary>
public class UssdState
{
    public string Step { get; set; } = "root";
    public string Msisdn { get; set; } = "";
    public Dictionary<string, string> Temp { get; set; } = new();
    public int PinAttempts { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Input validation and sanitization utilities
/// </summary>
public static class InputValidator
{
    private static readonly Regex PinRegex = new(@"^\d{4,6}$", RegexOptions.Compiled);
    private static readonly Regex AccountIdRegex = new(@"^[a-zA-Z0-9:_-]+$", RegexOptions.Compiled);
    private static readonly Regex AmountRegex = new(@"^\d+$", RegexOptions.Compiled);

    public static bool IsValidPin(string pin) => PinRegex.IsMatch(pin);
    public static bool IsValidAccountId(string accountId) => AccountIdRegex.IsMatch(accountId) && accountId.Length <= 100;
    public static bool IsValidAmount(string amount) => AmountRegex.IsMatch(amount) && long.TryParse(amount, out var amt) && amt > 0;
    public static bool IsValidMenuChoice(string choice) => choice.Length == 1 && "12345".Contains(choice);
    public static bool IsValidMsisdn(string msisdn) => !string.IsNullOrEmpty(msisdn) && msisdn.Length >= 10 && msisdn.Length <= 20;

    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        // Remove potentially dangerous characters
        return Regex.Replace(input, @"[<>""'&]", "").Trim();
    }
}

/// <summary>
/// Rate limiting policy for USSD requests
/// </summary>
public class UssdRateLimitPolicy : IRateLimiterPolicy<string>
{
    public RateLimitLease Acquire(HttpContext httpContext, RateLimitContext<string> context)
    {
        var msisdn = httpContext.Request.Form["msisdn"].FirstOrDefault() ?? "";
        var key = $"ussd_rate_limit:{msisddn}";
        
        // Simple sliding window rate limiter
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-1);
        
        // This is a simplified implementation - in production, use Redis with proper sliding window
        return RateLimitLease.Create(true, TimeSpan.FromMinutes(1));
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; set; }
}
