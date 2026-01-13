using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// Tenant Settings repository implementasyonu.
/// </summary>
public sealed class TenantSettingsRepository : ITenantSettingsRepository
{
    private readonly AdminDbContext _dbContext;

    public TenantSettingsRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantSetting?> GetAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantSetting>> GetAllByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TenantSettings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TenantSettings
            .AnyAsync(x => x.TenantId == tenantId && x.Key == key, cancellationToken);
    }

    public async Task AddAsync(TenantSetting setting, CancellationToken cancellationToken = default)
    {
        _dbContext.TenantSettings.Add(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TenantSetting setting, CancellationToken cancellationToken = default)
    {
        _dbContext.TenantSettings.Update(setting);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == key, cancellationToken);

        if (setting != null)
        {
            _dbContext.TenantSettings.Remove(setting);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
