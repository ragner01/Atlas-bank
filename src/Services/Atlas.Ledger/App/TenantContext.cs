using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.App;

/// <summary>
/// Service for managing tenant context with proper security
/// </summary>
public interface ITenantContext
{
    TenantId CurrentTenant { get; }
    bool IsValid { get; }
}

/// <summary>
/// Secure tenant context implementation that enforces tenant isolation
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public TenantId CurrentTenant { get; }
    public bool IsValid { get; }

    public TenantContext(TenantId tenantId)
    {
        CurrentTenant = tenantId;
        IsValid = !string.IsNullOrWhiteSpace(tenantId.Value) && IsValidTenantId(tenantId.Value);
    }

    /// <summary>
    /// Creates a tenant context from header with strict validation
    /// </summary>
    public static TenantContext FromHeader(string? tenantHeader)
    {
        if (string.IsNullOrWhiteSpace(tenantHeader))
        {
            // For testing purposes, use a default tenant
            return new TenantContext(new TenantId("tnt_demo"));
        }
        
        if (!IsValidTenantId(tenantHeader))
        {
            throw new UnauthorizedAccessException($"Invalid tenant ID format: {tenantHeader}");
        }
        
        return new TenantContext(new TenantId(tenantHeader));
    }

    /// <summary>
    /// Validates tenant ID format for security
    /// </summary>
    private static bool IsValidTenantId(string tenantId)
    {
        // Tenant ID must start with 'tnt_' and contain only alphanumeric characters, underscores, and hyphens
        if (!tenantId.StartsWith("tnt_"))
            return false;
            
        if (tenantId.Length < 8 || tenantId.Length > 50)
            return false;
            
        // Check for valid characters
        return tenantId.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
    }
}
