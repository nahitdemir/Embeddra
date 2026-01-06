using Microsoft.Extensions.Configuration;

namespace Embeddra.BuildingBlocks.Observability;

public static class ElasticApmConfigurationExtensions
{
    public static void AddEmbeddraElasticApm(
        this IConfigurationBuilder configuration,
        IConfiguration existingConfiguration,
        string serviceName)
    {
        var serverUrl = existingConfiguration["ELASTIC_APM_SERVER_URL"]
            ?? existingConfiguration["ElasticApm:ServerUrls"]
            ?? "http://localhost:8200";

        var environment = existingConfiguration["ASPNETCORE_ENVIRONMENT"]
            ?? existingConfiguration["ElasticApm:Environment"]
            ?? "Development";

        var values = new Dictionary<string, string?>
        {
            ["ElasticApm:ServiceName"] = serviceName,
            ["ElasticApm:ServerUrls"] = serverUrl,
            ["ElasticApm:Environment"] = environment
        };

        configuration.AddInMemoryCollection(values);
    }
}
