using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AtlasBank.Infrastructure.Validation;

/// <summary>
/// Input validation middleware
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
        if (ShouldValidateRequest(context))
        {
            var validationResult = await ValidateRequestAsync(context);
            if (!validationResult.IsValid)
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                
                var errorResponse = new
                {
                    error = "Validation failed",
                    message = "Request validation failed",
                    details = validationResult.Errors,
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }
        }

        await _next(context);
    }

    private bool ShouldValidateRequest(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Only validate POST, PUT, PATCH requests
        if (!new[] { "POST", "PUT", "PATCH" }.Contains(method))
        {
            return false;
        }

        // Skip validation for certain endpoints
        var skipPaths = new[] { "/health", "/metrics", "/swagger" };
        if (skipPaths.Any(skipPath => path.Contains(skipPath)))
        {
            return false;
        }

        return true;
    }

    private async Task<ValidationResult> ValidateRequestAsync(HttpContext context)
    {
        var result = new ValidationResult();

        try
        {
            // Read request body
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrEmpty(body))
            {
                return result; // Empty body is valid for some endpoints
            }

            // Validate JSON structure
            try
            {
                var jsonDocument = JsonDocument.Parse(body);
                var validationErrors = ValidateJsonDocument(jsonDocument.RootElement, context.Request.Path);
                result.Errors.AddRange(validationErrors);
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"Invalid JSON format: {ex.Message}");
            }

            // Validate request headers
            var headerErrors = ValidateHeaders(context.Request.Headers);
            result.Errors.AddRange(headerErrors);

            // Validate query parameters
            var queryErrors = ValidateQueryParameters(context.Request.Query);
            result.Errors.AddRange(queryErrors);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during request validation");
            result.Errors.Add("Internal validation error");
        }

        return result;
    }

    private List<string> ValidateJsonDocument(JsonElement element, PathString path)
    {
        var errors = new List<string>();

        // Basic JSON validation rules
        if (element.ValueKind == JsonValueKind.Object)
        {
            var obj = element.EnumerateObject();
            foreach (var property in obj)
            {
                // Check for suspicious patterns
                if (property.Name.Length > 100)
                {
                    errors.Add($"Property name too long: {property.Name}");
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString() ?? "";
                    
                    // Check for SQL injection patterns
                    if (ContainsSqlInjectionPattern(value))
                    {
                        errors.Add($"Potential SQL injection in property: {property.Name}");
                    }

                    // Check for XSS patterns
                    if (ContainsXssPattern(value))
                    {
                        errors.Add($"Potential XSS in property: {property.Name}");
                    }

                    // Check string length
                    if (value.Length > 10000)
                    {
                        errors.Add($"Property value too long: {property.Name}");
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    var value = property.Value.GetDecimal();
                    
                    // Check for reasonable number ranges
                    if (Math.Abs(value) > 999999999)
                    {
                        errors.Add($"Number too large: {property.Name}");
                    }
                }
            }
        }

        return errors;
    }

    private List<string> ValidateHeaders(IHeaderDictionary headers)
    {
        var errors = new List<string>();

        // Validate Content-Type
        if (headers.ContainsKey("Content-Type"))
        {
            var contentType = headers["Content-Type"].ToString();
            if (!contentType.Contains("application/json") && !contentType.Contains("application/x-www-form-urlencoded"))
            {
                errors.Add("Invalid Content-Type header");
            }
        }

        // Validate User-Agent
        if (headers.ContainsKey("User-Agent"))
        {
            var userAgent = headers["User-Agent"].ToString();
            if (userAgent.Length > 500)
            {
                errors.Add("User-Agent header too long");
            }
        }

        // Check for suspicious headers
        var suspiciousHeaders = new[] { "X-Forwarded-Host", "X-Original-URL", "X-Rewrite-URL" };
        foreach (var header in suspiciousHeaders)
        {
            if (headers.ContainsKey(header))
            {
                errors.Add($"Suspicious header detected: {header}");
            }
        }

        return errors;
    }

    private List<string> ValidateQueryParameters(IQueryCollection query)
    {
        var errors = new List<string>();

        foreach (var param in query)
        {
            // Check parameter name length
            if (param.Key.Length > 100)
            {
                errors.Add($"Query parameter name too long: {param.Key}");
            }

            // Check parameter value length
            foreach (var value in param.Value)
            {
                if (value?.Length > 1000)
                {
                    errors.Add($"Query parameter value too long: {param.Key}");
                }

                // Check for suspicious patterns
                if (ContainsSqlInjectionPattern(value ?? ""))
                {
                    errors.Add($"Potential SQL injection in query parameter: {param.Key}");
                }
            }
        }

        return errors;
    }

    private bool ContainsSqlInjectionPattern(string input)
    {
        var patterns = new[]
        {
            "'; DROP TABLE",
            "UNION SELECT",
            "OR 1=1",
            "AND 1=1",
            "EXEC(",
            "EXECUTE(",
            "SCRIPT>",
            "<SCRIPT"
        };

        return patterns.Any(pattern => 
            input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsXssPattern(string input)
    {
        var patterns = new[]
        {
            "<script",
            "javascript:",
            "onload=",
            "onerror=",
            "onclick=",
            "onmouseover=",
            "onfocus=",
            "onblur="
        };

        return patterns.Any(pattern => 
            input.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();
}

