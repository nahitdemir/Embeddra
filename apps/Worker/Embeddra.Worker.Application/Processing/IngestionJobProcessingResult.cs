namespace Embeddra.Worker.Application.Processing;

public sealed record IngestionJobProcessingResult(
    Guid JobId,
    string TenantId,
    string SourceType,
    int AttemptedCount,
    int ProcessedCount,
    int FailedCount,
    long BulkDurationMs,
    long? EsTookMs);
