using Embeddra.Contracts;

namespace Embeddra.Admin.Domain;

/// <summary>
/// API Anahtarı - Search API erişimi için.
/// Rich Domain Model: İş mantığı entity içinde.
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string KeyType { get; private set; } = ApiKeyTypes.SearchPublic;
    public string? KeyHash { get; private set; }
    public string? KeyPrefix { get; private set; }
    public string[] AllowedOrigins { get; private set; } = Array.Empty<string>();
    public ApiKeyStatus Status { get; private set; } = ApiKeyStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // EF Core için parametresiz constructor
    private ApiKey() { }

    /// <summary>
    /// Yeni bir API anahtarı oluşturur.
    /// </summary>
    public static ApiKey Create(
        string tenantId,
        string name,
        string keyHash,
        string keyPrefix,
        string? description = null,
        string? keyType = null,
        string[]? allowedOrigins = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId gerekli.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name gerekli.", nameof(name));
        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("KeyHash gerekli.", nameof(keyHash));

        return new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            KeyType = ApiKeyTypes.Normalize(keyType),
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            AllowedOrigins = allowedOrigins ?? Array.Empty<string>(),
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Anahtar aktif mi?
    /// </summary>
    public bool IsActive => Status == ApiKeyStatus.Active;

    /// <summary>
    /// Anahtar iptal edilmiş mi?
    /// </summary>
    public bool IsRevoked => Status == ApiKeyStatus.Revoked;

    /// <summary>
    /// Anahtarı iptal eder.
    /// </summary>
    public void Revoke()
    {
        if (Status == ApiKeyStatus.Revoked)
            return;

        Status = ApiKeyStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Belirtilen origin'e izin verilmiş mi?
    /// </summary>
    public bool IsOriginAllowed(string origin)
    {
        if (AllowedOrigins.Length == 0)
            return true; // Eğer liste boşsa tüm origin'lere izin ver

        return AllowedOrigins.Any(allowed => 
            string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase));
    }
}
