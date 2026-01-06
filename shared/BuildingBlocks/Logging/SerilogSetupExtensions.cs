using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Elastic.Apm.SerilogEnricher;
using Elastic.CommonSchema.Serilog;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace Embeddra.BuildingBlocks.Logging;

public static class SerilogSetupExtensions
{
    public static IHostBuilder UseEmbeddraSerilog(
        this IHostBuilder hostBuilder,
        IConfiguration configuration,
        string serviceName,
        string indexName,
        string? serviceVersion = null)
    {
        return hostBuilder.UseSerilog((context, loggerConfiguration) =>
        {
            var environment = context.HostingEnvironment.EnvironmentName ?? "Unknown";
            var elasticsearchUrl = configuration["ELASTICSEARCH_URL"] ?? "http://localhost:9200";

            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service.name", serviceName)
                .Enrich.WithProperty("service.version", serviceVersion ?? "unknown")
                .Enrich.WithProperty("service.environment", environment)
                .Enrich.WithProperty("environment", environment)
                .Enrich.With<CorrelationIdEnricher>()
                .Enrich.With<TenantIdEnricher>()
                .Enrich.With<ElasticApmLogEnricher>()
                .Enrich.WithElasticApmCorrelationInfo()
                .WriteTo.Console(new EcsTextFormatter())
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
                {
                    IndexFormat = indexName,
                    AutoRegisterTemplate = false,
                    CustomFormatter = new EcsTextFormatter()
                });
        });
    }
}
