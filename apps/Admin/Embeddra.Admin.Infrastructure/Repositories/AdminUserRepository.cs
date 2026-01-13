using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Embeddra.Admin.Infrastructure.Repositories;

/// <summary>
/// AdminUser repository implementasyonu.
/// </summary>
public sealed class AdminUserRepository : IAdminUserRepository
{
    private readonly AdminDbContext _dbContext;

    public AdminUserRepository(AdminDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdminUsers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<AdminUser?> FindByEmailAsync(string? tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        
        if (tenantId is null)
        {
            return await _dbContext.AdminUsers
                .FirstOrDefaultAsync(x => x.TenantId == null && x.Email == normalizedEmail, cancellationToken);
        }

        return await _dbContext.AdminUsers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> FindAllByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        
        return await _dbContext.AdminUsers
            .Where(x => x.Email == normalizedEmail)
            .OrderBy(x => x.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AdminUsers
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string? tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        
        if (tenantId is null)
        {
            return await _dbContext.AdminUsers
                .AnyAsync(x => x.TenantId == null && x.Email == normalizedEmail, cancellationToken);
        }

        return await _dbContext.AdminUsers
            .AnyAsync(x => x.TenantId == tenantId && x.Email == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        _dbContext.AdminUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        _dbContext.AdminUsers.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
