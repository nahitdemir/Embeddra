using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services;

/// <summary>
/// Tenant oluşturma isteği.
/// </summary>
public sealed record CreateTenantRequest(string TenantId, string Name);

/// <summary>
/// Tenant oluşturma sonucu.
/// </summary>
public sealed record CreateTenantResult(
    bool Success,
    Tenant? Tenant = null,
    string? Error = null);

/// <summary>
/// Tenant servis interface'i.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Yeni tenant oluşturur.
    /// </summary>
    Task<CreateTenantResult> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant'ı getirir.
    /// </summary>
    Task<Tenant?> GetTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant var mı kontrol eder.
    /// </summary>
    Task<bool> ExistsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
