using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// IngestionJob repository implementasyonu.
/// </summary>
public sealed class IngestionJobRepository : IIngestionJobRepository
{
    private readonly AdminDbContext _dbContext;

    public IngestionJobRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IngestionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.IngestionJobs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IngestionJob>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.IngestionJobs
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        _dbContext.IngestionJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        _dbContext.IngestionJobs.Update(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
