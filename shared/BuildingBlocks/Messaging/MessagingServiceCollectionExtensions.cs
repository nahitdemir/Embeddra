using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.BuildingBlocks.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddraRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RabbitMqOptions>? configure = null)
    {
        var options = RabbitMqOptions.FromConfiguration(configuration);
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<RabbitMqTopology>();

        return services;
    }
}
