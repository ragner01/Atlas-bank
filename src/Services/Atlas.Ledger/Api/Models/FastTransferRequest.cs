using System.ComponentModel.DataAnnotations;

namespace Atlas.Ledger.Api.Models;

/// <summary>
/// Request model for the high-performance fast-transfer endpoint.
/// Includes comprehensive validation and API documentation.
/// </summary>
public class FastTransferRequest
{
    /// <summary>
    /// Source account identifier for the transfer
    /// </summary>
    [Required(ErrorMessage = "Source Account ID is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Source Account ID must be between 1 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Source Account ID must contain only alphanumeric characters, underscores, and hyphens")]
    [Display(Description = "Unique identifier of the source account")]
    public string SourceAccountId { get; set; } = default!;

    /// <summary>
    /// Destination account identifier for the transfer
    /// </summary>
    [Required(ErrorMessage = "Destination Account ID is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Destination Account ID must be between 1 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Destination Account ID must contain only alphanumeric characters, underscores, and hyphens")]
    [Display(Description = "Unique identifier of the destination account")]
    public string DestinationAccountId { get; set; } = default!;

    /// <summary>
    /// Transfer amount in minor units (e.g., cents for USD, kobo for NGN)
    /// </summary>
    [Required(ErrorMessage = "Minor amount is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "Amount must be positive")]
    [Display(Description = "Transfer amount in minor units (e.g., 10000 = 100.00 NGN)")]
    public long Minor { get; set; }

    /// <summary>
    /// Currency code in ISO 4217 format (e.g., NGN, USD, EUR)
    /// </summary>
    [Required(ErrorMessage = "Currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be exactly 3 characters.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter uppercase code")]
    [Display(Description = "Three-letter ISO 4217 currency code")]
    public string Currency { get; set; } = default!;

    /// <summary>
    /// Description or narration for the transfer
    /// </summary>
    [Required(ErrorMessage = "Narration is required.")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Narration must be between 1 and 256 characters.")]
    [Display(Description = "Description or narration for the transfer")]
    public string Narration { get; set; } = default!;

    /// <summary>
    /// Tenant identifier for multi-tenant operations
    /// </summary>
    [Required(ErrorMessage = "Tenant ID is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Tenant ID must be between 1 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Tenant ID must contain only alphanumeric characters, underscores, and hyphens")]
    [Display(Description = "Tenant identifier for multi-tenant operations")]
    public string TenantId { get; set; } = default!;

    /// <summary>
    /// Idempotency key to prevent duplicate transfers
    /// </summary>
    [Required(ErrorMessage = "Idempotency key is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Idempotency key must be between 1 and 100 characters.")]
    [Display(Description = "Unique key to prevent duplicate transfers")]
    public string IdempotencyKey { get; set; } = default!;
}
