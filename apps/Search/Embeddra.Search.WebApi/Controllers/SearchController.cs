using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Search.WebApi.Controllers;

[ApiController]
public sealed class SearchController : ControllerBase
{
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

        var response = await client.PostAsync("/products/_search", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        stopwatch.Stop();

        var (tookMs, total) = ParseElasticsearchResponse(body);
        var resultCount = total ?? 0;
        var noResult = resultCount == 0;

        _logger.LogInformation(
            "search_metrics {duration_ms} {es_took_ms} {result_count} {no_result}",
            stopwatch.ElapsedMilliseconds,
            tookMs,
            resultCount,
            noResult);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new { error = "elasticsearch_error" });
        }

        return Ok(new { took = tookMs, total = resultCount });
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
