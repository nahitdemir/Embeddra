using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Infrastructure.Repositories;

namespace Embeddra.Admin.Infrastructure.DependencyInjection;

public static class AdminInfrastructureExtensions
{
    public static IServiceCollection AddEmbeddraAdminPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AdminDb")
            ?? configuration.GetConnectionString("AuditDb")
            ?? "Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra";



        services.AddDbContext<AdminDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IIngestionJobRepository, IngestionJobRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IProductRawRepository, ProductRawRepository>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();

        services.AddHostedService<AdminDataInitializer>();

        return services;
    }
}
