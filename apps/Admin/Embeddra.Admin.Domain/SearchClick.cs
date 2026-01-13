namespace Embeddra.Admin.Domain;

public sealed class SearchClick
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid SearchId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
