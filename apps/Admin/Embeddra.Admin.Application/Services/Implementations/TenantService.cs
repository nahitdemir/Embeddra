using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services.Implementations;

public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;

    public TenantService(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _tenantRepository.ExistsAsync(request.TenantId, cancellationToken);
        if (exists)
        {
            return new CreateTenantResult(false, Error: "tenant_exists");
        }

        var tenant = Tenant.Create(request.TenantId, request.Name);
        await _tenantRepository.AddAsync(tenant, cancellationToken);

        return new CreateTenantResult(true, Tenant: tenant);
    }

    public async Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _tenantRepository.ExistsAsync(tenantId, cancellationToken);
    }
}
