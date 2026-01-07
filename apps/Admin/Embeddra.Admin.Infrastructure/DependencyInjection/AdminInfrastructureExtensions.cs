using Embeddra.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddHostedService<AdminDataInitializer>();

        return services;
    }
}
