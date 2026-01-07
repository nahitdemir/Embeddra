using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Embeddra.BuildingBlocks.Messaging;
using Embeddra.Worker.Application.Embedding;
using Embeddra.Worker.Application.Processing;
using Embeddra.Worker.Infrastructure.Indexing;
using Embeddra.Worker.Infrastructure.Persistence;
using Elastic.Apm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Embeddra.Worker.Infrastructure.Processing;

public sealed class IngestionJobProcessor : IIngestionJobProcessor
{
    private readonly IngestionDbContext _dbContext;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly ElasticBulkIndexer _bulkIndexer;
    private readonly ElasticsearchIndexManager _indexManager;
    private readonly ILogger<IngestionJobProcessor> _logger;

    public IngestionJobProcessor(
        IngestionDbContext dbContext,
        IEmbeddingClient embeddingClient,
        ElasticBulkIndexer bulkIndexer,
        ElasticsearchIndexManager indexManager,
        ILogger<IngestionJobProcessor> logger)
    {
        _dbContext = dbContext;
        _embeddingClient = embeddingClient;
        _bulkIndexer = bulkIndexer;
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task<IngestionJobProcessingResult> ProcessAsync(
        IngestionJobMessage message,
        int retryCount,
        CancellationToken cancellationToken)
    {
        _ = retryCount;
        var jobId = ParseJobId(message.JobId);
        var tenantId = message.TenantId ?? throw new InvalidOperationException("tenant_id is required.");
        var sourceType = message.SourceType ?? "unknown";
        var transaction = Agent.Tracer.CurrentTransaction;

        IngestionJob? job = null;
        var parseFailures = 0;
        var processedCount = 0;
        var failedCount = 0;

        try
        {
            job = await _dbContext.IngestionJobs
                .SingleOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId, cancellationToken);

            if (job is null)
            {
                throw new InvalidOperationException($"Ingestion job not found: {jobId}");
            }

            await _indexManager.EnsureProductIndexAsync(tenantId, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            job.Status = IngestionJobStatus.Processing;
            job.StartedAt ??= now;
            if (!job.TotalCount.HasValue || job.TotalCount.Value <= 0)
            {
                var inferredCount = message.Count > 0 ? message.Count : (int?)null;
                if (inferredCount.HasValue)
                {
                    job.TotalCount = inferredCount.Value;
                }
            }

            var startSpan = transaction?.StartSpan("DB.UpdateJobStatus", "db");
            await _dbContext.SaveChangesAsync(cancellationToken);
            startSpan?.End();

            var fetchSpan = transaction?.StartSpan("DB.FetchProductsRaw", "db");
            var rawRows = await _dbContext.ProductsRaw
                .AsNoTracking()
                .Where(x => x.JobId == jobId && x.TenantId == tenantId)
                .ToListAsync(cancellationToken);
            fetchSpan?.End();

            var buildResult = BuildDocuments(rawRows);
            parseFailures = buildResult.ParseFailures;
            var documents = buildResult.Documents;
            if (!job.TotalCount.HasValue || job.TotalCount.Value <= 0)
            {
                job.TotalCount = documents.Count + parseFailures;
            }

            if (documents.Count == 0)
            {
                failedCount = parseFailures;
                processedCount = 0;
                await UpdateJobCompletedAsync(job, processedCount, failedCount, cancellationToken);

                return new IngestionJobProcessingResult(
                    jobId,
                    tenantId,
                    sourceType,
                    0,
                    processedCount,
                    failedCount,
                    0,
                    null);
            }

            var embeddingSpan = transaction?.StartSpan("Embedding.Generate", "app");
            var embeddings = await _embeddingClient.EmbedAsync(
                documents.Select(x => x.EmbeddingText).ToList(),
                cancellationToken);
            embeddingSpan?.End();

            if (embeddings.Count != documents.Count)
            {
                throw new InvalidOperationException("Embedding client returned unexpected batch size.");
            }

            for (var i = 0; i < documents.Count; i++)
            {
                documents[i].Body["embedding"] = embeddings[i];
            }

            var indexName = ElasticIndexNameResolver.ResolveProductIndexName(tenantId);
            var bulkSpan = transaction?.StartSpan("ES.BulkIndex", "elasticsearch");
            var bulkStopwatch = Stopwatch.StartNew();
            var bulkResult = await _bulkIndexer.IndexAsync(
                indexName,
                documents.Select(x => new ElasticBulkIndexDocument(x.Id, x.Body)).ToList(),
                cancellationToken);
            bulkStopwatch.Stop();
            bulkSpan?.End();

            if (bulkResult.Errors)
            {
                failedCount = parseFailures + bulkResult.FailedCount;
                processedCount = Math.Max(documents.Count - bulkResult.FailedCount, 0);
                await UpdateJobFailedAsync(job, processedCount, failedCount, "elasticsearch_bulk_errors", cancellationToken);
                throw new InvalidOperationException("Elasticsearch bulk request reported errors.");
            }

            failedCount = parseFailures + bulkResult.FailedCount;
            processedCount = Math.Max(documents.Count - bulkResult.FailedCount, 0);
            await UpdateJobCompletedAsync(job, processedCount, failedCount, cancellationToken);

            if (parseFailures > 0 || bulkResult.FailedCount > 0)
            {
                _logger.LogWarning(
                    "ingestion_job_partial_failures {job_id} {tenant_id} {parse_failures} {bulk_failures}",
                    jobId,
                    tenantId,
                    parseFailures,
                    bulkResult.FailedCount);
            }

            return new IngestionJobProcessingResult(
                jobId,
                tenantId,
                sourceType,
                documents.Count,
                processedCount,
                failedCount,
                bulkStopwatch.ElapsedMilliseconds,
                bulkResult.TookMs);
        }
        catch (Exception ex)
        {
            if (job is not null)
            {
                failedCount = Math.Max(failedCount, parseFailures);
                await UpdateJobFailedAsync(job, processedCount, failedCount, ex.Message, cancellationToken);
            }

            throw;
        }
    }

    private async Task UpdateJobCompletedAsync(
        IngestionJob job,
        int processedCount,
        int failedCount,
        CancellationToken cancellationToken)
    {
        var transaction = Agent.Tracer.CurrentTransaction;
        var span = transaction?.StartSpan("DB.UpdateJobStatus", "db");
        job.Status = IngestionJobStatus.Completed;
        job.ProcessedCount = processedCount;
        job.FailedCount = failedCount;
        job.Error = failedCount > 0 ? "partial_failures" : null;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        span?.End();
    }

    private async Task UpdateJobFailedAsync(
        IngestionJob job,
        int processedCount,
        int failedCount,
        string error,
        CancellationToken cancellationToken)
    {
        var transaction = Agent.Tracer.CurrentTransaction;
        var span = transaction?.StartSpan("DB.UpdateJobStatus", "db");
        job.Status = IngestionJobStatus.Failed;
        job.ProcessedCount = processedCount;
        job.FailedCount = failedCount;
        job.Error = TruncateError(error);
        job.CompletedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        span?.End();
    }

    private static string TruncateError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown";
        }

        return message.Length > 512 ? message[..512] : message;
    }

    private static Guid ParseJobId(string? jobId)
    {
        if (Guid.TryParse(jobId, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("job_id is invalid.");
    }

    private static BuildDocumentsResult BuildDocuments(IEnumerable<ProductRaw> rawRows)
    {
        var documents = new List<ProductIndexDocument>();
        var parseFailures = 0;

        foreach (var row in rawRows)
        {
            if (string.IsNullOrWhiteSpace(row.PayloadJson))
            {
                parseFailures++;
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(row.PayloadJson);
                var index = 0;
                foreach (var element in ExtractProducts(document.RootElement))
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        parseFailures++;
                        continue;
                    }

                    var product = BuildDocument(row, element, index);
                    if (product is null)
                    {
                        parseFailures++;
                        continue;
                    }

                    documents.Add(product);
                    index++;
                }
            }
            catch (JsonException)
            {
                parseFailures++;
            }
        }

        return new BuildDocumentsResult(documents, parseFailures);
    }

    private static IEnumerable<JsonElement> ExtractProducts(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in new[] { "documents", "items", "products" })
            {
                if (root.TryGetProperty(property, out var array)
                    && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in array.EnumerateArray())
                    {
                        yield return element;
                    }

                    yield break;
                }
            }

            if (root.TryGetProperty("document", out var document) && document.ValueKind == JsonValueKind.Object)
            {
                yield return document;
                yield break;
            }

            yield return root;
        }
    }

    private static ProductIndexDocument? BuildDocument(ProductRaw row, JsonElement element, int index)
    {
        var productId = GetString(element, "id", "productId", "product_id")
            ?? $"{row.Id:N}-{index}";

        var name = GetString(element, "name", "title");
        var description = GetString(element, "description", "summary");
        var brand = GetString(element, "brand");
        var category = GetString(element, "category", "category_name");
        var price = GetDecimal(element, "price", "unit_price", "amount");
        var inStock = GetBool(element, "in_stock", "inStock", "available");
        var attributes = GetObject(element, "attributes", "attrs", "metadata");

        var body = new Dictionary<string, object?>
        {
            ["tenant_id"] = row.TenantId,
            ["product_id"] = productId,
            ["name"] = name,
            ["description"] = description,
            ["brand"] = brand,
            ["category"] = category,
            ["price"] = price,
            ["in_stock"] = inStock,
            ["attributes"] = attributes
        };

        var embeddingText = BuildEmbeddingText(name, description, brand, category, row.PayloadJson);
        return new ProductIndexDocument(productId, embeddingText, body);
    }

    private static string BuildEmbeddingText(
        string? name,
        string? description,
        string? brand,
        string? category,
        string fallback)
    {
        var parts = new List<string>();
        AppendIfNotEmpty(parts, name);
        AppendIfNotEmpty(parts, description);
        AppendIfNotEmpty(parts, brand);
        AppendIfNotEmpty(parts, category);

        var text = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static void AppendIfNotEmpty(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            else if (property.ValueKind == JsonValueKind.Number)
            {
                return property.ToString();
            }
        }

        return null;
    }

    private static decimal? GetDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var text = property.GetString();
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var text = property.GetString();
                if (bool.TryParse(text, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static JsonElement? GetObject(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Object)
            {
                return property.Clone();
            }
        }

        return null;
    }

    private sealed record BuildDocumentsResult(
        List<ProductIndexDocument> Documents,
        int ParseFailures);

    private sealed record ProductIndexDocument(
        string Id,
        string EmbeddingText,
        Dictionary<string, object?> Body);
}
