using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Repositories;

/// <summary>
/// Admin User repository interface.
/// </summary>
public interface IAdminUserRepository
{
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminUser?> FindByEmailAsync(string? tenantId, string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUser>> FindAllByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUser>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string? tenantId, string email, CancellationToken cancellationToken = default);
    Task AddAsync(AdminUser user, CancellationToken cancellationToken = default);
    Task UpdateAsync(AdminUser user, CancellationToken cancellationToken = default);
}
