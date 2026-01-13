namespace Embeddra.Admin.Domain;

/// <summary>
/// Tenant-specific setting - runtime configuration değeri.
/// Rich Domain Model.
/// </summary>
public sealed class TenantSetting
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public SettingType Type { get; private set; }
    public string? Description { get; private set; }
    public bool IsSensitive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // EF Core için parametresiz constructor
    private TenantSetting() { }

    /// <summary>
    /// Yeni bir tenant setting oluşturur.
    /// </summary>
    public static TenantSetting Create(
        string tenantId,
        string key,
        string value,
        SettingType type,
        string? description = null,
        bool isSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId gerekli.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key gerekli.", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var now = DateTimeOffset.UtcNow;
        return new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Trim(),
            Key = key.Trim(),
            Value = value,
            Type = type,
            Description = description?.Trim(),
            IsSensitive = isSensitive,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Setting değerini günceller.
    /// </summary>
    public void UpdateValue(string value, string? description = null)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        Value = value;
        if (description != null)
        {
            Description = description.Trim();
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Setting value type.
/// </summary>
public enum SettingType
{
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3
}
