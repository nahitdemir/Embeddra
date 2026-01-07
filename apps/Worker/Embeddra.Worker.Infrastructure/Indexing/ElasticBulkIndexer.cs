using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Embeddra.Worker.Infrastructure.Indexing;

public sealed class ElasticBulkIndexer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public ElasticBulkIndexer(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ElasticBulkIndexResult> IndexAsync(
        string indexName,
        IReadOnlyList<ElasticBulkIndexDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return new ElasticBulkIndexResult(0, 0, null, false);
        }

        var payload = BuildBulkPayload(indexName, documents);
        var client = _httpClientFactory.CreateClient("elasticsearch");
        using var content = new StringContent(payload, Encoding.UTF8, "application/x-ndjson");

        using var response = await client.PostAsync("/_bulk", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Elasticsearch bulk request failed with status {(int)response.StatusCode}: {body}");
        }

        return ParseBulkResponse(body, documents.Count);
    }

    private static string BuildBulkPayload(string indexName, IReadOnlyList<ElasticBulkIndexDocument> documents)
    {
        var builder = new StringBuilder();
        foreach (var document in documents)
        {
            var action = new Dictionary<string, object?>
            {
                ["index"] = new Dictionary<string, object?>
                {
                    ["_index"] = indexName,
                    ["_id"] = document.Id
                }
            };

            builder.AppendLine(JsonSerializer.Serialize(action, SerializerOptions));
            builder.AppendLine(JsonSerializer.Serialize(document.Body, SerializerOptions));
        }

        return builder.ToString();
    }

    private static ElasticBulkIndexResult ParseBulkResponse(string body, int expectedCount)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var errors = root.TryGetProperty("errors", out var errorsElement)
                && errorsElement.ValueKind == JsonValueKind.True;

            long? took = null;
            if (root.TryGetProperty("took", out var tookElement) && tookElement.ValueKind == JsonValueKind.Number)
            {
                took = tookElement.GetInt64();
            }

            var failedCount = 0;
            var totalCount = expectedCount;

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                totalCount = items.GetArrayLength();

                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var operation = item.EnumerateObject().FirstOrDefault();
                    if (operation.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var failed = false;
                    if (operation.Value.TryGetProperty("error", out _))
                    {
                        failed = true;
                    }
                    else if (operation.Value.TryGetProperty("status", out var statusElement)
                        && statusElement.ValueKind == JsonValueKind.Number
                        && statusElement.GetInt32() >= 300)
                    {
                        failed = true;
                    }

                    if (failed)
                    {
                        failedCount++;
                    }
                }
            }
            else if (errors)
            {
                failedCount = expectedCount;
            }

            return new ElasticBulkIndexResult(totalCount, failedCount, took, errors);
        }
        catch (JsonException)
        {
            return new ElasticBulkIndexResult(expectedCount, expectedCount, null, true);
        }
    }
}

public sealed record ElasticBulkIndexResult(int TotalCount, int FailedCount, long? TookMs, bool Errors);

public sealed record ElasticBulkIndexDocument(string Id, IDictionary<string, object?> Body);
