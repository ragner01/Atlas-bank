using Atlas.Common.ValueObjects;

namespace Atlas.Ledger.App;

/// <summary>
/// Service for managing tenant context
/// </summary>
public interface ITenantContext
{
    TenantId CurrentTenant { get; }
    bool IsValid { get; }
}

/// <summary>
/// Default tenant context implementation
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public TenantId CurrentTenant { get; }
    public bool IsValid { get; }

    public TenantContext(TenantId tenantId)
    {
        CurrentTenant = tenantId;
        IsValid = !string.IsNullOrWhiteSpace(tenantId.Value);
    }

    public static TenantContext FromHeader(string? tenantHeader)
    {
        if (string.IsNullOrWhiteSpace(tenantHeader))
        {
            return new TenantContext(new TenantId("tnt_demo")); // Default for development
        }
        
        return new TenantContext(new TenantId(tenantHeader));
    }
}
