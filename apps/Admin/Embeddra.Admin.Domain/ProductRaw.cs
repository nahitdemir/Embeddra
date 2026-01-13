namespace Embeddra.Admin.Domain;

/// <summary>
/// Ham ürün verisi - ingestion sırasında geçici olarak saklanan payload.
/// </summary>
public sealed class ProductRaw
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public Guid JobId { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core için parametresiz constructor
    private ProductRaw() { }

    /// <summary>
    /// Yeni bir ham ürün verisi kaydı oluşturur.
    /// </summary>
    public static ProductRaw Create(string tenantId, Guid jobId, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId gerekli.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson gerekli.", nameof(payloadJson));

        return new ProductRaw
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobId = jobId,
            PayloadJson = payloadJson,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
