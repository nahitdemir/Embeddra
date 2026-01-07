using System.Text;
using System.Text.Json;
using Embeddra.BuildingBlocks.Messaging;
using RabbitMQ.Client;

namespace Embeddra.Admin.WebApi.Messaging;

public sealed class IngestionJobPublisher
{
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqTopology _topology;
    private readonly ILogger<IngestionJobPublisher> _logger;

    public IngestionJobPublisher(
        RabbitMqOptions options,
        RabbitMqTopology topology,
        ILogger<IngestionJobPublisher> logger)
    {
        _options = options;
        _topology = topology;
        _logger = logger;
    }

    public Task PublishAsync(IngestionJobMessage message, string correlationId, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString)
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        _topology.DeclareIngestionTopology(channel, _options);

        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.CorrelationId = correlationId;
        properties.Headers = new Dictionary<string, object>();
        RabbitMqCorrelation.SetCorrelationId(properties.Headers, correlationId);
        RabbitMqHeaders.SetRetryCount(properties.Headers, 0);

        var payload = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(payload);

        channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: _options.IngestionQueueName,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "ingestion_job_published {job_id} {tenant_id} {source_type} {count}",
            message.JobId,
            message.TenantId,
            message.SourceType,
            message.Count);

        return Task.CompletedTask;
    }
}
