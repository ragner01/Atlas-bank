using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AtlasBank.Infrastructure.Security;

/// <summary>
/// Enhanced authentication configuration
/// </summary>
public static class AuthenticationConfiguration
{
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Add("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // Add custom claims validation
                    var claims = context.Principal?.Claims;
                    if (claims != null)
                    {
                        // Validate required claims
                        if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
                        {
                            context.Fail("Missing user identifier claim");
                        }
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Add authorization policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAuthenticatedUser", policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy("RequireAdminRole", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

            options.AddPolicy("RequireCustomerRole", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Customer");
            });

            options.AddPolicy("RequireAgentRole", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Agent");
            });
        });

        // Add custom authorization handlers
        services.AddScoped<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TenantAuthorizationHandler>();
    }
}

/// <summary>
/// JWT settings configuration
/// </summary>
public class JwtSettings
{
    public string SecretKey { get; set; } = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    public string Issuer { get; set; } = "AtlasBank";
    public string Audience { get; set; } = "AtlasBankUsers";
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// Custom authorization handler for resource ownership
/// </summary>
public class ResourceOwnerAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var resourceUserId = requirement.ResourceUserId;

        if (userId == resourceUserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class ResourceOwnerRequirement : IAuthorizationRequirement
{
    public string ResourceUserId { get; }

    public ResourceOwnerRequirement(string resourceUserId)
    {
        ResourceUserId = resourceUserId;
    }
}

/// <summary>
/// Custom authorization handler for tenant access
/// </summary>
public class TenantAuthorizationHandler : AuthorizationHandler<TenantRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRequirement requirement)
    {
        var userTenant = context.User.FindFirst("tenant_id")?.Value;
        var resourceTenant = requirement.ResourceTenantId;

        if (userTenant == resourceTenant)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class TenantRequirement : IAuthorizationRequirement
{
    public string ResourceTenantId { get; }

    public TenantRequirement(string resourceTenantId)
    {
        ResourceTenantId = resourceTenantId;
    }
}

/// <summary>
/// JWT token service
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, string[] roles, Dictionary<string, object>? customClaims = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    bool IsTokenExpired(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtSettings> jwtSettings, ILogger<JwtTokenService> logger)
    {
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public string GenerateToken(string userId, string email, string[] roles, Dictionary<string, object>? customClaims = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email),
            new("jti", Guid.NewGuid().ToString()),
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add roles
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add custom claims
        if (customClaims != null)
        {
            foreach (var claim in customClaims)
            {
                claims.Add(new Claim(claim.Key, JsonSerializer.Serialize(claim.Value)));
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }

    public bool IsTokenExpired(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>
/// Password hashing service
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}

/// <summary>
/// Rate limiting for authentication endpoints
/// </summary>
public class AuthenticationRateLimiter
{
    private readonly Dictionary<string, List<DateTime>> _attempts = new();
    private readonly object _lock = new object();

    public bool IsRateLimited(string identifier, int maxAttempts = 5, TimeSpan window = default)
    {
        if (window == default)
            window = TimeSpan.FromMinutes(15);

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var key = identifier.ToLowerInvariant();

            if (!_attempts.ContainsKey(key))
            {
                _attempts[key] = new List<DateTime>();
            }

            var attempts = _attempts[key];
            
            // Remove old attempts outside the window
            attempts.RemoveAll(t => now - t > window);

            if (attempts.Count >= maxAttempts)
            {
                return true;
            }

            attempts.Add(now);
            return false;
        }
    }

    public void ClearAttempts(string identifier)
    {
        lock (_lock)
        {
            var key = identifier.ToLowerInvariant();
            _attempts.Remove(key);
        }
    }
}
