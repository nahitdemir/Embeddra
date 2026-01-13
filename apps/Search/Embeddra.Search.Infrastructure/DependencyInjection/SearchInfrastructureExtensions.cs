using Embeddra.Search.Application.Embedding;
using Embeddra.Search.Infrastructure.Analytics;
using Embeddra.Search.Infrastructure.Embedding;
using Embeddra.Search.Infrastructure.Security;
using Embeddra.Search.Infrastructure.Tuning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.Search.Infrastructure.DependencyInjection;

public static class SearchInfrastructureExtensions
{
    public static IServiceCollection AddEmbeddraSearchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(EmbeddingOptions.FromConfiguration(configuration));
        services.AddSingleton<IEmbeddingClient, DeterministicEmbeddingClient>();
        services.AddSingleton<SearchAdminDb>();
        services.AddSingleton<SearchAnalyticsWriter>();
        services.AddSingleton<AllowedOriginRepository>();
        services.AddSingleton<SearchTuningRepository>();

        return services;
    }
}
