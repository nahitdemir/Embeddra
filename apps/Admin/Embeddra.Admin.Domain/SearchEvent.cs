namespace Embeddra.Admin.Domain;

public sealed class SearchEvent
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public int? Bm25TookMs { get; set; }
    public int? KnnTookMs { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
