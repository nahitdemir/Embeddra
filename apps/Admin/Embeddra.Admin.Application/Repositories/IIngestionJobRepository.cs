using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Repositories;

/// <summary>
/// Ingestion Job repository interface.
/// </summary>
public interface IIngestionJobRepository
{
    Task<IngestionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngestionJob>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(IngestionJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default);
}
