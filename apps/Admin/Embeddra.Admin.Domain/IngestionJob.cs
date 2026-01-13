namespace Embeddra.Admin.Domain;

/// <summary>
/// Ingestion işi - ürün verilerinin sisteme alınması sürecini temsil eder.
/// Rich Domain Model: İş mantığı entity içinde.
/// </summary>
public sealed class IngestionJob
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public IngestionSourceType SourceType { get; private set; } = IngestionSourceType.Json;
    public IngestionJobStatus Status { get; private set; } = IngestionJobStatus.Queued;
    public int? TotalCount { get; private set; }
    public int ProcessedCount { get; private set; }
    public int FailedCount { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // EF Core için parametresiz constructor
    private IngestionJob() { }

    /// <summary>
    /// Yeni bir ingestion job oluşturur.
    /// </summary>
    public static IngestionJob Create(string tenantId, IngestionSourceType sourceType, int? totalCount = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId gerekli.", nameof(tenantId));

        return new IngestionJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceType = sourceType,
            Status = IngestionJobStatus.Queued,
            TotalCount = totalCount,
            ProcessedCount = 0,
            FailedCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// İşi başlatır.
    /// </summary>
    public void Start()
    {
        if (Status != IngestionJobStatus.Queued)
            throw new InvalidOperationException($"Sadece Queued durumundaki işler başlatılabilir. Mevcut: {Status}");

        Status = IngestionJobStatus.Processing;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// İlerleme kaydeder.
    /// </summary>
    public void RecordProgress(int processed, int failed)
    {
        if (Status != IngestionJobStatus.Processing)
            throw new InvalidOperationException($"Sadece Processing durumundaki işlerde ilerleme kaydedilebilir. Mevcut: {Status}");

        ProcessedCount = processed;
        FailedCount = failed;
    }

    /// <summary>
    /// İşi başarıyla tamamlar.
    /// </summary>
    public void Complete()
    {
        if (Status != IngestionJobStatus.Processing)
            throw new InvalidOperationException($"Sadece Processing durumundaki işler tamamlanabilir. Mevcut: {Status}");

        Status = IngestionJobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// İşi hata ile sonlandırır.
    /// </summary>
    public void Fail(string error)
    {
        Status = IngestionJobStatus.Failed;
        Error = string.IsNullOrWhiteSpace(error) ? "Bilinmeyen hata" : TruncateError(error);
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// İş tamamlanmış mı (başarılı veya başarısız)?
    /// </summary>
    public bool IsCompleted => Status is IngestionJobStatus.Completed or IngestionJobStatus.Failed;

    /// <summary>
    /// İş başarıyla tamamlandı mı?
    /// </summary>
    public bool IsSuccessful => Status == IngestionJobStatus.Completed;

    private static string TruncateError(string error)
        => error.Length > 512 ? error[..512] : error;
}
