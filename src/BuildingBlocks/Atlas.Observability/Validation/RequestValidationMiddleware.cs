using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtlasBank.BuildingBlocks.Validation;

/// <summary>
/// Comprehensive request validation middleware
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Validate request size
            if (context.Request.ContentLength > 10 * 1024 * 1024) // 10MB limit
            {
                _logger.LogWarning("Request too large: {ContentLength} bytes", context.Request.ContentLength);
                context.Response.StatusCode = 413;
                await context.Response.WriteAsync("Request too large");
                return;
            }

            // Validate content type for POST/PUT requests
            if (context.Request.Method is "POST" or "PUT" or "PATCH")
            {
                var contentType = context.Request.ContentType;
                if (string.IsNullOrEmpty(contentType) || !IsValidContentType(contentType))
                {
                    _logger.LogWarning("Invalid content type: {ContentType}", contentType);
                    context.Response.StatusCode = 415;
                    await context.Response.WriteAsync("Unsupported media type");
                    return;
                }
            }

            // Validate headers
            if (!ValidateHeaders(context.Request.Headers))
            {
                _logger.LogWarning("Invalid headers detected");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid headers");
                return;
            }

            // Validate query parameters
            if (!ValidateQueryParameters(context.Request.Query))
            {
                _logger.LogWarning("Invalid query parameters detected");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid query parameters");
                return;
            }

            // Validate path parameters
            if (!ValidatePathParameters(context.Request.Path))
            {
                _logger.LogWarning("Invalid path parameters detected");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid path parameters");
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request validation error");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Request validation failed");
        }
    }

    private static bool IsValidContentType(string contentType)
    {
        var validTypes = new[]
        {
            "application/json",
            "application/x-www-form-urlencoded",
            "multipart/form-data",
            "text/plain"
        };

        return validTypes.Any(type => contentType.StartsWith(type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateHeaders(IHeaderDictionary headers)
    {
        // Check for suspicious headers
        var suspiciousHeaders = new[]
        {
            "x-forwarded-for",
            "x-real-ip",
            "x-forwarded-proto"
        };

        foreach (var header in suspiciousHeaders)
        {
            if (headers.ContainsKey(header))
            {
                var value = headers[header].ToString();
                if (ContainsSuspiciousContent(value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateQueryParameters(IQueryCollection query)
    {
        foreach (var param in query)
        {
            if (ContainsSuspiciousContent(param.Value.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidatePathParameters(PathString path)
    {
        var pathString = path.Value ?? "";
        
        // Check for path traversal attempts
        if (pathString.Contains("..") || pathString.Contains("//"))
        {
            return false;
        }

        // Check for suspicious patterns
        if (ContainsSuspiciousContent(pathString))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsSuspiciousContent(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var suspiciousPatterns = new[]
        {
            @"<script",
            @"javascript:",
            @"vbscript:",
            @"onload\s*=",
            @"onerror\s*=",
            @"eval\s*\(",
            @"expression\s*\(",
            @"url\s*\(",
            @"@import",
            @"\x00", // null byte
            @"\x1a", // substitute character
            @"\x1b"  // escape character
        };

        return suspiciousPatterns.Any(pattern => 
            Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
    }
}

/// <summary>
/// Model validation middleware
/// </summary>
public class ModelValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ModelValidationMiddleware> _logger;

    public ModelValidationMiddleware(RequestDelegate next, ILogger<ModelValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Validate request body if present
            if (context.Request.ContentLength > 0 && 
                context.Request.ContentType?.StartsWith("application/json") == true)
            {
                context.Request.EnableBuffering();
                
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        JsonDocument.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in request body");
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsync("Invalid JSON");
                        return;
                    }
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model validation error");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Model validation failed");
        }
    }
}

/// <summary>
/// Input sanitization utilities
/// </summary>
public static class InputSanitizer
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex ScriptRegex = new(@"<script[^>]*>.*?</script>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleRegex = new(@"<style[^>]*>.*?</style>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Sanitizes HTML content by removing dangerous tags
    /// </summary>
    public static string SanitizeHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove script tags
        input = ScriptRegex.Replace(input, "");
        
        // Remove style tags
        input = StyleRegex.Replace(input, "");
        
        // Remove all HTML tags
        input = HtmlTagRegex.Replace(input, "");
        
        // Decode HTML entities
        input = System.Net.WebUtility.HtmlDecode(input);
        
        return input.Trim();
    }

    /// <summary>
    /// Sanitizes SQL input by escaping special characters
    /// </summary>
    public static string SanitizeSql(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace("'", "''")
                   .Replace(";", "")
                   .Replace("--", "")
                   .Replace("/*", "")
                   .Replace("*/", "");
    }

    /// <summary>
    /// Sanitizes file path input
    /// </summary>
    public static string SanitizeFilePath(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars())
            .Distinct()
            .ToArray();

        foreach (var invalidChar in invalidChars)
        {
            input = input.Replace(invalidChar, '_');
        }

        // Remove path traversal attempts
        input = input.Replace("..", ".");
        input = input.Replace("//", "/");

        return input.Trim();
    }

    /// <summary>
    /// Validates email address format
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates phone number format (basic validation)
    /// </summary>
    public static bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return false;

        // Basic phone number validation (digits, +, -, spaces, parentheses)
        var phoneRegex = new Regex(@"^[\+]?[0-9\s\-\(\)]{7,20}$");
        return phoneRegex.IsMatch(phoneNumber);
    }

    /// <summary>
    /// Validates MSISDN format
    /// </summary>
    public static bool IsValidMsisdn(string msisdn)
    {
        if (string.IsNullOrEmpty(msisdn))
            return false;

        // MSISDN should start with country code and be 10-15 digits
        var msisdnRegex = new Regex(@"^[1-9][0-9]{9,14}$");
        return msisdnRegex.IsMatch(msisdn);
    }

    /// <summary>
    /// Validates currency code format
    /// </summary>
    public static bool IsValidCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode))
            return false;

        // Currency code should be 3 uppercase letters
        var currencyRegex = new Regex(@"^[A-Z]{3}$");
        return currencyRegex.IsMatch(currencyCode);
    }

    /// <summary>
    /// Validates amount (positive decimal)
    /// </summary>
    public static bool IsValidAmount(decimal amount)
    {
        return amount > 0 && amount <= 999999999.99m; // Max 999M with 2 decimal places
    }

    /// <summary>
    /// Validates account ID format
    /// </summary>
    public static bool IsValidAccountId(string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
            return false;

        // Account ID should be in format "type::identifier"
        var accountRegex = new Regex(@"^[a-zA-Z0-9_]+::[a-zA-Z0-9_\-\.]+$");
        return accountRegex.IsMatch(accountId);
    }
}

/// <summary>
/// Custom validation attributes
/// </summary>
public class MsisdnAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string msisdn)
            return false;

        return InputSanitizer.IsValidMsisdn(msisdn);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid MSISDN (10-15 digits starting with country code)";
    }
}

public class CurrencyCodeAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string currencyCode)
            return false;

        return InputSanitizer.IsValidCurrencyCode(currencyCode);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid 3-letter currency code";
    }
}

public class AccountIdAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string accountId)
            return false;

        return InputSanitizer.IsValidAccountId(accountId);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be in format 'type::identifier'";
    }
}

public class PositiveAmountAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not decimal amount)
            return false;

        return InputSanitizer.IsValidAmount(amount);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a positive amount not exceeding 999,999,999.99";
    }
}
