namespace Embeddra.Admin.Domain;

/// <summary>
/// Admin kullanıcısı - platform veya tenant yöneticisi.
/// Rich Domain Model: İş mantığı entity içinde.
/// </summary>
public sealed class AdminUser
{
    public Guid Id { get; private set; }
    public string? TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = UserRole.TenantOwner;
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    // EF Core için parametresiz constructor
    private AdminUser() { }

    /// <summary>
    /// Yeni bir admin kullanıcı oluşturur.
    /// </summary>
    public static AdminUser Create(string? tenantId, string email, string name, string passwordHash, string role)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email gerekli.", nameof(email));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name gerekli.", nameof(name));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash gerekli.", nameof(passwordHash));

        return new AdminUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            PasswordHash = passwordHash,
            Role = string.IsNullOrWhiteSpace(role) ? UserRole.TenantOwner : role,
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Platform owner mı?
    /// </summary>
    public bool IsPlatformOwner => TenantId is null && Role == UserRole.PlatformOwner;

    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive => Status == UserStatus.Active;

    /// <summary>
    /// Giriş kaydeder.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Şifre hashini günceller (re-hash gerektiğinde).
    /// </summary>
    public void UpdatePasswordHash(string newHash)
    {
        if (string.IsNullOrWhiteSpace(newHash))
            throw new ArgumentException("Yeni hash gerekli.", nameof(newHash));
        
        PasswordHash = newHash;
    }

    /// <summary>
    /// Kullanıcıyı devre dışı bırakır.
    /// </summary>
    public void Disable()
    {
        if (Status == UserStatus.Disabled)
            return;
        
        Status = UserStatus.Disabled;
    }

    /// <summary>
    /// Kullanıcıyı aktif eder.
    /// </summary>
    public void Enable()
    {
        if (Status == UserStatus.Active)
            return;
        
        Status = UserStatus.Active;
    }

    /// <summary>
    /// Kullanıcı bilgilerini günceller.
    /// </summary>
    public void Update(string name, string role)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();
        
        if (!string.IsNullOrWhiteSpace(role))
            Role = role;
    }
}
