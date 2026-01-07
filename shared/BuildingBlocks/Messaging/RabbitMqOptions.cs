using Microsoft.Extensions.Configuration;

namespace Embeddra.BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public string ConnectionString { get; set; } = "amqp://embeddra:embeddra@localhost:5672/";
    public string ExchangeName { get; set; } = "ingestion.jobs.exchange";
    public string IngestionQueueName { get; set; } = "ingestion.jobs";
    public string RetryQueueName { get; set; } = "ingestion.jobs.retry";
    public string DeadLetterQueueName { get; set; } = "ingestion.jobs.dlq";
    public int RetryDelayMilliseconds { get; set; } = 30000;
    public int MaxRetryCount { get; set; } = 5;
    public ushort PrefetchCount { get; set; } = 1;

    public static RabbitMqOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new RabbitMqOptions();

        var connectionString = configuration["RABBITMQ_CONNECTION_STRING"]
            ?? configuration["RabbitMq:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            options.ConnectionString = connectionString;
        }

        options.ExchangeName = configuration["RabbitMq:ExchangeName"] ?? options.ExchangeName;
        options.IngestionQueueName = configuration["RabbitMq:IngestionQueueName"] ?? options.IngestionQueueName;
        options.RetryQueueName = configuration["RabbitMq:RetryQueueName"] ?? options.RetryQueueName;
        options.DeadLetterQueueName = configuration["RabbitMq:DeadLetterQueueName"] ?? options.DeadLetterQueueName;

        var retryDelay = configuration.GetValue<int?>("RabbitMq:RetryDelayMilliseconds");
        if (retryDelay.HasValue && retryDelay.Value > 0)
        {
            options.RetryDelayMilliseconds = retryDelay.Value;
        }

        var maxRetryCount = configuration.GetValue<int?>("RabbitMq:MaxRetryCount");
        if (maxRetryCount.HasValue && maxRetryCount.Value >= 0)
        {
            options.MaxRetryCount = maxRetryCount.Value;
        }

        var prefetchCount = configuration.GetValue<int?>("RabbitMq:PrefetchCount");
        if (prefetchCount.HasValue && prefetchCount.Value > 0 && prefetchCount.Value <= ushort.MaxValue)
        {
            options.PrefetchCount = (ushort)prefetchCount.Value;
        }

        return options;
    }
}
