namespace Embeddra.Admin.Domain;

/// <summary>
/// Tenant - müşteri organizasyonu.
/// Rich Domain Model.
/// </summary>
public sealed class Tenant
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core için parametresiz constructor
    private Tenant() { }

    /// <summary>
    /// Yeni bir tenant oluşturur.
    /// </summary>
    public static Tenant Create(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id gerekli.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name gerekli.", nameof(name));

        return new Tenant
        {
            Id = id.Trim(),
            Name = name.Trim(),
            Status = TenantStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive => Status == TenantStatus.Active;

    /// <summary>
    /// Tenant'ı askıya alır.
    /// </summary>
    public void Suspend()
    {
        if (Status == TenantStatus.Suspended)
            return;

        Status = TenantStatus.Suspended;
    }

    /// <summary>
    /// Tenant'ı aktif eder.
    /// </summary>
    public void Activate()
    {
        if (Status == TenantStatus.Active)
            return;

        Status = TenantStatus.Active;
    }
}
