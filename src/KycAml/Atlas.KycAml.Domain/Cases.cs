namespace Atlas.KycAml.Domain;

public enum CaseStatus { Open, UnderReview, Escalated, Closed }

public sealed class AmlCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = "tnt_demo";
    public string CustomerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public string? Owner { get; set; }
}
