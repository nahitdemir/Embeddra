using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Search.WebApi.Controllers;

[ApiController]
public sealed class SearchController : ControllerBase
{
    private const string IndexPrefix = "products";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IHttpClientFactory httpClientFactory, ILogger<SearchController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var indexName = ResolveIndexName(tenantId);
        var client = _httpClientFactory.CreateClient("elasticsearch");
        var esQuery = new
        {
            query = new
            {
                query_string = new
                {
                    query = request.Query
                }
            },
            size = request.Size ?? 10
        };

        var content = new StringContent(JsonSerializer.Serialize(esQuery), Encoding.UTF8, "application/json");
        var stopwatch = Stopwatch.StartNew();

        var response = await client.PostAsync($"/{indexName}/_search", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        stopwatch.Stop();

        var (tookMs, total) = ParseElasticsearchResponse(body);
        var resultCount = total ?? 0;
        var noResult = resultCount == 0;

        _logger.LogInformation(
            "search_metrics {duration_ms} {es_took_ms} {result_count} {no_result} {index_name}",
            stopwatch.ElapsedMilliseconds,
            tookMs,
            resultCount,
            noResult,
            indexName);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new { error = "elasticsearch_error" });
        }

        return Ok(new { took = tookMs, total = resultCount });
    }

    private static string ResolveIndexName(string tenantId)
    {
        var normalized = NormalizeIndexSegment(tenantId);
        return string.IsNullOrWhiteSpace(normalized)
            ? $"{IndexPrefix}-default"
            : $"{IndexPrefix}-{normalized}";
    }

    private static string NormalizeIndexSegment(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '-' || ch == '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var sanitized = builder.ToString().Trim('-', '_');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        return sanitized.Length > 60 ? sanitized[..60] : sanitized;
    }

    private static (long? TookMs, long? Total) ParseElasticsearchResponse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            long? took = null;
            if (root.TryGetProperty("took", out var tookElement) && tookElement.ValueKind == JsonValueKind.Number)
            {
                took = tookElement.GetInt64();
            }

            long? total = null;
            if (root.TryGetProperty("hits", out var hitsElement)
                && hitsElement.TryGetProperty("total", out var totalElement))
            {
                if (totalElement.ValueKind == JsonValueKind.Object
                    && totalElement.TryGetProperty("value", out var valueElement)
                    && valueElement.ValueKind == JsonValueKind.Number)
                {
                    total = valueElement.GetInt64();
                }
            }

            return (took, total);
        }
        catch
        {
            return (null, null);
        }
    }
}

public sealed record SearchRequest(string Query, int? Size);
