using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.BuildingBlocks.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddraAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AuditDb")
            ?? "Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra";

        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddHostedService<AuditLogInitializer>();

        return services;
    }
}
