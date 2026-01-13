namespace Embeddra.Admin.Domain;

/// <summary>
/// Kullanıcı durumu.
/// </summary>
public enum UserStatus
{
    Active,
    Disabled,
    Pending
}

/// <summary>
/// API anahtarı durumu.
/// </summary>
public enum ApiKeyStatus
{
    Active,
    Revoked
}

/// <summary>
/// Tenant durumu.
/// </summary>
public enum TenantStatus
{
    Active,
    Suspended
}
