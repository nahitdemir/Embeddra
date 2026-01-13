using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// ProductRaw repository implementasyonu.
/// </summary>
public sealed class ProductRawRepository : IProductRawRepository
{
    private readonly AdminDbContext _dbContext;

    public ProductRawRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ProductRaw productRaw, CancellationToken cancellationToken = default)
    {
        _dbContext.ProductsRaw.Add(productRaw);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductRaw?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductsRaw
            .FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
    }
}
