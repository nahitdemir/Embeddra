using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.BuildingBlocks.Authentication;

public static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddEmbeddraApiKeyAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ApiKeyAuthenticationOptions>? configure = null)
    {
        var options = new ApiKeyAuthenticationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IApiKeyValidator>(_ => new PostgresApiKeyValidator(configuration));

        return services;
    }
}
