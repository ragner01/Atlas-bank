using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AtlasBank.Infrastructure.Security;

/// <summary>
/// Enhanced security headers middleware
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Content Security Policy
        if (!string.IsNullOrEmpty(_options.ContentSecurityPolicy))
        {
            response.Headers.Append("Content-Security-Policy", _options.ContentSecurityPolicy);
        }

        // X-Frame-Options
        if (!string.IsNullOrEmpty(_options.XFrameOptions))
        {
            response.Headers.Append("X-Frame-Options", _options.XFrameOptions);
        }

        // X-Content-Type-Options
        if (_options.XContentTypeOptions)
        {
            response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-XSS-Protection
        if (_options.XXssProtection)
        {
            response.Headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy
        if (!string.IsNullOrEmpty(_options.ReferrerPolicy))
        {
            response.Headers.Append("Referrer-Policy", _options.ReferrerPolicy);
        }

        // Permissions-Policy
        if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
        {
            response.Headers.Append("Permissions-Policy", _options.PermissionsPolicy);
        }

        // Strict-Transport-Security
        if (_options.StrictTransportSecurity)
        {
            var hstsHeader = $"max-age={_options.HstsMaxAge}";
            if (_options.HstsIncludeSubDomains)
            {
                hstsHeader += "; includeSubDomains";
            }
            if (_options.HstsPreload)
            {
                hstsHeader += "; preload";
            }
            response.Headers.Append("Strict-Transport-Security", hstsHeader);
        }

        // Remove Server header
        if (_options.RemoveServerHeader)
        {
            response.Headers.Remove("Server");
        }

        // Add custom security headers
        foreach (var header in _options.CustomHeaders)
        {
            response.Headers.Append(header.Key, header.Value);
        }

        await _next(context);
    }
}

public class SecurityHeadersOptions
{
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' https:; connect-src 'self' https:; frame-ancestors 'none';";
    public string XFrameOptions { get; set; } = "DENY";
    public bool XContentTypeOptions { get; set; } = true;
    public bool XXssProtection { get; set; } = true;
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    public string PermissionsPolicy { get; set; } = "geolocation=(), microphone=(), camera=()";
    public bool StrictTransportSecurity { get; set; } = true;
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
    public bool HstsIncludeSubDomains { get; set; } = true;
    public bool HstsPreload { get; set; } = true;
    public bool RemoveServerHeader { get; set; } = true;
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

