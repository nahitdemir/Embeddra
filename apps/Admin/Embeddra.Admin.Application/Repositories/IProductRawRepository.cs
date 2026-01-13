using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Repositories;

/// <summary>
/// Product Raw Data repository interface.
/// </summary>
public interface IProductRawRepository
{
    Task AddAsync(ProductRaw productRaw, CancellationToken cancellationToken = default);
    Task<ProductRaw?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
