using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Repositories;

/// <summary>
/// Tenant Settings repository interface.
/// </summary>
public interface ITenantSettingsRepository
{
    Task<TenantSetting?> GetAsync(string tenantId, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSetting>> GetAllByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string tenantId, string key, CancellationToken cancellationToken = default);
    Task AddAsync(TenantSetting setting, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantSetting setting, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string key, CancellationToken cancellationToken = default);
}
