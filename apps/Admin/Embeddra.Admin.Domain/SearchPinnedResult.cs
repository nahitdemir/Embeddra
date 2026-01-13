namespace Embeddra.Admin.Domain;

public sealed class SearchPinnedResult
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string ProductIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}
