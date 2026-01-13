using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// Tenant repository implementasyonu.
/// </summary>
public sealed class TenantRepository : ITenantRepository
{
    private readonly AdminDbContext _dbContext;

    public TenantRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .AnyAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        _dbContext.Tenants.Update(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
