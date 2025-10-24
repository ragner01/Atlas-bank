using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AtlasBank.Api.Middleware;

/// <summary>
/// Middleware for adding comprehensive security headers
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Content Security Policy
        response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' https:; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'");

        // Prevent MIME type sniffing
        response.Headers.Append("X-Content-Type-Options", "nosniff");

        // Enable XSS protection
        response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // Prevent clickjacking
        response.Headers.Append("X-Frame-Options", "DENY");

        // Referrer Policy
        response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions Policy (formerly Feature Policy)
        response.Headers.Append("Permissions-Policy", 
            "geolocation=(), " +
            "microphone=(), " +
            "camera=(), " +
            "payment=(), " +
            "usb=(), " +
            "magnetometer=(), " +
            "gyroscope=(), " +
            "speaker=(), " +
            "vibrate=(), " +
            "fullscreen=(self), " +
            "sync-xhr=()");

        // Strict Transport Security (only for HTTPS)
        if (context.Request.IsHttps)
        {
            response.Headers.Append("Strict-Transport-Security", 
                "max-age=31536000; includeSubDomains; preload");
        }

        // Cross-Origin policies
        response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
        response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
        response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");

        // Remove server information
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");
        response.Headers.Remove("X-AspNet-Version");
        response.Headers.Remove("X-AspNetMvc-Version");

        // Add custom security headers
        response.Headers.Append("X-Robots-Tag", "noindex, nofollow");
        response.Headers.Append("X-Download-Options", "noopen");

        await _next(context);
    }
}

/// <summary>
/// CORS configuration for production security
/// </summary>
public static class CorsConfiguration
{
    public static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { "https://atlasbank.com", "https://console.atlasbank.com" };

        services.AddCors(options =>
        {
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });

            options.AddPolicy("Development", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public static class RateLimitingConfiguration
{
    public static void ConfigureRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global rate limit
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            // API endpoints rate limit
            options.AddFixedWindowLimiter("Api", options =>
            {
                options.PermitLimit = 60;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 5;
            });

            // Authentication endpoints rate limit
            options.AddFixedWindowLimiter("Auth", options =>
            {
                options.PermitLimit = 5;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 2;
            });

            // Transfer endpoints rate limit
            options.AddFixedWindowLimiter("Transfer", options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 3;
            });
        });
    }
}
