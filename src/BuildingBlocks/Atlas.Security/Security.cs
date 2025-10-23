using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using Atlas.Common.ValueObjects;

namespace Atlas.Security;

/// <summary>
/// Represents a user context with tenant information
/// </summary>
public record UserContext
{
    public string UserId { get; init; } = string.Empty;
    public TenantId TenantId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
    public List<string> Permissions { get; init; } = new();
    public Dictionary<string, string> Claims { get; init; } = new();
}

/// <summary>
/// Interface for accessing current user context
/// </summary>
public interface IUserContextProvider
{
    UserContext? GetCurrentUser();
    TenantId GetCurrentTenant();
    bool IsAuthenticated { get; }
}

/// <summary>
/// HTTP context-based user context provider
/// </summary>
public class HttpUserContextProvider : IUserContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public UserContext? GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return null;

        var claims = httpContext.User.Claims.ToList();
        
        return new UserContext
        {
            UserId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            TenantId = new TenantId(claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value ?? "default"),
            Email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? string.Empty,
            Name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty,
            Roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList(),
            Permissions = claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList(),
            Claims = claims.ToDictionary(c => c.Type, c => c.Value)
        };
    }

    public TenantId GetCurrentTenant()
    {
        var user = GetCurrentUser();
        return user?.TenantId ?? new TenantId("default");
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}

/// <summary>
/// Authorization policy provider for dynamic policies
/// </summary>
public class AtlasAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public AtlasAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Tenant:"))
        {
            var tenantId = policyName[7..]; // Remove "Tenant:" prefix
            var policy = new AuthorizationPolicyBuilder()
                .RequireClaim("tenant_id", tenantId)
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (policyName.StartsWith("Permission:"))
        {
            var permission = policyName[11..]; // Remove "Permission:" prefix
            var policy = new AuthorizationPolicyBuilder()
                .RequireClaim("permission", permission)
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (policyName.StartsWith("Role:"))
        {
            var role = policyName[5..]; // Remove "Role:" prefix
            var policy = new AuthorizationPolicyBuilder()
                .RequireRole(role)
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackPolicyProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }
}

/// <summary>
/// Tenant context middleware for multi-tenancy
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ExtractTenantId(context);
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            context.Items["TenantId"] = tenantId;
            _logger.LogDebug("Set tenant context to {TenantId}", tenantId);
        }

        await _next(context);
    }

    private static string? ExtractTenantId(HttpContext context)
    {
        // Try to get tenant ID from various sources in order of preference
        
        // 1. From JWT claim
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim))
            return tenantClaim;

        // 2. From header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
            return headerValue.FirstOrDefault();

        // 3. From subdomain
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (subdomain != "www" && subdomain != "api")
                return subdomain;
        }

        // 4. From query parameter
        if (context.Request.Query.TryGetValue("tenant", out var queryValue))
            return queryValue.FirstOrDefault();

        return null;
    }
}

/// <summary>
/// Service collection extensions for security
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasSecurity(this IServiceCollection services, Action<SecurityOptions> configure)
    {
        var options = new SecurityOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<IUserContextProvider, HttpUserContextProvider>();
        services.AddHttpContextAccessor();

        // JWT Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = options.Authority;
                jwtOptions.Audience = options.Audience;
                jwtOptions.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
            });

        // Authorization
        services.AddAuthorization(authOptions =>
        {
            authOptions.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddSingleton<IAuthorizationPolicyProvider, AtlasAuthorizationPolicyProvider>();

        return services;
    }

    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantContextMiddleware>();
    }
}

public class SecurityOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;
}
