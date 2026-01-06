using System.Diagnostics;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Messaging;
using Elastic.Apm;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.Worker.Host;

public sealed class Worker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIngestionJobAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunIngestionJobAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        CorrelationContext.CorrelationId = correlationId;

        var transaction = Agent.Tracer.StartTransaction("IngestionJob", "background");
        var stopwatch = Stopwatch.StartNew();

        using var scope = _scopeFactory.CreateScope();
        var auditLogWriter = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogEntry(AuditActions.IngestionJobStarted, "worker", new { correlation_id = correlationId }),
                cancellationToken);

            var batchSize = Random.Shared.Next(50, 250);
            var failedItems = 0;

            var dbSpan = transaction.StartSpan("DB.FetchProductsRaw", "db");
            await Task.Delay(80, cancellationToken);
            dbSpan.End();

            var embeddingSpan = transaction.StartSpan("Embedding.Generate", "app");
            await Task.Delay(120, cancellationToken);
            embeddingSpan.End();

            var esSpan = transaction.StartSpan("ES.BulkIndex", "elasticsearch");
            var bulkStopwatch = Stopwatch.StartNew();
            await Task.Delay(140, cancellationToken);
            bulkStopwatch.Stop();
            esSpan.End();

            var updateSpan = transaction.StartSpan("DB.UpdateJobStatus", "db");
            await Task.Delay(60, cancellationToken);
            updateSpan.End();

            var headers = new Dictionary<string, object>();
            RabbitMqCorrelation.SetCorrelationId(headers, correlationId);
            var propagatedCorrelationId = RabbitMqCorrelation.GetCorrelationId(headers);

            var rabbitSpan = transaction.StartSpan("Rabbit.Ack", "messaging");
            await Task.Delay(30, cancellationToken);
            rabbitSpan.End();

            stopwatch.Stop();

            _logger.LogInformation(
                "ingestion_job_completed {job_duration_ms} {batch_size} {bulk_duration_ms} {failed_items_count}",
                stopwatch.ElapsedMilliseconds,
                batchSize,
                bulkStopwatch.ElapsedMilliseconds,
                failedItems);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    AuditActions.IngestionJobCompleted,
                    "worker",
                    new
                    {
                        batch_size = batchSize,
                        job_duration_ms = stopwatch.ElapsedMilliseconds,
                        bulk_duration_ms = bulkStopwatch.ElapsedMilliseconds,
                        failed_items_count = failedItems,
                        correlation_id = propagatedCorrelationId
                    }),
                cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            transaction.CaptureException(ex);

            _logger.LogError(ex, "ingestion_job_failed {job_duration_ms}", stopwatch.ElapsedMilliseconds);

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(
                    AuditActions.IngestionJobFailed,
                    "worker",
                    new
                    {
                        job_duration_ms = stopwatch.ElapsedMilliseconds,
                        error_summary = ex.Message
                    }),
                cancellationToken);
        }
        finally
        {
            transaction.End();
            CorrelationContext.CorrelationId = null;
        }
    }
}
