using System.Diagnostics;
using System.Text.Json;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Messaging;
using Embeddra.BuildingBlocks.Tenancy;
using Embeddra.Worker.Application.Processing;
using Elastic.Apm;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Embeddra.Worker.Host;

public sealed class Worker : BackgroundService
{
    private static readonly JsonSerializerOptions MessageSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqTopology _topology;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        RabbitMqOptions options,
        RabbitMqTopology topology)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
        _topology = topology;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "rabbitmq_consumer_crashed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunConsumerAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        _topology.DeclareIngestionTopology(channel, _options);
        channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) => await HandleMessageAsync(channel, args, stoppingToken);

        var consumerTag = channel.BasicConsume(_options.IngestionQueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation(
            "rabbitmq_consumer_started {queue} {prefetch}",
            _options.IngestionQueueName,
            _options.PrefetchCount);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        finally
        {
            TryCancelConsumer(channel, consumerTag);
        }
    }

    private async Task HandleMessageAsync(IModel channel, BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        var headers = args.BasicProperties?.Headers ?? new Dictionary<string, object>();
        var correlationId = RabbitMqCorrelation.GetCorrelationId(headers);
        if (string.IsNullOrWhiteSpace(correlationId) && !string.IsNullOrWhiteSpace(args.BasicProperties?.CorrelationId))
        {
            correlationId = args.BasicProperties?.CorrelationId;
        }

        correlationId ??= Guid.NewGuid().ToString("N");
        CorrelationContext.CorrelationId = correlationId;

        var retryCount = RabbitMqHeaders.GetRetryCount(headers);
        IngestionJobMessage? message = null;

        try
        {
            message = DeserializeMessage(args.Body);
            await ProcessIngestionJobAsync(message, correlationId, retryCount, cancellationToken);
            channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            HandleProcessingFailure(channel, args, message, correlationId, retryCount, ex);
        }
        finally
        {
            CorrelationContext.CorrelationId = null;
        }
    }

    private async Task ProcessIngestionJobAsync(
        IngestionJobMessage message,
        string correlationId,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var transaction = Agent.Tracer.StartTransaction("IngestionJob", "background");
        var stopwatch = Stopwatch.StartNew();

        using var scope = _scopeFactory.CreateScope();
        var auditLogWriter = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
        var jobProcessor = scope.ServiceProvider.GetRequiredService<IIngestionJobProcessor>();

        var jobId = message.JobId ?? "unknown";
        var tenantId = message.TenantId ?? "unknown";
        var sourceType = message.SourceType ?? "unknown";
        TenantContext.TenantId = string.IsNullOrWhiteSpace(message.TenantId) ? null : message.TenantId;

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    AuditActions.IngestionJobStarted,
                    "worker",
                    new
                    {
                        job_id = jobId,
                        tenant_id = tenantId,
                        source_type = sourceType,
                        retry_count = retryCount,
                        correlation_id = correlationId
                    }),
                cancellationToken);

            var result = await jobProcessor.ProcessAsync(message, retryCount, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "ingestion_job_completed {job_id} {tenant_id} {job_duration_ms} {batch_size} {bulk_duration_ms} {failed_items_count}",
                result.JobId,
                result.TenantId,
                stopwatch.ElapsedMilliseconds,
                result.AttemptedCount,
                result.BulkDurationMs,
                result.FailedCount);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    AuditActions.IngestionJobCompleted,
                    "worker",
                    new
                    {
                        job_id = result.JobId,
                        tenant_id = result.TenantId,
                        source_type = result.SourceType,
                        batch_size = result.AttemptedCount,
                        processed_count = result.ProcessedCount,
                        job_duration_ms = stopwatch.ElapsedMilliseconds,
                        bulk_duration_ms = result.BulkDurationMs,
                        es_took_ms = result.EsTookMs,
                        failed_items_count = result.FailedCount,
                        retry_count = retryCount,
                        correlation_id = correlationId
                    }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            transaction.CaptureException(ex);

            _logger.LogError(
                ex,
                "ingestion_job_failed {job_id} {tenant_id} {job_duration_ms} {retry_count}",
                jobId,
                tenantId,
                stopwatch.ElapsedMilliseconds,
                retryCount);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    AuditActions.IngestionJobFailed,
                    "worker",
                    new
                    {
                        job_id = jobId,
                        tenant_id = tenantId,
                        source_type = sourceType,
                        job_duration_ms = stopwatch.ElapsedMilliseconds,
                        retry_count = retryCount,
                        correlation_id = correlationId,
                        error_summary = ex.Message
                    }),
                cancellationToken);

            throw;
        }
        finally
        {
            transaction.End();
            TenantContext.TenantId = null;
        }
    }

    private void HandleProcessingFailure(
        IModel channel,
        BasicDeliverEventArgs args,
        IngestionJobMessage? message,
        string correlationId,
        int retryCount,
        Exception exception)
    {
        var nextRetry = retryCount + 1;
        var shouldRetry = nextRetry <= _options.MaxRetryCount;
        var targetQueue = shouldRetry ? _options.RetryQueueName : _options.DeadLetterQueueName;
        var outgoingRetryCount = shouldRetry ? nextRetry : retryCount;
        var jobId = message?.JobId ?? "unknown";
        var tenantId = message?.TenantId ?? "unknown";

        try
        {
            var properties = CreatePublishProperties(channel, args.BasicProperties, correlationId, outgoingRetryCount);
            channel.BasicPublish(_options.ExchangeName, targetQueue, properties, args.Body);
            channel.BasicAck(args.DeliveryTag, multiple: false);

            _logger.LogWarning(
                exception,
                "ingestion_job_retry_enqueued {queue} {retry_count} {job_id} {tenant_id} {correlation_id}",
                targetQueue,
                outgoingRetryCount,
                jobId,
                tenantId,
                correlationId);
        }
        catch (Exception publishException)
        {
            _logger.LogError(
                publishException,
                "ingestion_job_retry_publish_failed {queue} {job_id} {tenant_id} {correlation_id}",
                targetQueue,
                jobId,
                tenantId,
                correlationId);

            channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static IBasicProperties CreatePublishProperties(
        IModel channel,
        IBasicProperties? originalProperties,
        string correlationId,
        int retryCount)
    {
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = originalProperties?.ContentType ?? "application/json";
        properties.ContentEncoding = originalProperties?.ContentEncoding;
        properties.Type = originalProperties?.Type;
        properties.AppId = originalProperties?.AppId;
        properties.MessageId = originalProperties?.MessageId ?? Guid.NewGuid().ToString("N");
        properties.CorrelationId = correlationId;

        var headers = new Dictionary<string, object>(originalProperties?.Headers ?? new Dictionary<string, object>());
        RabbitMqCorrelation.SetCorrelationId(headers, correlationId);
        RabbitMqHeaders.SetRetryCount(headers, retryCount);
        properties.Headers = headers;

        return properties;
    }

    private static IngestionJobMessage DeserializeMessage(ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
        {
            throw new InvalidOperationException("Ingestion job payload is empty.");
        }

        var message = JsonSerializer.Deserialize<IngestionJobMessage>(body.Span, MessageSerializerOptions);
        if (message is null)
        {
            throw new InvalidOperationException("Ingestion job payload is invalid.");
        }

        return message;
    }

    private static void TryCancelConsumer(IModel channel, string consumerTag)
    {
        try
        {
            channel.BasicCancel(consumerTag);
        }
        catch
        {
        }
    }
}
