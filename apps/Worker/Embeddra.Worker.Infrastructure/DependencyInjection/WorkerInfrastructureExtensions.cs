using Embeddra.Worker.Application.Embedding;
using Embeddra.Worker.Application.Processing;
using Embeddra.Worker.Infrastructure.Embedding;
using Embeddra.Worker.Infrastructure.Indexing;
using Embeddra.Worker.Infrastructure.Persistence;
using Embeddra.Worker.Infrastructure.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.Worker.Infrastructure.DependencyInjection;

public static class WorkerInfrastructureExtensions
{
    public static IServiceCollection AddEmbeddraWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AdminDb")
            ?? configuration.GetConnectionString("AuditDb")
            ?? "Host=localhost;Port=5433;Database=embeddra;Username=embeddra;Password=embeddra";

        services.AddDbContext<IngestionDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton(EmbeddingOptions.FromConfiguration(configuration));
        services.AddSingleton<IEmbeddingClient, DeterministicEmbeddingClient>();
        services.AddSingleton<ElasticBulkIndexer>();
        services.AddSingleton<ElasticsearchIndexManager>();
        services.AddHostedService<ElasticsearchIndexInitializer>();
        services.AddScoped<IIngestionJobProcessor, IngestionJobProcessor>();

        return services;
    }
}
