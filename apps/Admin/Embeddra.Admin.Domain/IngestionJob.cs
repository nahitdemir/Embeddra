namespace Embeddra.Admin.Domain;

public sealed class IngestionJob
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public IngestionSourceType SourceType { get; set; } = IngestionSourceType.Json;
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;
    public int? TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
