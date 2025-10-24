using System.ComponentModel.DataAnnotations;

namespace Atlas.Settlement;

/// <summary>
/// Configuration options for the Settlement service
/// </summary>
public class SettlementOptions
{
    public const string SectionName = "Settlement";

    /// <summary>
    /// Settlement processing interval in minutes
    /// </summary>
    [Range(1, 1440)] // 1 minute to 24 hours
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Merchant Discount Rate in basis points (e.g., 170 = 1.7%)
    /// </summary>
    [Range(0, 10000)] // 0 to 100%
    public decimal MdrBasisPoints { get; set; } = 170;

    /// <summary>
    /// Fixed fee in minor units (e.g., 100 = $1.00)
    /// </summary>
    [Range(0, 100000)] // 0 to $1000
    public long FixedFeeMinor { get; set; } = 100;

    /// <summary>
    /// Blob container name for settlement files
    /// </summary>
    [Required]
    [StringLength(63, MinimumLength = 3)]
    public string BlobContainerName { get; set; } = "settlement";

    /// <summary>
    /// Maximum settlement amount per merchant per day (in minor units)
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxSettlementAmountMinor { get; set; } = 100000000; // $1M

    /// <summary>
    /// Enable audit logging for settlement operations
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;
}
