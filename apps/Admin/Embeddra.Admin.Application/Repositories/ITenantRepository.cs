using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Repositories;

/// <summary>
/// Tenant repository interface.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
