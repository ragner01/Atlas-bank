using System.ComponentModel.DataAnnotations;

namespace Atlas.Ledger.Api.Models;

public class FastTransferRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Account ID must contain only alphanumeric characters, underscores, and hyphens")]
    public string SourceAccountId { get; set; } = default!;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Account ID must contain only alphanumeric characters, underscores, and hyphens")]
    public string DestinationAccountId { get; set; } = default!;

    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "Amount must be positive")]
    public long Minor { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter uppercase code")]
    public string Currency { get; set; } = default!;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Narration { get; set; } = default!;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Tenant ID must contain only alphanumeric characters, underscores, and hyphens")]
    public string TenantId { get; set; } = default!;
}
