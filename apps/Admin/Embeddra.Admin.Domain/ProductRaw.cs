namespace Embeddra.Admin.Domain;

public sealed class ProductRaw
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
