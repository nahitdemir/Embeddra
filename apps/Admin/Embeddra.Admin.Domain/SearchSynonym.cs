namespace Embeddra.Admin.Domain;

public sealed class SearchSynonym
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string SynonymsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}
