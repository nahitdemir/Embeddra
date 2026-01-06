using Elastic.Apm.SerilogEnricher;
using Elastic.CommonSchema.Serilog;
using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
            var elasticsearchUser = configuration["ELASTICSEARCH_USERNAME"];
            var elasticsearchPassword = configuration["ELASTICSEARCH_PASSWORD"];

            var sinkOptions = new ElasticsearchSinkOptions(new Uri(elasticsearchUrl))
            {
                IndexFormat = indexName,
                AutoRegisterTemplate = false,
                CustomFormatter = new EcsTextFormatter()
            };

            if (!string.IsNullOrWhiteSpace(elasticsearchUser) || !string.IsNullOrWhiteSpace(elasticsearchPassword))
            {
                sinkOptions.ModifyConnectionSettings = connection =>
                    connection.BasicAuthentication(elasticsearchUser ?? "elastic", elasticsearchPassword ?? string.Empty);
            }

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
                .WriteTo.Elasticsearch(sinkOptions);
        });
    }
}
