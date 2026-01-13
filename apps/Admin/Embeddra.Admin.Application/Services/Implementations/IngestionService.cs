using System.Text.Json;
using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Domain;
using Embeddra.BuildingBlocks.Messaging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.BuildingBlocks.Correlation;
using Microsoft.Extensions.Logging;

namespace Embeddra.Admin.Application.Services.Implementations;

public sealed class IngestionService : IIngestionService
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly IProductRawRepository _rawRepository;
    private readonly IIngestionJobPublisher _publisher;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IIngestionJobRepository jobRepository,
        IProductRawRepository rawRepository,
        IIngestionJobPublisher publisher,
        ILogger<IngestionService> logger)
    {
        _jobRepository = jobRepository;
        _rawRepository = rawRepository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<IngestionResult> StartBulkIngestionAsync(
        string tenantId,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        int count = 0;
        if (payload.ValueKind == JsonValueKind.Array)
        {
            count = payload.GetArrayLength();
        }
        else
        {
            // Tekil obje veya başka format, iş mantığına göre ele alınır.
            // Şimdilik 1 varsayıyoruz veya array zorunlu olabilir.
            count = 1;
        }

        var job = IngestionJob.Create(tenantId, IngestionSourceType.Json, count);
        var raw = ProductRaw.Create(tenantId, job.Id, payload.GetRawText());

        await _jobRepository.AddAsync(job, cancellationToken);
        await _rawRepository.AddAsync(raw, cancellationToken);

        var message = new IngestionJobMessage
        {
            JobId = job.Id.ToString(),
            TenantId = tenantId,
            SourceType = IngestionSourceType.Json.ToString(),
            Count = count
        };

        var correlationId = CorrelationContext.CorrelationId ?? Guid.NewGuid().ToString("N");

        try
        {
            await _publisher.PublishAsync(message, correlationId, cancellationToken);
            return new IngestionResult(job.Id, true, DocumentCount: count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ingestion_publish_failed {job_id} {tenant_id}", job.Id, tenantId);
            
            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            return new IngestionResult(job.Id, false, Error: "ingestion_publish_failed");
        }
    }

    public async Task<IngestionResult> StartCsvIngestionAsync(
        string tenantId,
        string csvContent,
        CancellationToken cancellationToken = default)
    {
        // CSV satır sayısını basitçe tahmin et (başlık hariç)
        var lineCount = csvContent.Count(c => c == '\n'); 
        // Daha iyi bir CSV parser kullanılabilir ama şimdilik basit tutalım.
        
        var job = IngestionJob.Create(tenantId, IngestionSourceType.Csv, lineCount > 0 ? lineCount - 1 : 0);
        var raw = ProductRaw.Create(tenantId, job.Id, csvContent); // CSV content'i raw payload olarak saklıyoruz

        await _jobRepository.AddAsync(job, cancellationToken);
        await _rawRepository.AddAsync(raw, cancellationToken);

        var message = new IngestionJobMessage
        {
            JobId = job.Id.ToString(),
            TenantId = tenantId,
            SourceType = IngestionSourceType.Csv.ToString(),
            Count = job.TotalCount ?? 0
        };

        var correlationId = CorrelationContext.CorrelationId ?? Guid.NewGuid().ToString("N");

        try
        {
            await _publisher.PublishAsync(message, correlationId, cancellationToken);
            return new IngestionResult(job.Id, true, DocumentCount: job.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ingestion_publish_failed {job_id} {tenant_id}", job.Id, tenantId);

            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            return new IngestionResult(job.Id, false, Error: "ingestion_publish_failed");
        }
    }

    public async Task<IngestionJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _jobRepository.GetByIdAsync(jobId, cancellationToken);
    }
}
