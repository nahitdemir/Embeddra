using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// ApiKey repository implementasyonu.
/// </summary>
public sealed class ApiKeyRepository : IApiKeyRepository
{
    private readonly AdminDbContext _dbContext;

    public ApiKeyRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyHashExistsAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .AnyAsync(x => x.KeyHash == keyHash, cancellationToken);
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        _dbContext.ApiKeys.Update(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
