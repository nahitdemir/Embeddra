using RabbitMQ.Client;

namespace Embeddra.BuildingBlocks.Messaging;

public sealed class RabbitMqTopology
{
    public void DeclareIngestionTopology(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(options.ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);

        var mainQueueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = options.ExchangeName,
            ["x-dead-letter-routing-key"] = options.DeadLetterQueueName
        };
        channel.QueueDeclare(
            options.IngestionQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArgs);
        channel.QueueBind(options.IngestionQueueName, options.ExchangeName, options.IngestionQueueName);

        var retryQueueArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = options.ExchangeName,
            ["x-dead-letter-routing-key"] = options.IngestionQueueName,
            ["x-message-ttl"] = options.RetryDelayMilliseconds
        };
        channel.QueueDeclare(
            options.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArgs);
        channel.QueueBind(options.RetryQueueName, options.ExchangeName, options.RetryQueueName);

        channel.QueueDeclare(options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.DeadLetterQueueName, options.ExchangeName, options.DeadLetterQueueName);
    }
}
