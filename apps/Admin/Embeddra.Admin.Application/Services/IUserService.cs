using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services;

/// <summary>
/// Kullanıcı servisi sonuç modeli.
/// </summary>
public sealed record UserTenantSummary(string? TenantId, string Email, string Role);

public sealed record LoginResult(
    bool Success,
    string? Token = null,
    DateTimeOffset? ExpiresAt = null,
    AdminUser? User = null,
    IReadOnlyList<UserTenantSummary>? Tenants = null,
    string? Error = null);

public sealed record CreateUserRequest(
    string? TenantId,
    string Email,
    string Name,
    string Password,
    string Role);

public sealed record CreateUserResult(
    bool Success,
    AdminUser? User = null,
    string? Error = null);

/// <summary>
/// Kullanıcı servis interface'i.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Kullanıcı girişi yapar.
    /// </summary>
    Task<LoginResult> LoginAsync(
        string? tenantId,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni kullanıcı oluşturur.
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcı getirir.
    /// </summary>
    Task<AdminUser?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant kullanıcılarını getirir.
    /// </summary>
    Task<IReadOnlyList<AdminUser>> GetUsersByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserTenantSummary>> GetUserTenantsAsync(
        string email,
        CancellationToken cancellationToken = default);
    Task<AdminUser?> UpdateUserAsync(Guid userId, string? tenantId, string? name, string? role, string? status, string? password, bool isPlatformAdmin);
}
