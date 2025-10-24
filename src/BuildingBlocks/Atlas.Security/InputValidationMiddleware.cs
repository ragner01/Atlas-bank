using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AtlasBank.Api.Middleware;

/// <summary>
/// Middleware for comprehensive input validation and sanitization
/// </summary>
public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;

    public InputValidationMiddleware(RequestDelegate next, ILogger<InputValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate request size
        if (context.Request.ContentLength > 10 * 1024 * 1024) // 10MB limit
        {
            _logger.LogWarning("Request too large: {ContentLength} bytes from {RemoteIp}", 
                context.Request.ContentLength, context.Connection.RemoteIpAddress);
            
            context.Response.StatusCode = 413; // Payload Too Large
            await context.Response.WriteAsync("Request payload too large");
            return;
        }

        // Validate headers
        if (!ValidateHeaders(context))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid headers");
            return;
        }

        // Sanitize query parameters
        SanitizeQueryParameters(context);

        await _next(context);
    }

    private bool ValidateHeaders(HttpContext context)
    {
        var request = context.Request;

        // Check for suspicious headers
        var suspiciousHeaders = new[]
        {
            "X-Forwarded-For", "X-Real-IP", "X-Originating-IP",
            "X-Remote-IP", "X-Remote-Addr", "X-Client-IP"
        };

        foreach (var header in suspiciousHeaders)
        {
            if (request.Headers.ContainsKey(header))
            {
                var value = request.Headers[header].ToString();
                if (ContainsSuspiciousContent(value))
                {
                    _logger.LogWarning("Suspicious header detected: {Header} = {Value} from {RemoteIp}",
                        header, value, context.Connection.RemoteIpAddress);
                    return false;
                }
            }
        }

        // Validate Content-Type for POST/PUT requests
        if (request.Method == "POST" || request.Method == "PUT")
        {
            var contentType = request.ContentType;
            if (string.IsNullOrEmpty(contentType) || 
                (!contentType.Contains("application/json") && !contentType.Contains("application/x-www-form-urlencoded")))
            {
                _logger.LogWarning("Invalid Content-Type: {ContentType} from {RemoteIp}",
                    contentType, context.Connection.RemoteIpAddress);
                return false;
            }
        }

        return true;
    }

    private void SanitizeQueryParameters(HttpContext context)
    {
        var query = context.Request.Query;
        var sanitizedQuery = new Dictionary<string, string>();

        foreach (var kvp in query)
        {
            var sanitizedKey = SanitizeString(kvp.Key);
            var sanitizedValue = SanitizeString(kvp.Value.ToString());
            
            if (!string.IsNullOrEmpty(sanitizedKey) && !string.IsNullOrEmpty(sanitizedValue))
            {
                sanitizedQuery[sanitizedKey] = sanitizedValue;
            }
        }

        // Update query string if any sanitization occurred
        if (sanitizedQuery.Count != query.Count)
        {
            _logger.LogInformation("Query parameters sanitized for {Path}", context.Request.Path);
        }
    }

    private string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove potentially dangerous characters
        var dangerousChars = new[] { '<', '>', '"', '\'', '&', ';', '(', ')', '|', '`', '$' };
        
        foreach (var c in dangerousChars)
        {
            input = input.Replace(c.ToString(), string.Empty);
        }

        // Limit length
        if (input.Length > 1000)
        {
            input = input.Substring(0, 1000);
        }

        return input.Trim();
    }

    private bool ContainsSuspiciousContent(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var suspiciousPatterns = new[]
        {
            "<script", "javascript:", "vbscript:", "onload=", "onerror=",
            "eval(", "expression(", "url(", "import(", "require(",
            "union select", "drop table", "delete from", "insert into",
            "exec(", "execute(", "sp_", "xp_", "cmd.exe", "powershell"
        };

        var lowerInput = input.ToLowerInvariant();
        return suspiciousPatterns.Any(pattern => lowerInput.Contains(pattern));
    }
}

/// <summary>
/// Action filter for model validation
/// </summary>
public class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            context.Result = new BadRequestObjectResult(new
            {
                error = "Validation failed",
                details = errors,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

/// <summary>
/// Custom validation attributes
/// </summary>
public class MsisdnAttribute : ValidationAttribute
{
    public override bool IsValid(object value)
    {
        if (value == null) return true; // Let Required attribute handle nulls

        var msisdn = value.ToString();
        if (string.IsNullOrEmpty(msisdn)) return false;

        // Remove non-digit characters
        var cleaned = new string(msisdn.Where(char.IsDigit).ToArray());
        
        // Check length (10-15 digits)
        return cleaned.Length >= 10 && cleaned.Length <= 15;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid phone number (10-15 digits)";
    }
}

public class AmountAttribute : ValidationAttribute
{
    public override bool IsValid(object value)
    {
        if (value == null) return true;

        if (value is decimal decimalValue)
        {
            return decimalValue > 0 && decimalValue <= 1000000; // Max 1M NGN
        }

        if (value is double doubleValue)
        {
            return doubleValue > 0 && doubleValue <= 1000000;
        }

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be between 0.01 and 1,000,000 NGN";
    }
}

public class PinAttribute : ValidationAttribute
{
    public override bool IsValid(object value)
    {
        if (value == null) return true;

        var pin = value.ToString();
        if (string.IsNullOrEmpty(pin)) return false;

        // PIN must be 4-6 digits
        return pin.Length >= 4 && pin.Length <= 6 && pin.All(char.IsDigit);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be 4-6 digits";
    }
}

