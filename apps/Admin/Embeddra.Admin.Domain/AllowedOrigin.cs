namespace Embeddra.Admin.Domain;

public sealed class AllowedOrigin
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
