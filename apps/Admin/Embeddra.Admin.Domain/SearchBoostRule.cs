namespace Embeddra.Admin.Domain;

public sealed class SearchBoostRule
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double Weight { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
